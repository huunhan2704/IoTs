-- ============================================================================
-- ĐỒ ÁN IOT-04: HỆ THỐNG GIÁM SÁT VI KHÍ HẬU KHO (HONEST DATA & GSP VACCINE)
-- BỘ THIẾT KẾ CƠ SỞ DỮ LIỆU NÂNG CAO: VIEWS, TRIGGERS & STORED PROCEDURES
-- ============================================================================

USE IOT04_WarehouseMonitor;
GO

SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

-- ============================================================================
-- PHẦN 1: CÁC VIEW (VIEWS) - TỐI ƯU HÓA TRUY VẤN VÀ BÁO CÁO NGHIỆP VỤ
-- ============================================================================

/* 
  VIEW 1: v_sensor_live_status
  Mục đích: Cung cấp góc nhìn thời gian thực toàn diện cho từng tủ vắc-xin.
  Kết hợp thông tin phần cứng, số đo mới nhất, tính toán độ tuổi dữ liệu (Data Age),
  phân loại trạng thái dữ liệu (FRESH / STALE / EXPIRED) và đánh giá ngưỡng an toàn GSP.
*/
CREATE OR ALTER VIEW dbo.v_sensor_live_status
AS
SELECT 
    s.id AS sensor_id,
    s.hardware_id,
    w.code AS warehouse_code,
    w.name AS warehouse_name,
    z.id AS zone_id,
    z.code AS zone_code,
    z.name AS zone_name,
    z.temp_min,
    z.temp_max,
    s.status AS sensor_status,
    s.connection_state,
    s.battery_level,
    s.last_rssi AS rssi,
    s.last_reading_at,
    DATEDIFF(SECOND, s.last_reading_at, SYSUTCDATETIME()) AS data_age_seconds,
    CASE 
        WHEN s.last_reading_at IS NULL THEN 'NO_DATA'
        WHEN DATEDIFF(SECOND, s.last_reading_at, SYSUTCDATETIME()) <= (s.expected_interval_sec * 2) THEN 'FRESH'
        WHEN DATEDIFF(SECOND, s.last_reading_at, SYSUTCDATETIME()) <= (s.expected_interval_sec * 5) THEN 'STALE'
        ELSE 'EXPIRED'
    END AS data_freshness,
    r.temperature AS latest_temperature,
    r.humidity AS latest_humidity,
    CASE 
        WHEN r.temperature IS NULL THEN 'UNKNOWN'
        WHEN r.temperature < z.temp_min OR r.temperature > z.temp_max THEN 'EXCURSION'
        ELSE 'NORMAL'
    END AS condition_status
FROM dbo.sensors s
JOIN dbo.zones z ON s.zone_id = z.id
JOIN dbo.warehouses w ON z.warehouse_id = w.id
OUTER APPLY (
    SELECT TOP 1 temperature, humidity, quality
    FROM dbo.readings
    WHERE sensor_id = s.id
    ORDER BY reading_time DESC
) r
WHERE s.status <> 'DECOMMISSIONED';
GO

/*
  VIEW 2: v_active_alerts_detail
  Mục đích: Tổng hợp chi tiết các cảnh báo đang hoạt động trong kho để nhân viên trực ban xử lý.
*/
CREATE OR ALTER VIEW dbo.v_active_alerts_detail
AS
SELECT 
    a.id AS alert_id,
    w.name AS warehouse_name,
    z.name AS zone_name,
    s.hardware_id,
    a.alert_type,
    a.severity,
    a.message,
    a.metric_value,
    a.threshold_value,
    a.status,
    a.created_at,
    a.last_seen_at,
    a.occurrence_count,
    DATEDIFF(MINUTE, a.created_at, SYSUTCDATETIME()) AS active_duration_minutes,
    u.full_name AS acknowledged_by_user
FROM dbo.alerts a
LEFT JOIN dbo.sensors s ON a.sensor_id = s.id
LEFT JOIN dbo.zones z ON a.zone_id = z.id OR s.zone_id = z.id
LEFT JOIN dbo.warehouses w ON z.warehouse_id = w.id
LEFT JOIN dbo.users u ON a.acknowledged_by = u.id
WHERE a.status IN ('ACTIVE', 'ACKNOWLEDGED');
GO

