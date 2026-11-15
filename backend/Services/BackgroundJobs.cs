using Dapper;
using Iot04.Api.Domain;
using Iot04.Api.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Iot04.Api.Services;

/// <summary>
/// UC04 — ConnectionMonitor: quét mỗi 30s, chuyển FSM CONNECTED -> STALE (2x) -> DISCONNECTED (5x),
/// mở DataGap (start_time = last_reading_at) và phát Alert SENSOR_OFFLINE (BR04–BR07).
/// </summary>
public sealed class ConnectionMonitor(Db db, AlertService alerts, Notifier notifier, IConfiguration cfg, ILogger<ConnectionMonitor> log) : BackgroundService
{
    private sealed class Row
    {
        public Guid Id { get; set; }
        public string HardwareId { get; set; } = "";
        public Guid? ZoneId { get; set; }
        public string ConnectionState { get; set; } = "";
        public string Status { get; set; } = "";
        public DateTime LastReadingAt { get; set; }
        public int StaleThresholdSec { get; set; }
        public int DisconnectThresholdSec { get; set; }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var period = TimeSpan.FromSeconds(cfg.GetValue("Monitor:ScanSeconds", 30));
        await Task.Delay(TimeSpan.FromSeconds(3), ct);
        using var timer = new PeriodicTimer(period);
        do
        {
            try { await ScanAsync(); }
            catch (Exception ex) { log.LogError(ex, "ConnectionMonitor lỗi"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    public async Task ScanAsync()
    {
        await using var c = await db.OpenAsync();
        var rows = await c.QueryAsync<Row>(@"
            SELECT id, hardware_id AS HardwareId, zone_id AS ZoneId, connection_state AS ConnectionState, status,
                   last_reading_at AS LastReadingAt, stale_threshold_sec AS StaleThresholdSec, disconnect_threshold_sec AS DisconnectThresholdSec
            FROM sensors WHERE status <> 'DECOMMISSIONED' AND last_reading_at IS NOT NULL");

        var now = DateTime.UtcNow;
        foreach (var s in rows)
        {
            var age = (now - s.LastReadingAt).TotalSeconds;
            var next = age >= s.DisconnectThresholdSec ? "DISCONNECTED" : age >= s.StaleThresholdSec ? "STALE" : "CONNECTED";
            // CONNECTED chỉ được thiết lập bởi bản tin thật (IngestionService), monitor chỉ "hạ cấp"
            if (next == s.ConnectionState || next == "CONNECTED") continue;
            if (s.ConnectionState == "DISCONNECTED") continue;

            await c.ExecuteAsync("UPDATE sensors SET connection_state=@next, status=CASE WHEN @next='DISCONNECTED' AND status IN ('ONLINE','DEGRADED') THEN 'OFFLINE' ELSE status END WHERE id=@Id",
                new { next, s.Id });
            log.LogWarning("{Hw}: {From} -> {To} (dataAge={Age:0}s)", s.HardwareId, s.ConnectionState, next, age);

            if (next == "STALE")
            {
                await alerts.RaiseAsync(c, s.ZoneId, s.Id, "SENSOR_STALE", "INFO",
                    $"{s.HardwareId}: không nhận dữ liệu {age / 60:0.0} phút (> 2x chu kỳ) — dữ liệu đang cũ");
            }
            else
            {
                try
                {
                    await c.ExecuteAsync("INSERT INTO data_gaps(sensor_id, zone_id, start_time, reason) VALUES (@Id, @ZoneId, @LastReadingAt, 'SENSOR_OFFLINE')",
                        new { s.Id, s.ZoneId, s.LastReadingAt });
                }
                catch (SqlException ex) when (ex.Number is 2601 or 2627) { /* đã có gap mở */ }

                await alerts.AutoResolveAsync(c, s.Id, "SENSOR_STALE", "Tự động: đã nâng cấp thành SENSOR_OFFLINE");
                await alerts.RaiseAsync(c, s.ZoneId, s.Id, "SENSOR_OFFLINE", "CRITICAL",
                    $"{s.HardwareId}: mất tín hiệu {age / 60:0.0} phút (> 5x chu kỳ) — đã mở DataGap");
            }
            await notifier.SensorState(new { sensorId = s.Id, hardwareId = s.HardwareId, zoneId = s.ZoneId, from = s.ConnectionState, to = next, at = now });
        }
    }
}

/// <summary>
/// UC05 — Drift detection. So sánh mỗi cảm biến với TRUNG VỊ CÁC CẢM BIẾN KHÁC cùng zone (leave-one-out),
/// theo từng bin thời gian. Tốc độ trôi = hệ số góc hồi quy tuyến tính của độ lệch theo giờ trên cửa sổ dài
/// (mặc định 6h, vì cửa sổ 1h của đồ án quá ngắn để phân biệt 0.1°C/h với nhiễu cảm biến).
/// CUSUM hai phía cập nhật tăng dần qua các lần đánh giá, reset khi hiệu chuẩn.
/// </summary>
public sealed class DriftDetectionService(Db db, AlertService alerts, Notifier notifier, IConfiguration cfg, ILogger<DriftDetectionService> log) : BackgroundService
{
    private double K => cfg.GetValue("Drift:K", 0.05);
    private double H => cfg.GetValue("Drift:H", 1.5);
    private int WindowHours => cfg.GetValue("Drift:WindowHours", 6);
    private int BinMinutes => cfg.GetValue("Drift:BinMinutes", 5);
    private int MinBins => cfg.GetValue("Drift:MinBins", 12);

    private sealed class SensorRow
    {
        public Guid Id { get; set; }
        public string HardwareId { get; set; } = "";
        public Guid ZoneId { get; set; }
        public string SensorType { get; set; } = "";
        public DateTime? LastCalibratedAt { get; set; }
    }
    private sealed class Profile
    {
        public Guid SensorId { get; set; }
        public decimal BaselineOffset { get; set; }
        public decimal CusumPositive { get; set; }
        public decimal CusumNegative { get; set; }
        public DateTime? LastEvaluatedAt { get; set; }
    }
    private sealed class BinRow
    {
        public Guid SensorId { get; set; }
        public long Bin { get; set; }
        public double Value { get; set; }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(cfg.GetValue("Drift:InitialDelaySeconds", 20)), ct);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(cfg.GetValue("Drift:IntervalMinutes", 15)));
        do
        {
            try { await EvaluateAllAsync(); }
            catch (Exception ex) { log.LogError(ex, "DriftDetection lỗi"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    public async Task EvaluateAllAsync()
    {
        await using var c = await db.OpenAsync();
        var sensors = (await c.QueryAsync<SensorRow>(@"
            SELECT id, hardware_id AS HardwareId, zone_id AS ZoneId, sensor_type AS SensorType, last_calibrated_at AS LastCalibratedAt
            FROM sensors WHERE zone_id IS NOT NULL AND status NOT IN ('DECOMMISSIONED','CALIBRATING')")).ToList();

        foreach (var zone in sensors.GroupBy(s => s.ZoneId))
            foreach (var metric in new[] { "TEMPERATURE", "HUMIDITY" })
            {
                var group = zone.Where(s => s.SensorType == "COMBO" || s.SensorType == metric).ToList();
                if (group.Count > 0) await EvaluateGroupAsync(c, zone.Key, metric, group);
            }
        log.LogInformation("Drift detection hoàn tất cho {Count} cảm biến", sensors.Count);
    }

    private async Task EvaluateGroupAsync(SqlConnection c, Guid zoneId, string metric, List<SensorRow> group)
    {
        var now = DateTime.UtcNow;
        var from = now.AddHours(-WindowHours);
        var col = metric == "TEMPERATURE" ? "temperature" : "humidity";
        var ids = group.Select(g => g.Id).ToArray();
        var bins = (await c.QueryAsync<BinRow>($@"
            SELECT sensor_id AS SensorId, DATEDIFF_BIG(MINUTE, '2000-01-01', reading_time) / @bin AS Bin, AVG(CAST({col} AS FLOAT)) AS Value
            FROM readings
            WHERE zone_id=@zoneId AND reading_time >= @from AND quality <> 'ANOMALOUS_REJECT' AND {col} IS NOT NULL AND sensor_id IN @ids
            GROUP BY sensor_id, DATEDIFF_BIG(MINUTE, '2000-01-01', reading_time) / @bin",
            new { zoneId, from, bin = BinMinutes, ids })).ToList();

        // Chỉ dùng dữ liệu sau lần hiệu chuẩn gần nhất của chính cảm biến
        var calBin = group.ToDictionary(g => g.Id, g => g.LastCalibratedAt is { } t ? (long)(t - new DateTime(2000, 1, 1)).TotalMinutes / BinMinutes : long.MinValue);
        var bySensor = bins.Where(b => b.Bin > calBin[b.SensorId]).GroupBy(b => b.SensorId).ToDictionary(g => g.Key, g => g.ToDictionary(x => x.Bin, x => x.Value));
        var active = group.Where(g => bySensor.TryGetValue(g.Id, out var d) && d.Count >= MinBins).ToList();

        var profiles = (await c.QueryAsync<Profile>(@"SELECT sensor_id AS SensorId, baseline_offset AS BaselineOffset, cusum_positive AS CusumPositive,
                cusum_negative AS CusumNegative, last_evaluated_at AS LastEvaluatedAt FROM drift_profiles WHERE metric_type=@metric AND sensor_id IN @ids",
            new { metric, ids })).ToDictionary(p => p.SensorId);

        foreach (var s in group)
        {
            if (!profiles.ContainsKey(s.Id))
            {
                await c.ExecuteAsync("INSERT INTO drift_profiles(sensor_id, metric_type) VALUES (@Id, @metric)", new { s.Id, metric });
                profiles[s.Id] = new Profile { SensorId = s.Id };
            }

            // UC05 2a: không đủ cảm biến tham chiếu -> NO_REFERENCE, không suy diễn
            if (active.Count < 2 || !active.Contains(s))
            {
                await SaveAsync(c, s, metric, "NO_REFERENCE", 0, 0, profiles[s.Id].CusumPositive, profiles[s.Id].CusumNegative, false, 0, now);
                continue;
            }

            var mine = bySensor[s.Id];
            var points = new List<(double Hours, double Dev)>();
            foreach (var (bin, value) in mine.OrderBy(k => k.Key))
            {
                var peers = active.Where(a => a.Id != s.Id && bySensor[a.Id].ContainsKey(bin)).Select(a => bySensor[a.Id][bin]).ToList();
                if (peers.Count == 0) continue;
                points.Add((bin * BinMinutes / 60.0, value - Median(peers)));
            }
            if (points.Count < MinBins)
            {
                await SaveAsync(c, s, metric, "NO_REFERENCE", 0, 0, profiles[s.Id].CusumPositive, profiles[s.Id].CusumNegative, false, points.Count, now);
                continue;
            }

            var rate = Slope(points);
            var meanDev = points.Average(p => p.Dev);

            // CUSUM tăng dần: chỉ cộng các bin mới kể từ lần đánh giá trước (lần đầu: 1 giờ gần nhất)
            var prof = profiles[s.Id];
            var since = prof.LastEvaluatedAt ?? now.AddHours(-1);
            var sinceHours = (since - new DateTime(2000, 1, 1)).TotalHours;
            double sp = (double)prof.CusumPositive, sn = (double)prof.CusumNegative, baseline = (double)prof.BaselineOffset;
            foreach (var p in points.Where(p => p.Hours > sinceHours))
            {
                sp = Math.Max(0, sp + (p.Dev - baseline) - K);
                sn = Math.Max(0, sn - (p.Dev - baseline) - K);
            }

            var severity = Rules.DriftSeverity(metric, Math.Abs(rate));
            var requiresCal = severity == "CRITICAL" || Math.Max(sp, sn) > H;
            await SaveAsync(c, s, metric, severity, rate, meanDev, (decimal)sp, (decimal)sn, requiresCal, points.Count, now);

            if (severity != "NONE")
            {
                var unit = metric == "TEMPERATURE" ? "°C/h" : "%RH/h";
                await alerts.RaiseAsync(c, zoneId, s.Id, "SENSOR_DRIFT", severity == "CRITICAL" ? "CRITICAL" : "WARNING",
                    $"{s.HardwareId}: trôi {(metric == "TEMPERATURE" ? "nhiệt độ" : "độ ẩm")} {rate:+0.000;-0.000} {unit} so với cảm biến cùng zone (lệch TB {meanDev:+0.00;-0.00}) — {severity}",
                    (decimal)Math.Round(rate, 3));
            }
        }
    }

    private async Task SaveAsync(SqlConnection c, SensorRow s, string metric, string severity, double rate, double meanDev,
        decimal sp, decimal sn, bool requiresCal, int samples, DateTime now)
    {
        rate = Math.Clamp(Math.Round(rate, 3), -999, 999);
        meanDev = Math.Clamp(Math.Round(meanDev, 3), -999, 999);
        sp = Math.Min(sp, 999999m); sn = Math.Min(sn, 999999m);
        await c.ExecuteAsync(@"UPDATE drift_profiles SET drift_severity=@severity, drift_rate_per_hour=@rate, cusum_positive=@sp, cusum_negative=@sn,
                requires_calibration=@requiresCal, last_evaluated_at=@now WHERE sensor_id=@Id AND metric_type=@metric;
            INSERT INTO drift_history(sensor_id, metric_type, evaluated_at, mean_deviation, cusum_positive, cusum_negative, drift_rate_per_hour, drift_severity, sample_count)
            VALUES (@Id, @metric, @now, @meanDev, @sp, @sn, @rate, @severity, @samples);",
            new { s.Id, metric, severity, rate, meanDev, sp, sn, requiresCal, now, samples });
        await notifier.Drift(new { sensorId = s.Id, metric, severity, rate, requiresCalibration = requiresCal });
    }

    private static double Median(List<double> v)
    {
        v.Sort();
        return v.Count % 2 == 1 ? v[v.Count / 2] : (v[v.Count / 2 - 1] + v[v.Count / 2]) / 2;
    }

    private static double Slope(List<(double X, double Y)> pts)
    {
        var mx = pts.Average(p => p.X);
        var my = pts.Average(p => p.Y);
        var num = pts.Sum(p => (p.X - mx) * (p.Y - my));
        var den = pts.Sum(p => (p.X - mx) * (p.X - mx));
        return den == 0 ? 0 : num / den;
    }
}

/// <summary>Thay thế continuous aggregate + retention policy của TimescaleDB (SQL Express không có SQL Agent).</summary>
public sealed class AggregationService(Db db, IConfiguration cfg, ILogger<AggregationService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await Task.Delay(TimeSpan.FromSeconds(10), ct);
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(cfg.GetValue("Aggregation:IntervalMinutes", 5)));
        var lastRetention = DateTime.MinValue;
        do
        {
            try
            {
                await RefreshAsync(DateTime.UtcNow.AddHours(-3), DateTime.UtcNow.AddHours(1));
                if (DateTime.UtcNow - lastRetention > TimeSpan.FromDays(1))
                {
                    await using var c = await db.OpenAsync();
                    await c.ExecuteAsync("EXEC sp_apply_retention @raw_days=@d", new { d = cfg.GetValue("Retention:RawDays", 1825) });
                    lastRetention = DateTime.UtcNow;
                }
            }
            catch (Exception ex) { log.LogError(ex, "Aggregation lỗi"); }
        } while (await timer.WaitForNextTickAsync(ct));
    }

    public async Task RefreshAsync(DateTime from, DateTime to)
    {
        await using var c = await db.OpenAsync();
        await c.ExecuteAsync("EXEC sp_refresh_readings_hourly @from, @to", new { from, to }, commandTimeout: 300);
    }
}
