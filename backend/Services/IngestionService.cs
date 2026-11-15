using System.Globalization;
using System.Text.Json;
using Dapper;
using Iot04.Api.Domain;
using Iot04.Api.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Iot04.Api.Services;

public enum IngestResult { Accepted, AcceptedAnomalous, Duplicate, Malformed, UnknownSensor, Rejected }

public sealed record ReadingPayload(DateTime? Ts, decimal? Temperature, decimal? Humidity, int? Battery, int? Rssi);

/// <summary>
/// UC03 — Ingestion pipeline: parse -> validate -> gắn nhãn quality -> ghi readings ->
/// cập nhật heartbeat / đóng DataGap -> đánh giá ngưỡng -> đẩy WebSocket.
/// </summary>
public sealed class IngestionService(Db db, AlertService alerts, Notifier notifier, IngestionMetrics metrics, SimulatorState simState, ILogger<IngestionService> log)
{
    private sealed class SensorCtx
    {
        public Guid Id { get; set; }
        public string HardwareId { get; set; } = "";
        public Guid? ZoneId { get; set; }
        public string Status { get; set; } = "";
        public string ConnectionState { get; set; } = "";
        public DateTime? LastReadingAt { get; set; }
        public decimal? TempMin { get; set; }
        public decimal? TempMax { get; set; }
        public decimal? HumidityMin { get; set; }
        public decimal? HumidityMax { get; set; }
        public string? ZoneName { get; set; }
        public string? TempDrift { get; set; }
        public string? HumDrift { get; set; }
    }

    public static ReadingPayload Parse(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var r = doc.RootElement;
        if (r.ValueKind != JsonValueKind.Object) throw new FormatException("Payload phải là JSON object");

        decimal? Dec(string name) => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDecimal() : null;
        int? Int(string name) => r.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.Number ? (int)Math.Round(v.GetDouble()) : null;

        DateTime? ts = null;
        if (r.TryGetProperty("ts", out var t) && t.ValueKind == JsonValueKind.String)
        {
            if (!DateTime.TryParse(t.GetString(), CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var parsed))
                throw new FormatException("Trường ts không đúng định dạng ISO-8601");
            ts = parsed;
        }
        return new ReadingPayload(ts, Dec("temperature"), Dec("humidity"), Int("battery"), Int("rssi"));
    }

    public async Task<IngestResult> ProcessAsync(string hardwareId, string json, string source)
    {
        ReadingPayload p;
        try { p = Parse(json); }
        catch (Exception ex)
        {
            metrics.Reject("MALFORMED");
            log.LogError("[{Source}] Bản tin sai định dạng từ {Hw}: {Error}", source, hardwareId, ex.Message);
            return IngestResult.Malformed;
        }
        return await ProcessAsync(hardwareId, p, source);
    }

