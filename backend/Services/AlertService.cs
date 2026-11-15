using Dapper;
using Iot04.Api.Domain;
using Iot04.Api.Infrastructure;
using Microsoft.Data.SqlClient;

namespace Iot04.Api.Services;

/// <summary>
/// AlertEngine (BR12, BR13). Chống bão cảnh báo: mỗi (sensor, loại) chỉ có 1 alert mở;
/// lần vi phạm lặp lại chỉ tăng occurrence_count, cập nhật last_seen_at và nâng mức độ nếu nặng hơn.
/// </summary>
public sealed class AlertService(Notifier notifier, ILogger<AlertService> log)
{
    private sealed class OpenAlert
    {
        public Guid Id { get; set; }
        public string Severity { get; set; } = "";
    }

    public async Task RaiseAsync(SqlConnection c, Guid? zoneId, Guid? sensorId, string type, string severity,
        string message, decimal? metricValue = null, decimal? thresholdValue = null)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var existing = await c.QueryFirstOrDefaultAsync<OpenAlert>(
                sensorId is not null
                    ? "SELECT id, severity FROM alerts WHERE sensor_id=@sensorId AND alert_type=@type AND status IN ('ACTIVE','ACKNOWLEDGED')"
                    : "SELECT id, severity FROM alerts WHERE sensor_id IS NULL AND zone_id=@zoneId AND alert_type=@type AND status IN ('ACTIVE','ACKNOWLEDGED')",
                new { sensorId, zoneId, type });

            if (existing is { } e)
            {
                var escalate = Rules.Rank(severity) > Rules.Rank(e.Severity);
                await c.ExecuteAsync(@"UPDATE alerts SET last_seen_at=SYSUTCDATETIME(), occurrence_count=occurrence_count+1,
                        metric_value=COALESCE(@metricValue, metric_value),
                        severity=CASE WHEN @escalate=1 THEN @severity ELSE severity END,
                        message=CASE WHEN @escalate=1 THEN @message ELSE message END,
                        status=CASE WHEN @escalate=1 THEN 'ACTIVE' ELSE status END
                    WHERE id=@id", new { id = e.Id, metricValue, escalate, severity, message });
                if (escalate) await notifier.Alert(new { id = e.Id, zoneId, sensorId, alertType = type, severity, message, escalated = true });
                return;
            }

            try
            {
                var id = await c.ExecuteScalarAsync<Guid>(@"INSERT INTO alerts(zone_id, sensor_id, alert_type, severity, message, metric_value, threshold_value)
                    OUTPUT INSERTED.id VALUES (@zoneId, @sensorId, @type, @severity, @message, @metricValue, @thresholdValue)",
                    new { zoneId, sensorId, type, severity, message, metricValue, thresholdValue });
                log.LogWarning("ALERT {Severity} {Type}: {Message}", severity, type, message);
                await notifier.Alert(new { id, zoneId, sensorId, alertType = type, severity, message, createdAt = DateTime.UtcNow });
                return;
            }
            catch (SqlException ex) when (ex.Number is 2601 or 2627)
            {
                // Luồng khác vừa tạo alert cùng loại -> thử lại nhánh cập nhật
            }
        }
    }

    /// <summary>Tự đóng các alert mức INFO đã hết hiệu lực (vd. SENSOR_STALE khi cảm biến kết nối lại).</summary>
    public async Task AutoResolveAsync(SqlConnection c, Guid sensorId, string type, string note) =>
        await c.ExecuteAsync(@"UPDATE alerts SET status='RESOLVED', resolved_at=SYSUTCDATETIME(), resolution_notes=@note
                               WHERE sensor_id=@sensorId AND alert_type=@type AND status IN ('ACTIVE','ACKNOWLEDGED')",
            new { sensorId, type, note });
}