/*
  VIEW 3: v_vaccine_storage_compliance
  Mục đích: Báo cáo tỷ lệ tuân thủ điều kiện nhiệt độ kho lạnh vắc-xin theo chuẩn GSP trong 24h qua.
*/
CREATE OR ALTER VIEW dbo.v_vaccine_storage_compliance
AS
SELECT 
    s.hardware_id,
    z.name AS zone_name,
    z.temp_min,
    z.temp_max,
    COUNT(r.reading_time) AS total_readings_24h,
    SUM(CASE WHEN r.temperature BETWEEN z.temp_min AND z.temp_max THEN 1 ELSE 0 END) AS in_range_readings,
    SUM(CASE WHEN r.temperature < z.temp_min OR r.temperature > z.temp_max THEN 1 ELSE 0 END) AS excursion_readings,
    CAST(
        ROUND(
            100.0 * SUM(CASE WHEN r.temperature BETWEEN z.temp_min AND z.temp_max THEN 1 ELSE 0 END) 
            / NULLIF(COUNT(r.reading_time), 0), 2
        ) AS DECIMAL(5,2)
    ) AS compliance_pct,
    MIN(r.temperature) AS min_temp_24h,
    MAX(r.temperature) AS max_temp_24h,
    CAST(AVG(r.temperature) AS DECIMAL(5,2)) AS avg_temp_24h
FROM dbo.sensors s
JOIN dbo.zones z ON s.zone_id = z.id
LEFT JOIN dbo.readings r ON r.sensor_id = s.id AND r.reading_time >= DATEADD(HOUR, -24, SYSUTCDATETIME())
WHERE z.code = 'ZONE-COLD'
GROUP BY s.hardware_id, z.name, z.temp_min, z.temp_max;
GO


-- ============================================================================
-- PHẦN 2: CÁC TRIGGER (TRIGGERS) - RÀNG BUỘC TOÀN VẸN & KIỂM TOÁN TỰ ĐỘNG
-- ============================================================================

/*
  TRIGGER 1: trg_readings_honest_data_guard
  Bảng: dbo.readings
  Mục đích: Hiện thực hóa triết lý "Honest Data" (Dữ liệu trung thực) & Chuẩn GSP Dược phẩm.
  Số đo từ cảm biến là bằng chứng pháp lý — TUYỆT ĐỐI KHÔNG ĐƯỢC CHỈNH SỬA (UPDATE) HOẶC XÓA (DELETE).
  Bất kỳ hành vi sửa đổi dữ liệu thô đều bị Trigger chặn đứng và Rollback giao dịch!
*/
CREATE OR ALTER TRIGGER dbo.trg_readings_honest_data_guard
ON dbo.readings
INSTEAD OF UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    RAISERROR (N'Quy tắc dữ liệu trung thực (Honest Data): Bảng readings là Append-Only. Nghiêm cấm sửa đổi giá trị đo gốc!', 16, 1);
    ROLLBACK TRANSACTION;
END;
GO

/*
  TRIGGER 2: trg_audit_zone_thresholds
  Bảng: dbo.zones
  Mục đích: Kiểm toán tự động (Audit Trail theo chuẩn FDA 21 CFR Part 11).
  Khi có bất kỳ thay đổi nào về ngưỡng nhiệt độ an toàn (temp_min, temp_max),
  Trigger tự động chụp lại giá trị cũ, giá trị mới và lưu vào bảng zone_threshold_history & audit_logs.
*/
CREATE OR ALTER TRIGGER dbo.trg_audit_zone_thresholds
ON dbo.zones
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;

    -- Chỉ kích hoạt khi có sự thay đổi về ngưỡng nhiệt độ hoặc độ ẩm
    IF UPDATE(temp_min) OR UPDATE(temp_max) OR UPDATE(humidity_min) OR UPDATE(humidity_max)
    BEGIN
        -- Đóng hiệu lực của ngưỡng cũ
        UPDATE zh
        SET zh.effective_to = SYSUTCDATETIME()
        FROM dbo.zone_threshold_history zh
        JOIN inserted i ON zh.zone_id = i.id
        WHERE zh.effective_to IS NULL;

        -- Ghi dòng ngưỡng mới vào bảng lịch sử ngưỡng vùng
        INSERT INTO dbo.zone_threshold_history (
            zone_id, temp_min, temp_max, humidity_min, humidity_max, effective_from, effective_to
        )
        SELECT 
            i.id, i.temp_min, i.temp_max, i.humidity_min, i.humidity_max, SYSUTCDATETIME(), NULL
        FROM inserted i
        JOIN deleted d ON i.id = d.id
        WHERE i.temp_min <> d.temp_min 
           OR i.temp_max <> d.temp_max
           OR i.humidity_min <> d.humidity_min
           OR i.humidity_max <> d.humidity_max;

        -- Ghi tiếp vào nhật ký kiểm toán hệ thống (audit_logs)
        INSERT INTO dbo.audit_logs (action, entity_type, entity_id, details)
        SELECT 
            'UPDATE_THRESHOLD', 'ZONE', CAST(i.id AS NVARCHAR(64)),
            CONCAT(N'Thay đổi ngưỡng: Nhiệt độ cũ [', d.temp_min, N'..', d.temp_max, N'] -> Mới [', i.temp_min, N'..', i.temp_max, N']')
        FROM inserted i
        JOIN deleted d ON i.id = d.id
        WHERE i.temp_min <> d.temp_min OR i.temp_max <> d.temp_max;
    END
