using System.Globalization;
using System.Text.Json;
using Dapper;
using Iot04.Api.Domain;
using Iot04.Api.Infrastructure;
using Iot04.Api.Services;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Data.SqlClient;

var builder = WebApplication.CreateBuilder(args);

// JSON formatting
builder.Services.Configure<JsonOptions>(opt =>
{
    opt.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opt.SerializerOptions.Converters.Add(new UtcDateTimeConverter());
});

// DI Services
builder.Services.AddSingleton<Db>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddSingleton<IngestionMetrics>();
builder.Services.AddSingleton<Notifier>();
builder.Services.AddSingleton<AlertService>();
builder.Services.AddSingleton<SimulatorState>();
builder.Services.AddSingleton<IngestionService>();

// Background Services
builder.Services.AddSingleton<MqttBrokerService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<MqttBrokerService>());
// builder.Services.AddHostedService<SensorSimulator>(); // Đã tắt hoàn toàn bộ giả lập, 100% dữ liệu từ Wokwi / thiết bị thật
builder.Services.AddHostedService<ConnectionMonitor>();
builder.Services.AddHostedService<DriftDetectionService>();
builder.Services.AddHostedService<AggregationService>();
builder.Services.AddHostedService<ExternalMqttBridgeService>();

// SignalR & CORS
builder.Services.AddSignalR();
builder.Services.AddCors(c => c.AddDefaultPolicy(p => p
    .SetIsOriginAllowed(_ => true)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

var app = builder.Build();

app.UseCors();

// Auth Middleware: extract Bearer token from header or query (for SignalR)
app.Use(async (ctx, next) =>
{
    var token = ctx.Request.Headers.Authorization.ToString().Replace("Bearer ", "").Trim();
    if (string.IsNullOrEmpty(token) && ctx.Request.Query.TryGetValue("access_token", out var q))
        token = q.ToString();

    if (!string.IsNullOrEmpty(token))
    {
        var tokens = ctx.RequestServices.GetRequiredService<TokenService>();
        ctx.Items["user"] = tokens.Validate(token);
    }
    await next();
});

// Startup Password Initialization: Update seed placeholder hashes with PBKDF2
try
{
    var db = app.Services.GetRequiredService<Db>();
    await using var c = await db.OpenAsync();
    var defaultHash = Passwords.Hash("123456");
    await c.ExecuteAsync("UPDATE users SET password_hash = @defaultHash WHERE password_hash = 'RESET_ON_STARTUP'", new { defaultHash });
}
catch (Exception ex)
{
    app.Logger.LogWarning("Không thể cập nhật mật khẩu khởi động: {Msg}", ex.Message);
}

// SignalR Hub
app.MapHub<MonitorHub>("/hubs/monitor");

// =============================================================================
// AUTH ENDPOINTS
// =============================================================================
app.MapPost("/api/auth/login", async (Db db, TokenService tokens, LoginDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Username) || string.IsNullOrWhiteSpace(dto.Password))
        return Results.BadRequest(new { error = "Tên đăng nhập và mật khẩu không được để trống" });

    await using var c = await db.OpenAsync();
    var user = await c.QueryFirstOrDefaultAsync<UserRecord>(
        "SELECT id, username, password_hash AS PasswordHash, full_name AS FullName, email, role, is_active AS IsActive FROM users WHERE username = @u",
        new { u = dto.Username.Trim() });

    if (user is null || !user.IsActive || !Passwords.Verify(dto.Password, user.PasswordHash))
        return Results.Json(new { error = "Tên đăng nhập hoặc mật khẩu không chính xác" }, statusCode: 401);

    await c.ExecuteAsync("UPDATE users SET last_login_at = SYSUTCDATETIME() WHERE id = @id", new { id = user.Id });
    await c.Audit(user.Id, "LOGIN", "users", user.Id, new { user.Username, user.Role });

    var authUser = new AuthUser(user.Id, user.Username, user.FullName, user.Role);
    var token = tokens.Issue(authUser);

    return Results.Ok(new
    {
        token,
        user = new { id = user.Id, username = user.Username, fullName = user.FullName, email = user.Email, role = user.Role }
    });
});

app.MapGet("/api/auth/me", (HttpContext ctx) =>
{
    var u = ctx.CurrentUser();
    return u is null ? Results.Json(new { error = "Chưa đăng nhập" }, statusCode: 401) : Results.Ok(u);
});