    public async Task<IngestResult> ProcessAsync(string hardwareId, ReadingPayload p, string source)
    {
        if (p.Temperature is null && p.Humidity is null)
        {
            metrics.Reject("MISSING_FIELDS");
            log.LogError("[{Source}] Bản tin thiếu giá trị đo từ {Hw}", source, hardwareId);
            return IngestResult.Malformed;
        }
        // Giá trị không thể lưu vào DECIMAL(5,2) -> coi như hỏng
        if (p.Temperature is { } tv && Math.Abs(tv) >= 1000 || p.Humidity is { } hv && Math.Abs(hv) >= 1000)
        {
            metrics.Reject("OUT_OF_STORAGE_RANGE");
            return IngestResult.Rejected;
        }

        // Hỗ trợ tự động ánh xạ alias nếu người dùng gõ SN-COLD-03-A thay vì SN-COLD-03-C
        if (hardwareId.Equals("SN-COLD-03-A", StringComparison.OrdinalIgnoreCase)) hardwareId = "SN-COLD-03-C";

        await using var c = await db.OpenAsync();
        var s = await c.QueryFirstOrDefaultAsync<SensorCtx>(@"
            SELECT s.id, s.hardware_id AS HardwareId, s.zone_id AS ZoneId, s.status, s.connection_state AS ConnectionState,
                   s.last_reading_at AS LastReadingAt, z.temp_min AS TempMin, z.temp_max AS TempMax,
                   z.humidity_min AS HumidityMin, z.humidity_max AS HumidityMax, z.name AS ZoneName,
                   (SELECT drift_severity FROM drift_profiles WHERE sensor_id=s.id AND metric_type='TEMPERATURE') AS TempDrift,
                   (SELECT drift_severity FROM drift_profiles WHERE sensor_id=s.id AND metric_type='HUMIDITY') AS HumDrift
            FROM sensors s LEFT JOIN zones z ON z.id = s.zone_id
            WHERE s.hardware_id = @hardwareId", new { hardwareId });

        if (s is null)
        {
            metrics.Reject("UNKNOWN_SENSOR");
            log.LogWarning("[{Source}] Từ chối bản tin từ cảm biến chưa đăng ký: {Hw}", source, hardwareId);
            return IngestResult.UnknownSensor;
        }
        if (s.Status == "DECOMMISSIONED")
        {
            metrics.Reject("DECOMMISSIONED");
            return IngestResult.Rejected;
        }

        if (source.Contains("WOKWI") && simState.Sensors.TryGetValue(hardwareId, out var sim))
        {
            sim.ForcedOffline = true;
        }

        var now = DateTime.UtcNow;
        var ts = p.Ts ?? now;
        if (ts > now.AddMinutes(5))
        {
            metrics.Reject("FUTURE_TIMESTAMP");
            log.LogWarning("[{Source}] {Hw} gửi timestamp tương lai {Ts} — từ chối (đồng hồ thiết bị sai)", source, hardwareId, ts);
            return IngestResult.Rejected;
        }

        var physicalOk =
            (p.Temperature is null || p.Temperature is >= Rules.TempPhysicalMin and <= Rules.TempPhysicalMax) &&
            (p.Humidity is null || p.Humidity is >= Rules.HumidityPhysicalMin and <= Rules.HumidityPhysicalMax);
        var batteryValid = p.Battery is >= 0 and <= 100;

        static bool Suspect(string? sev) => sev is "MODERATE" or "CRITICAL";
        var quality = !physicalOk ? "ANOMALOUS_REJECT"
            : (p.Temperature is not null && Suspect(s.TempDrift)) || (p.Humidity is not null && Suspect(s.HumDrift)) || s.Status == "CALIBRATING"
                ? "SUSPECT_DRIFT" : "GOOD";

        try
        {
            await c.ExecuteAsync(@"INSERT INTO readings(sensor_id, reading_time, zone_id, temperature, humidity, battery_level, rssi, quality)
                                   VALUES (@Id, @ts, @ZoneId, @temp, @hum, @bat, @rssi, @quality)",
                new { s.Id, ts, s.ZoneId, temp = p.Temperature, hum = p.Humidity, bat = batteryValid ? p.Battery : null, rssi = p.Rssi, quality });
        }
        catch (SqlException ex) when (ex.Number is 2627 or 2601)
        {
            metrics.Duplicate();   // MQTT QoS1 có thể gửi lặp -> bỏ qua an toàn
            return IngestResult.Duplicate;
        }

        if (quality == "ANOMALOUS_REJECT")
            log.LogWarning("[{Source}] {Hw} giá trị phi vật lý T={T} H={H} -> ANOMALOUS_REJECT (không tính thống kê)", source, hardwareId, p.Temperature, p.Humidity);

        // Cập nhật heartbeat chỉ khi đây là bản tin mới nhất (bản tin đến trễ không làm "trẻ hóa" dữ liệu)
        var isLatest = s.LastReadingAt is null || ts > s.LastReadingAt;
        var gapClosed = 0;
        if (isLatest)
        {
            await c.ExecuteAsync(@"UPDATE sensors SET last_reading_at=@ts, connection_state='CONNECTED',
                    battery_level=COALESCE(@bat, battery_level), last_rssi=COALESCE(@rssi, last_rssi),
                    status=CASE WHEN status='OFFLINE' THEN 'ONLINE' ELSE status END
                WHERE id=@Id", new { s.Id, ts, bat = batteryValid ? p.Battery : null, rssi = p.Rssi });

            // UC04 5a / BR07: đóng DataGap đang mở
            gapClosed = await c.ExecuteAsync("UPDATE data_gaps SET end_time=@ts WHERE sensor_id=@Id AND end_time IS NULL AND start_time <= @ts",
                new { s.Id, ts });

            if (s.ConnectionState != "CONNECTED")
            {
                await alerts.AutoResolveAsync(c, s.Id, "SENSOR_STALE", "Tự động: cảm biến đã gửi dữ liệu trở lại");
                await notifier.SensorState(new { sensorId = s.Id, hardwareId, zoneId = s.ZoneId, from = s.ConnectionState, to = "CONNECTED", gapClosed = gapClosed > 0, at = ts });
                log.LogInformation("{Hw}: {From} -> CONNECTED{Gap}", hardwareId, s.ConnectionState, gapClosed > 0 ? " (đóng DataGap)" : "");
            }
        }

        // BR12/BR13: đánh giá ngưỡng zone
        if (quality != "ANOMALOUS_REJECT" && s.ZoneId is { } zoneId && s.TempMin is not null)
        {
            var breached = false;
            if (p.Temperature is { } t && Rules.BoundsSeverity(t, s.TempMin!.Value, s.TempMax!.Value) is { } sevT)
            {
                breached = true;
                var limit = t > s.TempMax ? s.TempMax : s.TempMin;
                await alerts.RaiseAsync(c, zoneId, s.Id, "TEMP_OUT_OF_BOUNDS", sevT,
                    $"{hardwareId} ({s.ZoneName}): nhiệt độ {t:0.0}°C ngoài ngưỡng [{s.TempMin:0.0}; {s.TempMax:0.0}]°C", t, limit);
            }
            if (p.Humidity is { } h && Rules.BoundsSeverity(h, s.HumidityMin!.Value, s.HumidityMax!.Value) is { } sevH)
            {
                breached = true;
                var limit = h > s.HumidityMax ? s.HumidityMax : s.HumidityMin;
                await alerts.RaiseAsync(c, zoneId, s.Id, "HUMIDITY_OUT_OF_BOUNDS", sevH,
                    $"{hardwareId} ({s.ZoneName}): độ ẩm {h:0.0}% ngoài ngưỡng [{s.HumidityMin:0}; {s.HumidityMax:0}]%", h, limit);
            }
            if (breached) await CheckZoneEmergencyAsync(c, zoneId, s.ZoneName);
        }

        if (batteryValid && p.Battery < Rules.LowBatteryPercent)
            await alerts.RaiseAsync(c, s.ZoneId, s.Id, "LOW_BATTERY", "INFO", $"{hardwareId}: pin yếu ({p.Battery}%)", p.Battery);

        metrics.Accept();
        await notifier.Reading(new
        {
            sensorId = s.Id, hardwareId, zoneId = s.ZoneId, time = ts,
            temperature = p.Temperature, humidity = p.Humidity, battery = batteryValid ? p.Battery : null, rssi = p.Rssi, quality
        });
        return quality == "ANOMALOUS_REJECT" ? IngestResult.AcceptedAnomalous : IngestResult.Accepted;
    }

    /// <summary>BR13 — EMERGENCY khi &gt; 50% cảm biến đang hoạt động trong zone cùng vi phạm.</summary>
    private async Task CheckZoneEmergencyAsync(SqlConnection c, Guid zoneId, string? zoneName)
    {
        var r = await c.QueryFirstAsync<(int Total, int Breaching)>(@"
            SELECT COUNT(*) AS Total,
                   COALESCE(SUM(CASE WHEN x.temperature < z.temp_min OR x.temperature > z.temp_max
                                       OR x.humidity < z.humidity_min OR x.humidity > z.humidity_max THEN 1 ELSE 0 END), 0) AS Breaching
            FROM sensors s
            JOIN zones z ON z.id = s.zone_id
            CROSS APPLY (SELECT TOP 1 temperature, humidity FROM readings
                         WHERE sensor_id = s.id AND quality <> 'ANOMALOUS_REJECT'
                           AND reading_time > DATEADD(SECOND, -s.stale_threshold_sec, SYSUTCDATETIME())
                         ORDER BY reading_time DESC) x
            WHERE s.zone_id = @zoneId AND s.status <> 'DECOMMISSIONED'", new { zoneId });

        if (r.Total >= 2 && r.Breaching > r.Total * Rules.EmergencyZoneRatio)
            await alerts.RaiseAsync(c, zoneId, null, "ZONE_EMERGENCY", "EMERGENCY",
                $"KHẨN CẤP: {r.Breaching}/{r.Total} cảm biến tại {zoneName} đồng loạt vi phạm ngưỡng", r.Breaching);
    }
}