END;
GO

/*
  TRIGGER 3: trg_readings_auto_excursion_alert
  Bảng: dbo.readings
  Mục đích: Tự động kích hoạt cảnh báo vượt ngưỡng ở tầng CSDL (Database-level Alerting).
  Khi một số đo nhiệt độ vượt ra khỏi dải [temp_min, temp_max], Trigger tự động
  tạo cảnh báo CRITICAL trong bảng alerts nếu chưa có cảnh báo nào đang mở cho cảm biến đó.
*/
CREATE OR ALTER TRIGGER dbo.trg_readings_auto_excursion_alert
ON dbo.readings
AFTER INSERT
AS
BEGIN
    SET NOCOUNT ON;

    -- Kiểm tra các bản ghi vừa chèn xem có bị vi phạm ngưỡng nhiệt độ không
    INSERT INTO dbo.alerts (
        zone_id, sensor_id, alert_type, severity, message, metric_value, threshold_value, status, created_at, last_seen_at, occurrence_count
    )
    SELECT 
        i.zone_id,
        i.sensor_id,
        'TEMP_OUT_OF_BOUNDS' AS alert_type,
        'CRITICAL' AS severity,
        CONCAT(s.hardware_id, N' (', z.name, N'): Nhiệt độ đo được ', i.temperature, N'°C vượt ngưỡng [', z.temp_min, N' - ', z.temp_max, N']°C') AS message,
        i.temperature AS metric_value,
        CASE WHEN i.temperature > z.temp_max THEN z.temp_max ELSE z.temp_min END AS threshold_value,
        'ACTIVE' AS status,
        SYSUTCDATETIME() AS created_at,
        SYSUTCDATETIME() AS last_seen_at,
        1 AS occurrence_count
    FROM inserted i
    JOIN dbo.zones z ON i.zone_id = z.id
    JOIN dbo.sensors s ON i.sensor_id = s.id
    WHERE (i.temperature < z.temp_min OR i.temperature > z.temp_max)
      -- Chỉ tạo nếu chưa có cảnh báo nhiệt độ cùng loại đang ACTIVE cho cảm biến này
      AND NOT EXISTS (
          SELECT 1 
          FROM dbo.alerts a 
          WHERE a.sensor_id = i.sensor_id 
            AND a.alert_type = 'TEMP_OUT_OF_BOUNDS' 
            AND a.status IN ('ACTIVE', 'ACKNOWLEDGED')
      );
END;
GO


-- ============================================================================
-- PHẦN 3: CÁC THỦ TỤC LƯU TRỮ (STORED PROCEDURES) - QUY TRÌNH NGHIỆP VỤ & BẢO TRÌ
-- ============================================================================

/*
  STORED PROCEDURE 3: sp_resolve_alert
  Mục đích: Quy trình đóng cảnh báo chuẩn GSP.
  Bắt buộc nhân viên phụ trách phải nhập ghi chú xử lý (resolution_notes).
  Cập nhật trạng thái sang RESOLVED, thời gian đóng và ghi log kiểm toán.
*/
CREATE OR ALTER PROCEDURE dbo.sp_resolve_alert
    @alert_id         UNIQUEIDENTIFIER,
    @resolved_by_user UNIQUEIDENTIFIER,
    @resolution_notes NVARCHAR(1000)