// =============================================================================
// WAREHOUSE & ZONE ENDPOINTS (UC01)
// =============================================================================
app.MapGet("/api/warehouses", async (Db db) =>
{
    await using var c = await db.OpenAsync();
    var whs = (await c.QueryAsync<WarehouseItem>("SELECT id, code, name, address, capacity_m2 AS CapacityM2, description, is_active AS IsActive FROM warehouses WHERE is_active = 1")).ToList();
    var zones = (await c.QueryAsync<ZoneSummary>(@"
        SELECT z.id, z.warehouse_id AS WarehouseId, z.code, z.name, z.zone_type AS ZoneType,
               z.temp_min AS TempMin, z.temp_max AS TempMax, z.humidity_min AS HumidityMin, z.humidity_max AS HumidityMax,
               (SELECT COUNT(*) FROM sensors s WHERE s.zone_id = z.id AND s.status <> 'DECOMMISSIONED') AS SensorCount,
               (SELECT COUNT(*) FROM sensors s WHERE s.zone_id = z.id AND s.connection_state = 'CONNECTED') AS ConnectedSensorCount
        FROM zones z WHERE z.is_active = 1")).ToList();

    var result = whs.Select(w => new
    {
        w.Id, w.Code, w.Name, w.Address, w.CapacityM2, w.Description,
        Zones = zones.Where(z => z.WarehouseId == w.Id).ToList()
    });
    return Results.Ok(result);
});

app.MapGet("/api/zones/overview", async (Db db) =>
{
    await using var c = await db.OpenAsync();
    var zones = (await c.QueryAsync<ZoneOverviewDto>(@"
        SELECT z.id, z.warehouse_id AS WarehouseId, w.name AS WarehouseName, z.code, z.name, z.zone_type AS ZoneType,
               z.temp_min AS TempMin, z.temp_max AS TempMax, z.humidity_min AS HumidityMin, z.humidity_max AS HumidityMax,
               (SELECT COUNT(*) FROM sensors s WHERE s.zone_id = z.id AND s.status <> 'DECOMMISSIONED') AS TotalSensors,
               (SELECT COUNT(*) FROM sensors s WHERE s.zone_id = z.id AND s.connection_state = 'CONNECTED') AS OnlineSensors,
               (SELECT COUNT(*) FROM alerts a WHERE a.zone_id = z.id AND a.status = 'ACTIVE') AS ActiveAlerts
        FROM zones z
        JOIN warehouses w ON w.id = z.warehouse_id
        WHERE z.is_active = 1")).ToList();

    // Lấy thông số nhiệt/ẩm mới nhất của từng zone
    var latest = (await c.QueryAsync<LatestZoneTelemetry>(@"
        SELECT s.zone_id AS ZoneId,
               AVG(r.temperature) AS TempAvg,
               AVG(r.humidity) AS HumidityAvg,
               MAX(r.reading_time) AS LastReadingAt
        FROM sensors s
        CROSS APPLY (
            SELECT TOP 1 temperature, humidity, reading_time
            FROM readings
            WHERE sensor_id = s.id AND quality <> 'ANOMALOUS_REJECT'
            ORDER BY reading_time DESC
        ) r
        WHERE s.zone_id IS NOT NULL AND s.status <> 'DECOMMISSIONED'
        GROUP BY s.zone_id")).ToDictionary(x => x.ZoneId);

    var now = DateTime.UtcNow;
    var list = zones.Select(z =>
    {
        latest.TryGetValue(z.Id, out var lat);
        var lastTs = lat?.LastReadingAt;
        var ageSec = lastTs.HasValue ? (now - lastTs.Value).TotalSeconds : double.MaxValue;
        var ageStatus = lastTs.HasValue ? Rules.DataAge(TimeSpan.FromSeconds(ageSec)) : "EXPIRED";

        return new
        {
            z.Id, z.WarehouseId, z.WarehouseName, z.Code, z.Name, z.ZoneType,
            z.TempMin, z.TempMax, z.HumidityMin, z.HumidityMax,
            z.TotalSensors, z.OnlineSensors, z.ActiveAlerts,
            CurrentTemp = lat?.TempAvg,
            CurrentHumidity = lat?.HumidityAvg,
            LastReadingAt = lastTs,
            DataAgeSeconds = lastTs.HasValue ? (long)ageSec : (long?)null,
            DataAgeStatus = ageStatus
        };
    });

    return Results.Ok(list);
});

app.MapGet("/api/zones/{id:guid}", async (Db db, Guid id) =>
{
    await using var c = await db.OpenAsync();
    var zone = await c.QueryFirstOrDefaultAsync<ZoneDetailDto>(@"
        SELECT z.id, z.warehouse_id AS WarehouseId, w.name AS WarehouseName, z.code, z.name, z.zone_type AS ZoneType,
               z.temp_min AS TempMin, z.temp_max AS TempMax, z.humidity_min AS HumidityMin, z.humidity_max AS HumidityMax,
               z.created_at AS CreatedAt
        FROM zones z JOIN warehouses w ON w.id = z.warehouse_id WHERE z.id = @id", new { id });
    if (zone is null) return Results.NotFound(new { error = "Không tìm thấy phân vùng" });

    var sensors = (await c.QueryAsync<SensorInZoneDto>(@"
        SELECT s.id, s.hardware_id AS HardwareId, s.sensor_type AS SensorType, s.connection_state AS ConnectionState,
               s.status, s.battery_level AS BatteryLevel, s.last_rssi AS LastRssi, s.expected_interval_sec AS ExpectedIntervalSec,
               s.last_reading_at AS LastReadingAt,
               (SELECT TOP 1 temperature FROM readings WHERE sensor_id = s.id AND quality <> 'ANOMALOUS_REJECT' ORDER BY reading_time DESC) AS LatestTemp,
               (SELECT TOP 1 humidity FROM readings WHERE sensor_id = s.id AND quality <> 'ANOMALOUS_REJECT' ORDER BY reading_time DESC) AS LatestHumidity,
               (SELECT drift_rate_per_hour FROM drift_profiles WHERE sensor_id = s.id AND metric_type = 'TEMPERATURE') AS TempDriftRate,
               (SELECT drift_severity FROM drift_profiles WHERE sensor_id = s.id AND metric_type = 'TEMPERATURE') AS TempDriftSeverity
        FROM sensors s
        WHERE s.zone_id = @id AND s.status <> 'DECOMMISSIONED'", new { id })).ToList();

    return Results.Ok(new { Zone = zone, Sensors = sensors });
});

app.MapPut("/api/zones/{id:guid}/thresholds", async (Db db, HttpContext ctx, Guid id, UpdateThresholdDto dto) =>
{
    if (dto.TempMin >= dto.TempMax) return Results.BadRequest(new { error = "Ngưỡng nhiệt min phải nhỏ hơn max (BR02)" });
    if (dto.HumidityMin >= dto.HumidityMax || dto.HumidityMin < 0 || dto.HumidityMax > 100)
        return Results.BadRequest(new { error = "Ngưỡng độ ẩm không hợp lệ [0-100] và min < max (BR02)" });

    var user = ctx.CurrentUser();
    await using var c = await db.OpenAsync();
    await using var tx = c.BeginTransaction();

    await c.ExecuteAsync(@"
        UPDATE zone_threshold_history SET effective_to = SYSUTCDATETIME() WHERE zone_id = @id AND effective_to IS NULL;
        INSERT INTO zone_threshold_history(zone_id, temp_min, temp_max, humidity_min, humidity_max, changed_by)
        VALUES (@id, @TempMin, @TempMax, @HumidityMin, @HumidityMax, @userId);
        UPDATE zones SET temp_min = @TempMin, temp_max = @TempMax, humidity_min = @HumidityMin, humidity_max = @HumidityMax WHERE id = @id;",
        new { id, dto.TempMin, dto.TempMax, dto.HumidityMin, dto.HumidityMax, userId = user?.Id }, tx);

    await c.Audit(user?.Id, "UPDATE_THRESHOLDS", "zones", id, dto, tx);
    await tx.CommitAsync();

    return Results.Ok(new { success = true });
}).Roles("ADMIN", "WAREHOUSE_MANAGER");

// =============================================================================
// SENSOR MANAGEMENT ENDPOINTS (UC02)
// =============================================================================
app.MapGet("/api/sensors", async (Db db) =>
{
    await using var c = await db.OpenAsync();
    var list = (await c.QueryAsync<SensorListItemDto>(@"
        SELECT s.id, s.hardware_id AS HardwareId, s.sensor_type AS SensorType, s.expected_interval_sec AS ExpectedIntervalSec,
               s.stale_threshold_sec AS StaleThresholdSec, s.disconnect_threshold_sec AS DisconnectThresholdSec,
               s.firmware_version AS FirmwareVersion, s.battery_level AS BatteryLevel, s.last_rssi AS LastRssi,
               s.connection_state AS ConnectionState, s.status, s.last_reading_at AS LastReadingAt,
               s.last_calibrated_at AS LastCalibratedAt, s.next_calibration_due AS NextCalibrationDue,
               z.id AS ZoneId, z.name AS ZoneName, w.name AS WarehouseName,
               dpT.drift_rate_per_hour AS TempDriftRate, dpT.drift_severity AS TempDriftSeverity
        FROM sensors s
        LEFT JOIN zones z ON z.id = s.zone_id
        LEFT JOIN warehouses w ON w.id = z.warehouse_id
        LEFT JOIN drift_profiles dpT ON dpT.sensor_id = s.id AND dpT.metric_type = 'TEMPERATURE'
        WHERE s.status <> 'DECOMMISSIONED'
        ORDER BY s.hardware_id")).ToList();

    var now = DateTime.UtcNow;
    var result = list.Select(s =>
    {
        var ageSec = s.LastReadingAt.HasValue ? (now - s.LastReadingAt.Value).TotalSeconds : double.MaxValue;
        return new
        {
            s.Id, s.HardwareId, s.SensorType, s.ExpectedIntervalSec, s.StaleThresholdSec, s.DisconnectThresholdSec,
            s.FirmwareVersion, s.BatteryLevel, s.LastRssi, s.ConnectionState, s.Status,
            s.LastReadingAt, s.LastCalibratedAt, s.NextCalibrationDue,
            s.ZoneId, s.ZoneName, s.WarehouseName,
            s.TempDriftRate, s.TempDriftSeverity,
            DataAgeSeconds = s.LastReadingAt.HasValue ? (long)ageSec : (long?)null,
            DataAgeStatus = s.LastReadingAt.HasValue ? Rules.DataAge(TimeSpan.FromSeconds(ageSec)) : "EXPIRED"
        };
    });

    return Results.Ok(result);
});

app.MapGet("/api/sensors/{id:guid}", async (Db db, Guid id) =>
{
    await using var c = await db.OpenAsync();
    var sensor = await c.QueryFirstOrDefaultAsync<SensorDetailFullDto>(@"
        SELECT s.id, s.hardware_id AS HardwareId, s.sensor_type AS SensorType, s.expected_interval_sec AS ExpectedIntervalSec,
               s.stale_threshold_sec AS StaleThresholdSec, s.disconnect_threshold_sec AS DisconnectThresholdSec,
               s.firmware_version AS FirmwareVersion, s.battery_level AS BatteryLevel, s.last_rssi AS LastRssi,
               s.connection_state AS ConnectionState, s.status, s.calibration_offset AS CalibrationOffset,
               s.last_reading_at AS LastReadingAt, s.last_calibrated_at AS LastCalibratedAt, s.next_calibration_due AS NextCalibrationDue,
               z.id AS ZoneId, z.name AS ZoneName, w.id AS WarehouseId, w.name AS WarehouseName
        FROM sensors s
        LEFT JOIN zones z ON z.id = s.zone_id
        LEFT JOIN warehouses w ON w.id = z.warehouse_id
        WHERE s.id = @id", new { id });
    if (sensor is null) return Results.NotFound(new { error = "Không tìm thấy cảm biến" });

    var driftProfiles = (await c.QueryAsync<DriftProfileDto>(@"
        SELECT metric_type AS MetricType, baseline_offset AS BaselineOffset, cusum_positive AS CusumPositive,
               cusum_negative AS CusumNegative, drift_rate_per_hour AS DriftRatePerHour, drift_severity AS DriftSeverity,
               requires_calibration AS RequiresCalibration, last_evaluated_at AS LastEvaluatedAt
        FROM drift_profiles WHERE sensor_id = @id", new { id })).ToList();

    var gaps = (await c.QueryAsync<DataGapDto>(@"
        SELECT TOP 20 id, start_time AS StartTime, end_time AS EndTime, duration_sec AS DurationSec, reason, created_at AS CreatedAt
        FROM data_gaps WHERE sensor_id = @id ORDER BY start_time DESC", new { id })).ToList();

    var calibrations = (await c.QueryAsync<CalibrationRecordDto>(@"
        SELECT cr.id, cr.calibration_date AS CalibrationDate, cr.old_offset AS OldOffset, cr.new_offset AS NewOffset,
               cr.standard_instrument_info AS StandardInstrumentInfo, cr.next_due_date AS NextDueDate, cr.notes,
               u.full_name AS TechnicianName
        FROM calibration_records cr
        JOIN users u ON u.id = cr.technician_id
        WHERE cr.sensor_id = @id
        ORDER BY cr.calibration_date DESC", new { id })).ToList();

    var now = DateTime.UtcNow;
    var ageSec = sensor.LastReadingAt.HasValue ? (now - sensor.LastReadingAt.Value).TotalSeconds : double.MaxValue;

    return Results.Ok(new
    {
        Sensor = sensor,
        DataAgeSeconds = sensor.LastReadingAt.HasValue ? (long)ageSec : (long?)null,
        DataAgeStatus = sensor.LastReadingAt.HasValue ? Rules.DataAge(TimeSpan.FromSeconds(ageSec)) : "EXPIRED",
        DriftProfiles = driftProfiles,
        DataGaps = gaps,
        Calibrations = calibrations
    });
});

app.MapPost("/api/sensors", async (Db db, HttpContext ctx, RegisterSensorDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.HardwareId)) return Results.BadRequest(new { error = "Mã phần cứng (Hardware ID) không được để trống" });
    if (dto.ExpectedIntervalSec < Rules.IntervalMin || dto.ExpectedIntervalSec > Rules.IntervalMax)
        return Results.BadRequest(new { error = $"Chu kỳ gửi tin phải từ {Rules.IntervalMin}s đến {Rules.IntervalMax}s (BR03)" });

    var user = ctx.CurrentUser();
    await using var c = await db.OpenAsync();

    var exists = await c.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM sensors WHERE hardware_id = @HardwareId", new { dto.HardwareId });
    if (exists > 0) return Results.BadRequest(new { error = "Hardware ID đã tồn tại trong hệ thống" });

    var id = Guid.NewGuid();
    await c.ExecuteAsync(@"
        INSERT INTO sensors(id, hardware_id, zone_id, sensor_type, expected_interval_sec, firmware_version, connection_state, status)
        VALUES (@id, @HardwareId, @ZoneId, @SensorType, @ExpectedIntervalSec, @FirmwareVersion, 'NEVER_CONNECTED', 'ONLINE');
        INSERT INTO drift_profiles(sensor_id, metric_type) VALUES (@id, 'TEMPERATURE'), (@id, 'HUMIDITY');
        INSERT INTO sensor_assignments(sensor_id, zone_id, assigned_by) VALUES (@id, @ZoneId, @userId);",
        new { id, dto.HardwareId, dto.ZoneId, SensorType = dto.SensorType ?? "COMBO", dto.ExpectedIntervalSec, FirmwareVersion = dto.FirmwareVersion ?? "v1.0.0", userId = user?.Id });

    await c.Audit(user?.Id, "REGISTER_SENSOR", "sensors", id, dto);
    return Results.Created($"/api/sensors/{id}", new { id, success = true });
}).Roles("ADMIN", "TECHNICIAN", "WAREHOUSE_MANAGER");

// =============================================================================
// TELEMETRY & CHART DATA (UC06 — HONEST DATA, NO BLIND INTERPOLATION)
// =============================================================================
app.MapGet("/api/zones/{id:guid}/chart-data", async (Db db, Guid id, int hours = 24) =>
{
    hours = Math.Clamp(hours, 1, 168);
    var from = DateTime.UtcNow.AddHours(-hours);

    await using var c = await db.OpenAsync();

    // 1. Lấy thông tin zone & ngưỡng vi khí hậu
    var zone = await c.QueryFirstOrDefaultAsync<ZoneThresholdDto>(
        "SELECT id, code, name, temp_min AS TempMin, temp_max AS TempMax, humidity_min AS HumidityMin, humidity_max AS HumidityMax FROM zones WHERE id = @id",
        new { id });
    if (zone is null) return Results.NotFound(new { error = "Zone không tồn tại" });

    // 2. Lấy toàn bộ readings trong cửa sổ thời gian
    var readings = (await c.QueryAsync<ChartReadingPoint>(@"
        SELECT r.reading_time AS Time,
               r.sensor_id AS SensorId,
               s.hardware_id AS HardwareId,
               r.temperature AS Temperature,
               r.humidity AS Humidity,
               r.quality AS Quality
        FROM readings r
        JOIN sensors s ON s.id = r.sensor_id
        WHERE r.zone_id = @id AND r.reading_time >= @from
        ORDER BY r.reading_time ASC", new { id, from })).ToList();

    // 3. Lấy DataGaps đã xảy ra trong khoảng thời gian để frontend vẽ GapOverlay (BR08)
    var gaps = (await c.QueryAsync<ChartGapRange>(@"
        SELECT g.sensor_id AS SensorId,
               s.hardware_id AS HardwareId,
               g.start_time AS StartTime,
               COALESCE(g.end_time, SYSUTCDATETIME()) AS EndTime,
               g.duration_sec AS DurationSec,
               g.reason AS Reason
        FROM data_gaps g
        JOIN sensors s ON s.id = g.sensor_id
        WHERE (g.zone_id = @id OR s.zone_id = @id)
          AND (g.end_time IS NULL OR g.end_time >= @from)
        ORDER BY g.start_time ASC", new { id, from })).ToList();

    return Results.Ok(new
    {
        Zone = zone,
        FromTime = from,
        ToTime = DateTime.UtcNow,
        Readings = readings,
        Gaps = gaps
    });
});

app.MapPost("/api/ingest", async (IngestionService ingestion, IngestRequest req) =>
{
    if (string.IsNullOrWhiteSpace(req.HardwareId))
        return Results.BadRequest(new { error = "hardwareId là bắt buộc" });

    var p = new ReadingPayload(req.Ts ?? DateTime.UtcNow, req.Temperature, req.Humidity, req.Battery, req.Rssi);
    var res = await ingestion.ProcessAsync(req.HardwareId, p, "HTTP_API");

    return res switch
    {
        IngestResult.Accepted => Results.Ok(new { status = "ACCEPTED", quality = "GOOD" }),
        IngestResult.AcceptedAnomalous => Results.Ok(new { status = "ACCEPTED_ANOMALOUS", quality = "ANOMALOUS_REJECT" }),
        IngestResult.Duplicate => Results.Ok(new { status = "DUPLICATE_IGNORED" }),
        IngestResult.UnknownSensor => Results.NotFound(new { error = "Cảm biến chưa đăng ký" }),
        _ => Results.BadRequest(new { error = "Bản tin không hợp lệ" })
    };
});

// =============================================================================
// ALERT MANAGEMENT ENDPOINTS (UC07, BR12, BR13)
// =============================================================================
app.MapGet("/api/alerts", async (Db db, string? status, string? severity, Guid? zoneId) =>
{
    await using var c = await db.OpenAsync();
    var sql = @"
        SELECT a.id, a.zone_id AS ZoneId, z.name AS ZoneName, a.sensor_id AS SensorId, s.hardware_id AS HardwareId,
               a.alert_type AS AlertType, a.severity AS Severity, a.message AS Message,
               a.metric_value AS MetricValue, a.threshold_value AS ThresholdValue,
               a.status AS Status, a.created_at AS CreatedAt, a.last_seen_at AS LastSeenAt, a.occurrence_count AS OccurrenceCount,
               a.acknowledged_at AS AcknowledgedAt, uAck.full_name AS AcknowledgedByName,
               a.resolved_at AS ResolvedAt, uRes.full_name AS ResolvedByName, a.resolution_notes AS ResolutionNotes
        FROM alerts a
        LEFT JOIN zones z ON z.id = a.zone_id
        LEFT JOIN sensors s ON s.id = a.sensor_id
        LEFT JOIN users uAck ON uAck.id = a.acknowledged_by
        LEFT JOIN users uRes ON uRes.id = a.resolved_by
        WHERE (@status IS NULL OR a.status = @status)
          AND (@severity IS NULL OR a.severity = @severity)
          AND (@zoneId IS NULL OR a.zone_id = @zoneId)
        ORDER BY CASE a.severity WHEN 'EMERGENCY' THEN 1 WHEN 'CRITICAL' THEN 2 WHEN 'WARNING' THEN 3 ELSE 4 END,
                 a.created_at DESC";

    var list = await c.QueryAsync<AlertItemDto>(sql, new { status, severity, zoneId });
    return Results.Ok(list);
});

app.MapPost("/api/alerts/{id:guid}/acknowledge", async (Db db, Notifier notifier, HttpContext ctx, Guid id) =>
{
    var user = ctx.CurrentUser();
    await using var c = await db.OpenAsync();
    var count = await c.ExecuteAsync(@"
        UPDATE alerts SET status = 'ACKNOWLEDGED', acknowledged_at = SYSUTCDATETIME(), acknowledged_by = @userId
        WHERE id = @id AND status = 'ACTIVE'", new { id, userId = user?.Id });

    if (count == 0) return Results.BadRequest(new { error = "Cảnh báo không tồn tại hoặc đã được xử lý" });

    await c.Audit(user?.Id, "ACKNOWLEDGE_ALERT", "alerts", id);
    await notifier.Alert(new { id, status = "ACKNOWLEDGED", acknowledgedBy = user?.FullName, at = DateTime.UtcNow });
    return Results.Ok(new { success = true });
}).Roles("ADMIN", "WAREHOUSE_MANAGER", "MONITORING_OPERATOR");

app.MapPost("/api/alerts/{id:guid}/resolve", async (Db db, Notifier notifier, HttpContext ctx, Guid id, ResolveAlertDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.ResolutionNotes))
        return Results.BadRequest(new { error = "Ghi chú giải quyết là bắt buộc khi đóng cảnh báo" });

    var user = ctx.CurrentUser();
    await using var c = await db.OpenAsync();
    var count = await c.ExecuteAsync(@"
        UPDATE alerts SET status = 'RESOLVED', resolved_at = SYSUTCDATETIME(), resolved_by = @userId, resolution_notes = @notes
        WHERE id = @id AND status IN ('ACTIVE', 'ACKNOWLEDGED')", new { id, userId = user?.Id, notes = dto.ResolutionNotes });

    if (count == 0) return Results.BadRequest(new { error = "Cảnh báo không tồn tại hoặc đã đóng trước đó" });

    await c.Audit(user?.Id, "RESOLVE_ALERT", "alerts", id, dto);
    await notifier.Alert(new { id, status = "RESOLVED", resolvedBy = user?.FullName, notes = dto.ResolutionNotes, at = DateTime.UtcNow });
    return Results.Ok(new { success = true });
}).Roles("ADMIN", "WAREHOUSE_MANAGER", "MONITORING_OPERATOR");

// =============================================================================
// INSPECTION & CALIBRATION (UC08, UC09, BR14)
// =============================================================================
app.MapGet("/api/inspections", async (Db db) =>
{
    await using var c = await db.OpenAsync();
    var list = await c.QueryAsync<InspectionSummaryDto>(@"
        SELECT i.id, i.zone_id AS ZoneId, z.name AS ZoneName, i.inspection_date AS InspectionDate,
               i.status, i.overall_result AS OverallResult, i.notes, i.created_at AS CreatedAt,
               u.full_name AS InspectorName,
               (SELECT COUNT(*) FROM inspection_items WHERE inspection_id = i.id) AS ItemCount,
               (SELECT COUNT(*) FROM inspection_items WHERE inspection_id = i.id AND result = 'FAIL') AS FailCount
        FROM inspections i
        JOIN zones z ON z.id = i.zone_id
        JOIN users u ON u.id = i.inspector_id
        ORDER BY i.created_at DESC");
    return Results.Ok(list);
});

app.MapPost("/api/inspections", async (Db db, HttpContext ctx, CreateInspectionDto dto) =>
{
    if (dto.Items is null || dto.Items.Count == 0)
        return Results.BadRequest(new { error = "Phiên kiểm tra phải có ít nhất 1 hạng mục kiểm tra" });

    var user = ctx.CurrentUser();
    await using var c = await db.OpenAsync();
    await using var tx = c.BeginTransaction();

    var overallResult = dto.Items.Any(x => x.Result == "FAIL") ? "FAIL"
        : dto.Items.Any(x => x.Result == "PARTIAL") ? "PARTIAL" : "PASS";

    var inspectionId = Guid.NewGuid();
    await c.ExecuteAsync(@"
        INSERT INTO inspections(id, zone_id, inspector_id, inspection_date, status, overall_result, notes)
        VALUES (@inspectionId, @ZoneId, @userId, @InspectionDate, 'COMPLETED', @overallResult, @Notes)",
        new { inspectionId, dto.ZoneId, userId = user?.Id, dto.InspectionDate, overallResult, dto.Notes }, tx);

    foreach (var item in dto.Items)
    {
        await c.ExecuteAsync(@"
            INSERT INTO inspection_items(inspection_id, sensor_id, checklist_item, reference_value, sensor_reading_value, result, notes)
            VALUES (@inspectionId, @SensorId, @ChecklistItem, @ReferenceValue, @SensorReadingValue, @Result, @Notes)",
            new { inspectionId, item.SensorId, item.ChecklistItem, item.ReferenceValue, item.SensorReadingValue, item.Result, item.Notes }, tx);

        // BR14: nếu FAIL -> tự động khóa trạng thái cảm biến sang CALIBRATING
        if (item.Result == "FAIL")
        {
            await c.ExecuteAsync("UPDATE sensors SET status = 'CALIBRATING' WHERE id = @SensorId", new { item.SensorId }, tx);
        }
    }

    await c.Audit(user?.Id, "CREATE_INSPECTION", "inspections", inspectionId, new { overallResult, itemCount = dto.Items.Count }, tx);
    await tx.CommitAsync();

    return Results.Created($"/api/inspections/{inspectionId}", new { id = inspectionId, overallResult });
}).Roles("ADMIN", "TECHNICIAN");

app.MapPost("/api/sensors/{id:guid}/calibration", async (Db db, SimulatorState simState, HttpContext ctx, Guid id, RecordCalibrationDto dto) =>
{
    if (dto.NextDueDate <= DateTime.UtcNow)
        return Results.BadRequest(new { error = "Ngày hiệu chuẩn tiếp theo phải ở tương lai" });

    var user = ctx.CurrentUser();
    await using var c = await db.OpenAsync();

    var sensor = await c.QueryFirstOrDefaultAsync<SensorSimpleDto>(
        "SELECT id, hardware_id AS HardwareId, calibration_offset AS CalibrationOffset FROM sensors WHERE id = @id", new { id });
    if (sensor is null) return Results.NotFound(new { error = "Không tìm thấy cảm biến" });

    await using var tx = c.BeginTransaction();

    var calibId = Guid.NewGuid();
    await c.ExecuteAsync(@"
        INSERT INTO calibration_records(id, sensor_id, technician_id, old_offset, new_offset, standard_instrument_info, next_due_date, notes)
        VALUES (@calibId, @id, @userId, @OldOffset, @NewOffset, @StandardInstrumentInfo, @NextDueDate, @Notes);
        UPDATE sensors SET calibration_offset = @NewOffset, last_calibrated_at = SYSUTCDATETIME(), next_calibration_due = @NextDueDate, status = 'ONLINE' WHERE id = @id;
        UPDATE drift_profiles SET cusum_positive = 0, cusum_negative = 0, drift_severity = 'NONE', drift_rate_per_hour = 0, requires_calibration = 0 WHERE sensor_id = @id;",
        new { calibId, id, userId = user?.Id, OldOffset = sensor.CalibrationOffset, dto.NewOffset, dto.StandardInstrumentInfo, dto.NextDueDate, dto.Notes }, tx);

    await c.Audit(user?.Id, "RECORD_CALIBRATION", "sensors", id, dto, tx);
    await tx.CommitAsync();

    // Reset simulator drift nếu đang giả lập
    simState.ResetDrift(sensor.HardwareId);

    return Results.Ok(new { success = true });
}).Roles("ADMIN", "TECHNICIAN");

// =============================================================================
// REPORT GENERATION (UC10, BR15 — DATA COVERAGE %)
// =============================================================================
app.MapGet("/api/reports/preview", async (Db db, Guid warehouseId, Guid? zoneId, DateTime from, DateTime to) =>
{
    if (from >= to) return Results.BadRequest(new { error = "Thời gian bắt đầu phải trước thời gian kết thúc" });

    await using var c = await db.OpenAsync();

    var zones = (await c.QueryAsync<ZoneReportTarget>(@"
        SELECT z.id, z.name, z.temp_min AS TempMin, z.temp_max AS TempMax, z.humidity_min AS HumidityMin, z.humidity_max AS HumidityMax,
               (SELECT COUNT(*) FROM sensors WHERE zone_id = z.id AND status <> 'DECOMMISSIONED') AS SensorCount,
               COALESCE((SELECT AVG(expected_interval_sec) FROM sensors WHERE zone_id = z.id AND status <> 'DECOMMISSIONED'), 150) AS AvgIntervalSec
        FROM zones z
        WHERE z.warehouse_id = @warehouseId AND (@zoneId IS NULL OR z.id = @zoneId)", new { warehouseId, zoneId })).ToList();

    var summaries = new List<object>();
    var totalSurveySeconds = (to - from).TotalSeconds;

    foreach (var z in zones)
    {
        var expectedReadings = (int)Math.Max(1, (totalSurveySeconds / z.AvgIntervalSec) * z.SensorCount);

        var metrics = await c.QueryFirstOrDefaultAsync<ZoneAggMetric>(@"
            SELECT AVG(r.temperature) AS TempAvg, MIN(r.temperature) AS TempMin, MAX(r.temperature) AS TempMax,
                   AVG(r.humidity) AS HumidityAvg, MIN(r.humidity) AS HumidityMin, MAX(r.humidity) AS HumidityMax,
                   COUNT(*) AS ValidReadings,
                   SUM(CASE WHEN r.quality = 'GOOD' THEN 1 ELSE 0 END) AS GoodReadings,
                   SUM(CASE WHEN r.temperature < z.temp_min OR r.temperature > z.temp_max
                               OR r.humidity < z.humidity_min OR r.humidity > z.humidity_max THEN 1 ELSE 0 END) AS BreachCount
            FROM readings r
            JOIN zones z ON z.id = r.zone_id
            WHERE r.zone_id = @id AND r.reading_time >= @from AND r.reading_time <= @to AND r.quality <> 'ANOMALOUS_REJECT'",
            new { id = z.Id, from, to });

        var gapDuration = await c.ExecuteScalarAsync<int>(@"
            SELECT COALESCE(SUM(DATEDIFF(SECOND,
                     CASE WHEN start_time < @from THEN @from ELSE start_time END,
                     CASE WHEN end_time IS NULL OR end_time > @to THEN @to ELSE end_time END)), 0)
            FROM data_gaps WHERE zone_id = @id AND start_time <= @to AND (end_time IS NULL OR end_time >= @from)",
            new { id = z.Id, from, to });

        var goodCount = metrics?.GoodReadings ?? 0;
        var coveragePct = Math.Min(100.0, Math.Round((double)goodCount / expectedReadings * 100.0, 2));

        summaries.Add(new
        {
            ZoneId = z.Id,
            ZoneName = z.Name,
            TempMin = metrics?.TempMin,
            TempMax = metrics?.TempMax,
            TempAvg = metrics?.TempAvg,
            HumidityMin = metrics?.HumidityMin,
            HumidityMax = metrics?.HumidityMax,
            HumidityAvg = metrics?.HumidityAvg,
            ExpectedReadings = expectedReadings,
            ValidReadings = metrics?.ValidReadings ?? 0,
            GoodReadings = goodCount,
            BreachCount = metrics?.BreachCount ?? 0,
            TotalGapDurationSec = gapDuration,
            DataCoveragePercentage = coveragePct
        });
    }

    return Results.Ok(new
    {
        WarehouseId = warehouseId,
        FromTime = from,
        ToTime = to,
        ZoneSummaries = summaries
    });
});

app.MapGet("/api/reports/export-csv", async (Db db, Guid warehouseId, Guid? zoneId, DateTime from, DateTime to) =>
{
    await using var c = await db.OpenAsync();
    var rows = (await c.QueryAsync<CsvRow>(@"
        SELECT r.reading_time AS ReadingTime, z.name AS ZoneName, s.hardware_id AS HardwareId,
               r.temperature AS Temperature, r.humidity AS Humidity, r.battery_level AS BatteryLevel,
               r.rssi AS Rssi, r.quality AS Quality
        FROM readings r
        JOIN sensors s ON s.id = r.sensor_id
        JOIN zones z ON z.id = r.zone_id
        WHERE z.warehouse_id = @warehouseId AND (@zoneId IS NULL OR z.id = @zoneId)
          AND r.reading_time >= @from AND r.reading_time <= @to
        ORDER BY r.reading_time ASC", new { warehouseId, zoneId, from, to })).ToList();

    var sb = new System.Text.StringBuilder();
    sb.AppendLine("ReadingTime,ZoneName,HardwareId,Temperature,Humidity,BatteryLevel,RSSI,Quality");
    foreach (var r in rows)
    {
        sb.AppendLine($"{r.ReadingTime:yyyy-MM-ddTHH:mm:ssZ},{Escape(r.ZoneName)},{r.HardwareId},{r.Temperature},{r.Humidity},{r.BatteryLevel},{r.Rssi},{r.Quality}");
    }

    static string Escape(string s) => s.Contains(',') ? $"\"{s.Replace("\"", "\"\"")}\"" : s;

    return Results.File(System.Text.Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", $"WarehouseReport_{DateTime.UtcNow:yyyyMMdd_HHmmss}.csv");
});

// =============================================================================
// SIMULATOR CONTROLS (Demo & Live Testing)
// =============================================================================
app.MapGet("/api/simulator/status", (SimulatorState sim, IngestionMetrics metrics, MqttBrokerService broker) =>
{
    return Results.Ok(new
    {
        BrokerRunning = broker.Running,
        BrokerPort = broker.Port,
        SimulatorEnabled = sim.Enabled,
        Sensors = sim.Sensors.Values.Select(s => new
        {
            s.HardwareId,
            s.ForcedOffline,
            s.DriftPerHour,
            s.DriftOffset,
            s.Battery,
            s.Excursion,
            s.LastSentAt
        }),
        Metrics = metrics.Snapshot()
    });
});

app.MapPost("/api/simulator/sensor/{hardwareId}/toggle-offline", (SimulatorState sim, string hardwareId) =>
{
    if (sim.Sensors.TryGetValue(hardwareId, out var s))
    {
        s.ForcedOffline = !s.ForcedOffline;
        return Results.Ok(new { hardwareId, forcedOffline = s.ForcedOffline });
    }
    return Results.NotFound(new { error = "Không tìm thấy cảm biến trong simulator" });
});

app.MapPost("/api/simulator/sensor/{hardwareId}/drift", (SimulatorState sim, string hardwareId, double driftPerHour) =>
{
    if (sim.Sensors.TryGetValue(hardwareId, out var s))
    {
        s.DriftPerHour = driftPerHour;
        return Results.Ok(new { hardwareId, driftPerHour });
    }
    return Results.NotFound(new { error = "Không tìm thấy cảm biến trong simulator" });
});

app.MapPost("/api/simulator/sensor/{hardwareId}/excursion", (SimulatorState sim, string hardwareId) =>
{
    if (sim.Sensors.TryGetValue(hardwareId, out var s))
    {
        s.Excursion = !s.Excursion;
        return Results.Ok(new { hardwareId, excursion = s.Excursion });
    }
    return Results.NotFound(new { error = "Không tìm thấy cảm biến trong simulator" });
});

app.MapGet("/api/metrics", (IngestionMetrics metrics) => Results.Ok(metrics.Snapshot()));

app.UseDefaultFiles();
app.UseStaticFiles();
app.MapFallbackToFile("index.html");

app.Run();

// =============================================================================
// DTO & RECORD DEFINITIONS
// =============================================================================
public record LoginDto(string Username, string Password);
public record UpdateThresholdDto(decimal TempMin, decimal TempMax, decimal HumidityMin, decimal HumidityMax);
public record RegisterSensorDto(string HardwareId, Guid? ZoneId, string? SensorType, int ExpectedIntervalSec, string? FirmwareVersion);
public record IngestRequest(string HardwareId, DateTime? Ts, decimal? Temperature, decimal? Humidity, int? Battery, int? Rssi);
public record ResolveAlertDto(string ResolutionNotes);
public record CreateInspectionDto(Guid ZoneId, DateTime InspectionDate, string? Notes, List<InspectionItemInputDto> Items);
public record InspectionItemInputDto(Guid SensorId, string ChecklistItem, decimal? ReferenceValue, decimal? SensorReadingValue, string Result, string? Notes);
public record RecordCalibrationDto(decimal NewOffset, string StandardInstrumentInfo, DateTime NextDueDate, string? Notes);

public class UserRecord { public Guid Id { get; set; } public string Username { get; set; } = ""; public string PasswordHash { get; set; } = ""; public string FullName { get; set; } = ""; public string Email { get; set; } = ""; public string Role { get; set; } = ""; public bool IsActive { get; set; } }
public class WarehouseItem { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string Address { get; set; } = ""; public decimal? CapacityM2 { get; set; } public string? Description { get; set; } public bool IsActive { get; set; } }
public class ZoneSummary { public Guid Id { get; set; } public Guid WarehouseId { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string ZoneType { get; set; } = ""; public decimal TempMin { get; set; } public decimal TempMax { get; set; } public decimal HumidityMin { get; set; } public decimal HumidityMax { get; set; } public int SensorCount { get; set; } public int ConnectedSensorCount { get; set; } }
public class ZoneOverviewDto { public Guid Id { get; set; } public Guid WarehouseId { get; set; } public string WarehouseName { get; set; } = ""; public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string ZoneType { get; set; } = ""; public decimal TempMin { get; set; } public decimal TempMax { get; set; } public decimal HumidityMin { get; set; } public decimal HumidityMax { get; set; } public int TotalSensors { get; set; } public int OnlineSensors { get; set; } public int ActiveAlerts { get; set; } }
public class LatestZoneTelemetry { public Guid ZoneId { get; set; } public decimal? TempAvg { get; set; } public decimal? HumidityAvg { get; set; } public DateTime? LastReadingAt { get; set; } }
public class ZoneDetailDto { public Guid Id { get; set; } public Guid WarehouseId { get; set; } public string WarehouseName { get; set; } = ""; public string Code { get; set; } = ""; public string Name { get; set; } = ""; public string ZoneType { get; set; } = ""; public decimal TempMin { get; set; } public decimal TempMax { get; set; } public decimal HumidityMin { get; set; } public decimal HumidityMax { get; set; } public DateTime CreatedAt { get; set; } }
public class SensorInZoneDto { public Guid Id { get; set; } public string HardwareId { get; set; } = ""; public string SensorType { get; set; } = ""; public string ConnectionState { get; set; } = ""; public string Status { get; set; } = ""; public int? BatteryLevel { get; set; } public int? LastRssi { get; set; } public int ExpectedIntervalSec { get; set; } public DateTime? LastReadingAt { get; set; } public decimal? LatestTemp { get; set; } public decimal? LatestHumidity { get; set; } public decimal? TempDriftRate { get; set; } public string? TempDriftSeverity { get; set; } }
public class SensorListItemDto { public Guid Id { get; set; } public string HardwareId { get; set; } = ""; public string SensorType { get; set; } = ""; public int ExpectedIntervalSec { get; set; } public int StaleThresholdSec { get; set; } public int DisconnectThresholdSec { get; set; } public string FirmwareVersion { get; set; } = ""; public int? BatteryLevel { get; set; } public int? LastRssi { get; set; } public string ConnectionState { get; set; } = ""; public string Status { get; set; } = ""; public DateTime? LastReadingAt { get; set; } public DateTime? LastCalibratedAt { get; set; } public DateTime? NextCalibrationDue { get; set; } public Guid? ZoneId { get; set; } public string? ZoneName { get; set; } public string? WarehouseName { get; set; } public decimal? TempDriftRate { get; set; } public string? TempDriftSeverity { get; set; } }
public class SensorDetailFullDto { public Guid Id { get; set; } public string HardwareId { get; set; } = ""; public string SensorType { get; set; } = ""; public int ExpectedIntervalSec { get; set; } public int StaleThresholdSec { get; set; } public int DisconnectThresholdSec { get; set; } public string FirmwareVersion { get; set; } = ""; public int? BatteryLevel { get; set; } public int? LastRssi { get; set; } public string ConnectionState { get; set; } = ""; public string Status { get; set; } = ""; public decimal CalibrationOffset { get; set; } public DateTime? LastReadingAt { get; set; } public DateTime? LastCalibratedAt { get; set; } public DateTime? NextCalibrationDue { get; set; } public Guid? ZoneId { get; set; } public string? ZoneName { get; set; } public Guid? WarehouseId { get; set; } public string? WarehouseName { get; set; } }
public class DriftProfileDto { public string MetricType { get; set; } = ""; public decimal BaselineOffset { get; set; } public decimal CusumPositive { get; set; } public decimal CusumNegative { get; set; } public decimal DriftRatePerHour { get; set; } public string DriftSeverity { get; set; } = ""; public bool RequiresCalibration { get; set; } public DateTime? LastEvaluatedAt { get; set; } }
public class DataGapDto { public Guid Id { get; set; } public DateTime StartTime { get; set; } public DateTime? EndTime { get; set; } public int? DurationSec { get; set; } public string Reason { get; set; } = ""; public DateTime CreatedAt { get; set; } }
public class CalibrationRecordDto { public Guid Id { get; set; } public DateTime CalibrationDate { get; set; } public decimal? OldOffset { get; set; } public decimal NewOffset { get; set; } public string StandardInstrumentInfo { get; set; } = ""; public DateTime NextDueDate { get; set; } public string? Notes { get; set; } public string TechnicianName { get; set; } = ""; }
public class ZoneThresholdDto { public Guid Id { get; set; } public string Code { get; set; } = ""; public string Name { get; set; } = ""; public decimal TempMin { get; set; } public decimal TempMax { get; set; } public decimal HumidityMin { get; set; } public decimal HumidityMax { get; set; } }
public class ChartReadingPoint { public DateTime Time { get; set; } public Guid SensorId { get; set; } public string HardwareId { get; set; } = ""; public decimal? Temperature { get; set; } public decimal? Humidity { get; set; } public string Quality { get; set; } = ""; }
public class ChartGapRange { public Guid SensorId { get; set; } public string HardwareId { get; set; } = ""; public DateTime StartTime { get; set; } public DateTime EndTime { get; set; } public int? DurationSec { get; set; } public string Reason { get; set; } = ""; }
public class AlertItemDto { public Guid Id { get; set; } public Guid? ZoneId { get; set; } public string? ZoneName { get; set; } public Guid? SensorId { get; set; } public string? HardwareId { get; set; } public string AlertType { get; set; } = ""; public string Severity { get; set; } = ""; public string Message { get; set; } = ""; public decimal? MetricValue { get; set; } public decimal? ThresholdValue { get; set; } public string Status { get; set; } = ""; public DateTime CreatedAt { get; set; } public DateTime LastSeenAt { get; set; } public int OccurrenceCount { get; set; } public DateTime? AcknowledgedAt { get; set; } public string? AcknowledgedByName { get; set; } public DateTime? ResolvedAt { get; set; } public string? ResolvedByName { get; set; } public string? ResolutionNotes { get; set; } }
public class InspectionSummaryDto { public Guid Id { get; set; } public Guid ZoneId { get; set; } public string ZoneName { get; set; } = ""; public DateTime InspectionDate { get; set; } public string Status { get; set; } = ""; public string? OverallResult { get; set; } public string? Notes { get; set; } public DateTime CreatedAt { get; set; } public string InspectorName { get; set; } = ""; public int ItemCount { get; set; } public int FailCount { get; set; } }
public class SensorSimpleDto { public Guid Id { get; set; } public string HardwareId { get; set; } = ""; public decimal CalibrationOffset { get; set; } }
public class ZoneReportTarget { public Guid Id { get; set; } public string Name { get; set; } = ""; public decimal TempMin { get; set; } public decimal TempMax { get; set; } public decimal HumidityMin { get; set; } public decimal HumidityMax { get; set; } public int SensorCount { get; set; } public double AvgIntervalSec { get; set; } }
public class ZoneAggMetric { public decimal? TempAvg { get; set; } public decimal? TempMin { get; set; } public decimal? TempMax { get; set; } public decimal? HumidityAvg { get; set; } public decimal? HumidityMin { get; set; } public decimal? HumidityMax { get; set; } public int ValidReadings { get; set; } public int GoodReadings { get; set; } public int BreachCount { get; set; } }
public class CsvRow { public DateTime ReadingTime { get; set; } public string ZoneName { get; set; } = ""; public string HardwareId { get; set; } = ""; public decimal? Temperature { get; set; } public decimal? Humidity { get; set; } public int? BatteryLevel { get; set; } public int? Rssi { get; set; } public string Quality { get; set; } = ""; }