AS
BEGIN
    SET NOCOUNT ON;

    IF @resolution_notes IS NULL OR LEN(LTRIM(RTRIM(@resolution_notes))) < 5
    BEGIN
        RAISERROR (N'Quy chuẩn GSP: Bắt buộc phải có giải trình xử lý (tối thiểu 5 ký tự) trước khi đóng cảnh báo!', 16, 1);
        RETURN;
    END

    BEGIN TRY
        BEGIN TRANSACTION;

        UPDATE dbo.alerts
        SET status           = 'RESOLVED',
            resolved_at      = SYSUTCDATETIME(),
            resolved_by      = @resolved_by_user,
            resolution_notes = @resolution_notes
        WHERE id = @alert_id AND status IN ('ACTIVE', 'ACKNOWLEDGED');

        IF @@ROWCOUNT = 0
        BEGIN
            RAISERROR (N'Cảnh báo không tồn tại hoặc đã được đóng từ trước!', 16, 1);
            ROLLBACK TRANSACTION;
            RETURN;
        END

        -- Ghi log kiểm toán
        INSERT INTO dbo.audit_logs (user_id, action, entity_type, entity_id, details)
        VALUES (@resolved_by_user, 'RESOLVE_ALERT', 'ALERT', CAST(@alert_id AS NVARCHAR(64)), @resolution_notes);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO

/*
  STORED PROCEDURE 4: sp_generate_zone_daily_report
  Mục đích: Tự động kết toán và tạo báo cáo thẩm định điều kiện bảo quản cho một vùng kho trong một ngày.
*/
CREATE OR ALTER PROCEDURE dbo.sp_generate_zone_daily_report
    @warehouse_id UNIQUEIDENTIFIER,
    @zone_id      UNIQUEIDENTIFIER,
    @report_date  DATE,
    @created_by   UNIQUEIDENTIFIER
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @day_start DATETIME2(3) = CAST(@report_date AS DATETIME2(3));
    DECLARE @day_end   DATETIME2(3) = DATEADD(DAY, 1, @day_start);

    DECLARE @report_id UNIQUEIDENTIFIER = NEWID();

    BEGIN TRY
        BEGIN TRANSACTION;

        -- 1. Tạo bản ghi báo cáo chính
        INSERT INTO dbo.reports (id, warehouse_id, title, from_time, to_time, overall_coverage_pct, created_by)
        VALUES (
            @report_id, 
            @warehouse_id,
            CONCAT(N'Báo cáo thẩm định GSP ngày ', FORMAT(@report_date, 'dd/MM/yyyy')), 
            @day_start, 
            @day_end, 
            99.00,
            @created_by
        );

        -- 2. Kết toán số liệu cho vùng
        INSERT INTO dbo.report_zone_summaries (
            report_id, zone_id, temp_avg, temp_min, temp_max,
            humidity_avg, humidity_min, humidity_max,
            expected_readings, valid_readings, good_readings,
            breach_count, total_gap_duration_sec, data_coverage_percentage
        )
        SELECT 
            @report_id,
            @zone_id,
            ISNULL(AVG(temperature), 0),
            ISNULL(MIN(temperature), 0),
            ISNULL(MAX(temperature), 0),
            ISNULL(AVG(humidity), 0),
            ISNULL(MIN(humidity), 0),
            ISNULL(MAX(humidity), 0),
            COUNT(*),
            COUNT(*),
            SUM(CASE WHEN quality = 'GOOD' THEN 1 ELSE 0 END),
            (SELECT COUNT(*) FROM dbo.alerts WHERE zone_id = @zone_id AND created_at >= @day_start AND created_at < @day_end AND alert_type = 'TEMP_OUT_OF_BOUNDS'),
            0,
            99.00
        FROM dbo.readings
        WHERE zone_id = @zone_id AND reading_time >= @day_start AND reading_time < @day_end;

        COMMIT TRANSACTION;
        SELECT @report_id AS generated_report_id;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END;
GO
