/* ============================================================================
   HỆ THỐNG IOT-04: THEO DÕI ĐIỀU KIỆN KHO BẰNG CẢM BIẾN
   DATABASE INITIALIZATION SCRIPT — Microsoft SQL Server 2019+ (đã test trên 2025 Express)
   ----------------------------------------------------------------------------
   Chuyển đổi từ bản PostgreSQL 16 + TimescaleDB trong đồ án, kèm các chỉnh sửa:
     [F1] drift_profiles: PK (sensor_id, metric_type) — cảm biến COMBO có 2 hồ sơ drift
     [F2] sensors.expected_interval_sec: bổ sung cận trên 3600s (BR03)
     [F3] stale/disconnect threshold là cột tính toán (2x, 5x) -> không thể lệch BR05/BR06
     [F4] readings: thêm PK (sensor_id, reading_time) chống bản tin trùng (MQTT QoS1),
          thêm zone_id (snapshot vị trí lúc đo), received_at (giờ server nhận),
          bỏ quality 'STALE'/'GAP' (gap không phải là một reading)
     [F5] data_gaps: bỏ cột resolved (suy ra từ end_time), duration là cột tính toán,
          unique filtered index: mỗi sensor chỉ có tối đa 1 gap đang mở
     [F6] alerts: CHECK alert_type, thêm metric_value/threshold_value,
          chống bão cảnh báo: mỗi (sensor, alert_type) chỉ 1 alert chưa RESOLVED
     [F7] zone_threshold_history: lưu lịch sử ngưỡng để báo cáo GSP đánh giá đúng
          ngưỡng có hiệu lực tại thời điểm đo
     [F8] sensor_assignments: lịch sử gán/chuyển zone của cảm biến
     [F9] audit_logs: nhật ký thao tác (yêu cầu truy vết GSP/GDP)
     [F10] Không dùng ON DELETE CASCADE từ kho/zone/sensor xuống dữ liệu đo:
          xóa mềm bằng is_active / DECOMMISSIONED để không mất bằng chứng tuân thủ
     [F11] reports: thêm định dạng, trạng thái, coverage tổng
     [F12] calibration_records: liên kết tùy chọn tới inspection gây ra hiệu chuẩn
   Thay thế tính năng TimescaleDB:
     - Hypertable readings      -> bảng clustered (sensor_id, reading_time)
     - Continuous aggregate     -> bảng readings_hourly + sp_refresh_readings_hourly
       readings_hourly             (backend gọi định kỳ; SQL Express không có Agent)
     - zone_daily_summary       -> VIEW tổng hợp từ readings_hourly
     - retention policy         -> sp_apply_retention
   Quy ước thời gian: mọi cột DATETIME2 lưu giờ UTC.
   ============================================================================ */

IF DB_ID(N'IOT04_WarehouseMonitor') IS NULL
    CREATE DATABASE IOT04_WarehouseMonitor;
GO
USE IOT04_WarehouseMonitor;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO

/* Xóa theo thứ tự phụ thuộc để script có thể chạy lại */
DROP VIEW  IF EXISTS dbo.v_zone_daily_summary;
DROP PROCEDURE IF EXISTS dbo.sp_refresh_readings_hourly;
DROP PROCEDURE IF EXISTS dbo.sp_apply_retention;
DROP TABLE IF EXISTS dbo.audit_logs, dbo.report_zone_summaries, dbo.reports,
                     dbo.calibration_records, dbo.inspection_items, dbo.inspections,
                     dbo.alerts, dbo.drift_history, dbo.drift_profiles, dbo.data_gaps,
                     dbo.readings_hourly, dbo.readings, dbo.sensor_assignments,
                     dbo.sensors, dbo.zone_threshold_history, dbo.zones,
                     dbo.warehouses, dbo.users;
GO

/* 1. users */
CREATE TABLE dbo.users (
    id             UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_users PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    username       VARCHAR(50)   NOT NULL CONSTRAINT uq_users_username UNIQUE,
    password_hash  VARCHAR(255)  NOT NULL,
    full_name      NVARCHAR(100) NOT NULL,
    email          VARCHAR(100)  NOT NULL CONSTRAINT uq_users_email UNIQUE,
    role           VARCHAR(30)   NOT NULL CONSTRAINT ck_users_role
                   CHECK (role IN ('ADMIN','WAREHOUSE_MANAGER','MONITORING_OPERATOR','TECHNICIAN')),
    is_active      BIT           NOT NULL DEFAULT 1,
    last_login_at  DATETIME2(3)  NULL,
    created_at     DATETIME2(3)  NOT NULL DEFAULT SYSUTCDATETIME()
);

/* 2. warehouses */
CREATE TABLE dbo.warehouses (
    id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_warehouses PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    code         VARCHAR(30)    NOT NULL CONSTRAINT uq_warehouses_code UNIQUE,
    name         NVARCHAR(100)  NOT NULL,
    address      NVARCHAR(255)  NOT NULL,
    capacity_m2  DECIMAL(10,2)  NULL,
    description  NVARCHAR(MAX)  NULL,
    is_active    BIT            NOT NULL DEFAULT 1,
    created_at   DATETIME2(3)   NOT NULL DEFAULT SYSUTCDATETIME()
);

/* 3. zones (BR01, BR02) */
CREATE TABLE dbo.zones (
    id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_zones PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    warehouse_id  UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_zones_warehouse REFERENCES dbo.warehouses(id),
    code          VARCHAR(30)   NOT NULL,
    name          NVARCHAR(100) NOT NULL,
    zone_type     VARCHAR(30)   NOT NULL CONSTRAINT ck_zones_type
                  CHECK (zone_type IN ('COLD_STORAGE','COOL_STORAGE','DRY_STORAGE','RECEIVING_DOCK','DISPATCH_DOCK')),
    temp_min      DECIMAL(5,2)  NOT NULL,
    temp_max      DECIMAL(5,2)  NOT NULL,
    humidity_min  DECIMAL(5,2)  NOT NULL,
    humidity_max  DECIMAL(5,2)  NOT NULL,
    is_active     BIT           NOT NULL DEFAULT 1,
    created_at    DATETIME2(3)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT ck_zones_temp     CHECK (temp_min < temp_max),
    CONSTRAINT ck_zones_humidity CHECK (humidity_min < humidity_max AND humidity_min >= 0 AND humidity_max <= 100),
    CONSTRAINT uq_zones_wh_code  UNIQUE (warehouse_id, code)
);

/* 3b. zone_threshold_history [F7] — ngưỡng có hiệu lực theo thời gian */
CREATE TABLE dbo.zone_threshold_history (
    id             BIGINT IDENTITY(1,1) CONSTRAINT pk_zth PRIMARY KEY,
    zone_id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_zth_zone REFERENCES dbo.zones(id),
    temp_min       DECIMAL(5,2) NOT NULL,
    temp_max       DECIMAL(5,2) NOT NULL,
    humidity_min   DECIMAL(5,2) NOT NULL,
    humidity_max   DECIMAL(5,2) NOT NULL,
    effective_from DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    effective_to   DATETIME2(3) NULL,           -- NULL = đang hiệu lực
    changed_by     UNIQUEIDENTIFIER NULL CONSTRAINT fk_zth_user REFERENCES dbo.users(id)
);
CREATE UNIQUE INDEX ux_zth_current ON dbo.zone_threshold_history(zone_id) WHERE effective_to IS NULL;

/* 4. sensors (BR03..BR06) */
CREATE TABLE dbo.sensors (
    id                       UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_sensors PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    hardware_id              VARCHAR(64)  NOT NULL CONSTRAINT uq_sensors_hw UNIQUE,
    zone_id                  UNIQUEIDENTIFIER NULL CONSTRAINT fk_sensors_zone REFERENCES dbo.zones(id),
    sensor_type              VARCHAR(30)  NOT NULL CONSTRAINT ck_sensors_type CHECK (sensor_type IN ('TEMPERATURE','HUMIDITY','COMBO')),
    expected_interval_sec    INT          NOT NULL DEFAULT 150
                             CONSTRAINT ck_sensors_interval CHECK (expected_interval_sec BETWEEN 30 AND 3600),  -- [F2]
    stale_threshold_sec      AS (expected_interval_sec * 2) PERSISTED,   -- [F3] BR05
    disconnect_threshold_sec AS (expected_interval_sec * 5) PERSISTED,   -- [F3] BR06
    firmware_version         VARCHAR(30)  NOT NULL DEFAULT 'v1.0.0',
    battery_level            INT          NULL CONSTRAINT ck_sensors_battery CHECK (battery_level BETWEEN 0 AND 100),
    last_rssi                INT          NULL,
    connection_state         VARCHAR(20)  NOT NULL DEFAULT 'NEVER_CONNECTED'
                             CONSTRAINT ck_sensors_conn CHECK (connection_state IN ('NEVER_CONNECTED','CONNECTED','STALE','DISCONNECTED')),
    status                   VARCHAR(30)  NOT NULL DEFAULT 'ONLINE'
                             CONSTRAINT ck_sensors_status CHECK (status IN ('ONLINE','OFFLINE','DEGRADED','CALIBRATING','DECOMMISSIONED')),
    calibration_offset       DECIMAL(5,3) NOT NULL DEFAULT 0,   -- chỉ để tham khảo, KHÔNG áp vào giá trị thô (BR10)
    last_reading_at          DATETIME2(3) NULL,
    last_calibrated_at       DATETIME2(3) NULL,
    next_calibration_due     DATETIME2(3) NULL,
    created_at               DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX ix_sensors_zone_state ON dbo.sensors(zone_id, connection_state);

/* 4b. sensor_assignments [F8] */
CREATE TABLE dbo.sensor_assignments (
    id            BIGINT IDENTITY(1,1) CONSTRAINT pk_sensor_assignments PRIMARY KEY,
    sensor_id     UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_sa_sensor REFERENCES dbo.sensors(id),
    zone_id       UNIQUEIDENTIFIER NULL     CONSTRAINT fk_sa_zone   REFERENCES dbo.zones(id),
    assigned_from DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    assigned_to   DATETIME2(3) NULL,
    assigned_by   UNIQUEIDENTIFIER NULL CONSTRAINT fk_sa_user REFERENCES dbo.users(id)
);
CREATE UNIQUE INDEX ux_sa_current ON dbo.sensor_assignments(sensor_id) WHERE assigned_to IS NULL;

/* 5. readings (thay Hypertable) [F4] */
CREATE TABLE dbo.readings (
    sensor_id     UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_readings_sensor REFERENCES dbo.sensors(id),
    reading_time  DATETIME2(3)  NOT NULL,                 -- thời điểm đo (theo thiết bị)
    zone_id       UNIQUEIDENTIFIER NULL,                  -- snapshot zone tại thời điểm đo
    temperature   DECIMAL(5,2)  NULL,                     -- giá trị THÔ, không chỉnh sửa (BR10)
    humidity      DECIMAL(5,2)  NULL,
    battery_level INT           NULL CONSTRAINT ck_readings_battery CHECK (battery_level BETWEEN 0 AND 100),
    rssi          INT           NULL,
    quality       VARCHAR(20)   NOT NULL DEFAULT 'GOOD'
                  CONSTRAINT ck_readings_quality CHECK (quality IN ('GOOD','SUSPECT_DRIFT','ANOMALOUS_REJECT')),
    received_at   DATETIME2(3)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT pk_readings PRIMARY KEY CLUSTERED (sensor_id, reading_time)
);
CREATE INDEX ix_readings_zone_time ON dbo.readings(zone_id, reading_time) INCLUDE (temperature, humidity, quality);
CREATE INDEX ix_readings_time      ON dbo.readings(reading_time);

/* 6. data_gaps (BR07) [F5] */
CREATE TABLE dbo.data_gaps (
    id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_data_gaps PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    sensor_id    UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_gaps_sensor REFERENCES dbo.sensors(id),
    zone_id      UNIQUEIDENTIFIER NULL,
    start_time   DATETIME2(3) NOT NULL,
    end_time     DATETIME2(3) NULL,
    duration_sec AS (DATEDIFF(SECOND, start_time, end_time)),
    reason       VARCHAR(30)  NOT NULL CONSTRAINT ck_gaps_reason
                 CHECK (reason IN ('SENSOR_OFFLINE','DRIFT_EXCEEDED','LOW_BATTERY','MAINTENANCE','NETWORK_FAILURE','UNKNOWN')),
    created_at   DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT ck_gaps_time CHECK (end_time IS NULL OR end_time >= start_time)
);
CREATE UNIQUE INDEX ux_gaps_one_open ON dbo.data_gaps(sensor_id) WHERE end_time IS NULL;
CREATE INDEX ix_gaps_sensor_time ON dbo.data_gaps(sensor_id, start_time DESC);
CREATE INDEX ix_gaps_zone_time   ON dbo.data_gaps(zone_id, start_time);

/* 7. drift_profiles (BR09, BR10) [F1] */
CREATE TABLE dbo.drift_profiles (
    sensor_id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_dp_sensor REFERENCES dbo.sensors(id),
    metric_type          VARCHAR(20)  NOT NULL CONSTRAINT ck_dp_metric CHECK (metric_type IN ('TEMPERATURE','HUMIDITY')),
    baseline_offset      DECIMAL(6,3) NOT NULL DEFAULT 0,
    cusum_positive       DECIMAL(9,3) NOT NULL DEFAULT 0,
    cusum_negative       DECIMAL(9,3) NOT NULL DEFAULT 0,
    drift_rate_per_hour  DECIMAL(6,3) NOT NULL DEFAULT 0,
    drift_severity       VARCHAR(20)  NOT NULL DEFAULT 'NONE'
                         CONSTRAINT ck_dp_severity CHECK (drift_severity IN ('NONE','MODERATE','CRITICAL','NO_REFERENCE')),
    requires_calibration BIT          NOT NULL DEFAULT 0,
    last_evaluated_at    DATETIME2(3) NULL,
    CONSTRAINT pk_drift_profiles PRIMARY KEY (sensor_id, metric_type)
);

/* 8. drift_history */
CREATE TABLE dbo.drift_history (
    sensor_id           UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_dh_sensor REFERENCES dbo.sensors(id),
    metric_type         VARCHAR(20)  NOT NULL,
    evaluated_at        DATETIME2(3) NOT NULL,
    mean_deviation      DECIMAL(6,3) NOT NULL,
    cusum_positive      DECIMAL(9,3) NOT NULL,
    cusum_negative      DECIMAL(9,3) NOT NULL,
    drift_rate_per_hour DECIMAL(6,3) NOT NULL,
    drift_severity      VARCHAR(20)  NOT NULL,
    sample_count        INT          NOT NULL,
    CONSTRAINT pk_drift_history PRIMARY KEY CLUSTERED (sensor_id, metric_type, evaluated_at)
);

/* 9. alerts (BR12, BR13) [F6] */
CREATE TABLE dbo.alerts (
    id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_alerts PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    zone_id          UNIQUEIDENTIFIER NULL CONSTRAINT fk_alerts_zone   REFERENCES dbo.zones(id),
    sensor_id        UNIQUEIDENTIFIER NULL CONSTRAINT fk_alerts_sensor REFERENCES dbo.sensors(id),
    alert_type       VARCHAR(40)   NOT NULL CONSTRAINT ck_alerts_type CHECK (alert_type IN (
                       'TEMP_OUT_OF_BOUNDS','HUMIDITY_OUT_OF_BOUNDS','SENSOR_STALE','SENSOR_OFFLINE',
                       'LOW_BATTERY','SENSOR_DRIFT','ZONE_EMERGENCY','INSPECTION_FAILED')),
    severity         VARCHAR(20)   NOT NULL CONSTRAINT ck_alerts_severity CHECK (severity IN ('INFO','WARNING','CRITICAL','EMERGENCY')),
    message          NVARCHAR(500) NOT NULL,
    metric_value     DECIMAL(7,2)  NULL,
    threshold_value  DECIMAL(7,2)  NULL,
    status           VARCHAR(20)   NOT NULL DEFAULT 'ACTIVE' CONSTRAINT ck_alerts_status CHECK (status IN ('ACTIVE','ACKNOWLEDGED','RESOLVED')),
    created_at       DATETIME2(3)  NOT NULL DEFAULT SYSUTCDATETIME(),
    last_seen_at     DATETIME2(3)  NOT NULL DEFAULT SYSUTCDATETIME(),   -- lần cuối điều kiện còn vi phạm
    occurrence_count INT           NOT NULL DEFAULT 1,
    acknowledged_at  DATETIME2(3)  NULL,
    acknowledged_by  UNIQUEIDENTIFIER NULL CONSTRAINT fk_alerts_ack REFERENCES dbo.users(id),
    resolved_at      DATETIME2(3)  NULL,
    resolved_by      UNIQUEIDENTIFIER NULL CONSTRAINT fk_alerts_res REFERENCES dbo.users(id),
    resolution_notes NVARCHAR(1000) NULL,
    CONSTRAINT ck_alerts_resolve_notes CHECK (status <> 'RESOLVED' OR (resolution_notes IS NOT NULL AND resolved_at IS NOT NULL))
);
CREATE INDEX ix_alerts_status_created ON dbo.alerts(status, created_at DESC);
/* Chống bão cảnh báo: 1 alert mở cho mỗi (sensor, loại). Alert cấp zone (sensor_id NULL) quản lý ở tầng ứng dụng. */
CREATE UNIQUE INDEX ux_alerts_open ON dbo.alerts(sensor_id, alert_type)
    WHERE status IN ('ACTIVE','ACKNOWLEDGED') AND sensor_id IS NOT NULL;

/* 10. inspections (BR14) */
CREATE TABLE dbo.inspections (
    id               UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_inspections PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    zone_id          UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_insp_zone REFERENCES dbo.zones(id),
    inspector_id     UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_insp_user REFERENCES dbo.users(id),
    inspection_date  DATE          NOT NULL,
    status           VARCHAR(20)   NOT NULL DEFAULT 'COMPLETED'
                     CONSTRAINT ck_insp_status CHECK (status IN ('SCHEDULED','IN_PROGRESS','COMPLETED','CANCELLED')),
    overall_result   VARCHAR(20)   NULL CONSTRAINT ck_insp_result CHECK (overall_result IN ('PASS','FAIL','PARTIAL')),
    notes            NVARCHAR(1000) NULL,
    created_at       DATETIME2(3)  NOT NULL DEFAULT SYSUTCDATETIME()
);

/* 11. inspection_items */
CREATE TABLE dbo.inspection_items (
    id                   UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_inspection_items PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    inspection_id        UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_ii_insp REFERENCES dbo.inspections(id) ON DELETE CASCADE,
    sensor_id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_ii_sensor REFERENCES dbo.sensors(id),
    checklist_item       NVARCHAR(100) NOT NULL,
    reference_value      DECIMAL(5,2)  NULL,
    sensor_reading_value DECIMAL(5,2)  NULL,
    deviation            AS (sensor_reading_value - reference_value),
    result               VARCHAR(20)   NOT NULL CONSTRAINT ck_ii_result CHECK (result IN ('PASS','FAIL','PARTIAL')),
    notes                NVARCHAR(500) NULL
);

/* 12. calibration_records [F12] */
CREATE TABLE dbo.calibration_records (
    id                       UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_calibration_records PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    sensor_id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_cal_sensor REFERENCES dbo.sensors(id),
    technician_id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_cal_user REFERENCES dbo.users(id),
    inspection_id            UNIQUEIDENTIFIER NULL     CONSTRAINT fk_cal_insp REFERENCES dbo.inspections(id),
    calibration_date         DATETIME2(3)  NOT NULL DEFAULT SYSUTCDATETIME(),
    old_offset               DECIMAL(5,3)  NULL,
    new_offset               DECIMAL(5,3)  NOT NULL,
    standard_instrument_info NVARCHAR(150) NOT NULL,
    next_due_date            DATETIME2(3)  NOT NULL,
    notes                    NVARCHAR(500) NULL,
    CONSTRAINT ck_cal_due CHECK (next_due_date > calibration_date)
);

/* 13. reports [F11] */
CREATE TABLE dbo.reports (
    id                      UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_reports PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    warehouse_id            UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_reports_wh REFERENCES dbo.warehouses(id),
    title                   NVARCHAR(150) NOT NULL,
    from_time               DATETIME2(3)  NOT NULL,
    to_time                 DATETIME2(3)  NOT NULL,
    overall_coverage_pct    DECIMAL(5,2)  NULL,
    created_by              UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_reports_user REFERENCES dbo.users(id),
    created_at              DATETIME2(3)  NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT ck_reports_range CHECK (from_time < to_time)
);

/* 14. report_zone_summaries */
CREATE TABLE dbo.report_zone_summaries (
    id                       UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_rzs PRIMARY KEY DEFAULT NEWSEQUENTIALID(),
    report_id                UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_rzs_report REFERENCES dbo.reports(id) ON DELETE CASCADE,
    zone_id                  UNIQUEIDENTIFIER NOT NULL CONSTRAINT fk_rzs_zone REFERENCES dbo.zones(id),
    temp_avg DECIMAL(5,2) NULL, temp_min DECIMAL(5,2) NULL, temp_max DECIMAL(5,2) NULL,
    humidity_avg DECIMAL(5,2) NULL, humidity_min DECIMAL(5,2) NULL, humidity_max DECIMAL(5,2) NULL,
    expected_readings        INT NOT NULL DEFAULT 0,
    valid_readings           INT NOT NULL DEFAULT 0,
    good_readings            INT NOT NULL DEFAULT 0,
    breach_count             INT NOT NULL DEFAULT 0,
    total_gap_duration_sec   INT NOT NULL DEFAULT 0,
    data_coverage_percentage DECIMAL(5,2) NOT NULL
);

/* 15. readings_hourly (thay Continuous Aggregate) */
CREATE TABLE dbo.readings_hourly (
    bucket            DATETIME2(0) NOT NULL,
    sensor_id         UNIQUEIDENTIFIER NOT NULL,
    zone_id           UNIQUEIDENTIFIER NULL,
    avg_temp DECIMAL(5,2) NULL, min_temp DECIMAL(5,2) NULL, max_temp DECIMAL(5,2) NULL,
    avg_humidity DECIMAL(5,2) NULL, min_humidity DECIMAL(5,2) NULL, max_humidity DECIMAL(5,2) NULL,
    good_readings     INT NOT NULL,
    suspect_readings  INT NOT NULL,
    rejected_readings INT NOT NULL,
    total_readings    INT NOT NULL,
    breach_readings   INT NOT NULL,      -- so với ngưỡng có hiệu lực tại thời điểm đo [F7]
    refreshed_at      DATETIME2(3) NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT pk_readings_hourly PRIMARY KEY CLUSTERED (sensor_id, bucket)
);
CREATE INDEX ix_rh_zone_bucket ON dbo.readings_hourly(zone_id, bucket);

/* 16. audit_logs [F9] */
CREATE TABLE dbo.audit_logs (
    id          BIGINT IDENTITY(1,1) CONSTRAINT pk_audit_logs PRIMARY KEY,
    user_id     UNIQUEIDENTIFIER NULL CONSTRAINT fk_audit_user REFERENCES dbo.users(id),
    action      VARCHAR(50)   NOT NULL,
    entity_type VARCHAR(50)   NOT NULL,
    entity_id   VARCHAR(64)   NULL,
    details     NVARCHAR(MAX) NULL,
    created_at  DATETIME2(3)  NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE INDEX ix_audit_entity ON dbo.audit_logs(entity_type, entity_id, created_at DESC);
GO

/* ---------------------------------------------------------------------------
   sp_refresh_readings_hourly: làm mới các bucket giờ trong [@from, @to)
   (tương đương add_continuous_aggregate_policy của TimescaleDB)
   ------------------------------------------------------------------------- */
CREATE PROCEDURE dbo.sp_refresh_readings_hourly
    @from DATETIME2(3),
    @to   DATETIME2(3)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @f DATETIME2(0) = DATEADD(HOUR, DATEDIFF(HOUR, '2000-01-01', @from), CAST('2000-01-01' AS DATETIME2(0)));

    BEGIN TRAN;
    DELETE FROM dbo.readings_hourly WHERE bucket >= @f AND bucket < @to;

    ;WITH src AS (
        SELECT r.*, DATEADD(HOUR, DATEDIFF(HOUR, '2000-01-01', r.reading_time), CAST('2000-01-01' AS DATETIME2(0))) AS bucket,
               CASE WHEN r.quality <> 'ANOMALOUS_REJECT' AND th.zone_id IS NOT NULL AND (
                        r.temperature < th.temp_min OR r.temperature > th.temp_max OR
                        r.humidity < th.humidity_min OR r.humidity > th.humidity_max) THEN 1 ELSE 0 END AS is_breach,
               ROW_NUMBER() OVER (PARTITION BY r.sensor_id, DATEADD(HOUR, DATEDIFF(HOUR, '2000-01-01', r.reading_time), CAST('2000-01-01' AS DATETIME2(0)))
                                  ORDER BY r.reading_time DESC) AS rn
        FROM dbo.readings r
        OUTER APPLY (SELECT TOP 1 h.zone_id, h.temp_min, h.temp_max, h.humidity_min, h.humidity_max
                     FROM dbo.zone_threshold_history h
                     WHERE h.zone_id = r.zone_id AND h.effective_from <= r.reading_time
                       AND (h.effective_to IS NULL OR h.effective_to > r.reading_time)
                     ORDER BY h.effective_from DESC) th
        WHERE r.reading_time >= @f AND r.reading_time < @to
    )
    INSERT INTO dbo.readings_hourly (bucket, sensor_id, zone_id, avg_temp, min_temp, max_temp,
        avg_humidity, min_humidity, max_humidity, good_readings, suspect_readings,
        rejected_readings, total_readings, breach_readings)
    SELECT  bucket, sensor_id,
            MAX(CASE WHEN rn = 1 THEN CAST(zone_id AS VARCHAR(36)) END),
            AVG(CASE WHEN quality <> 'ANOMALOUS_REJECT' THEN temperature END),
            MIN(CASE WHEN quality <> 'ANOMALOUS_REJECT' THEN temperature END),
            MAX(CASE WHEN quality <> 'ANOMALOUS_REJECT' THEN temperature END),
            AVG(CASE WHEN quality <> 'ANOMALOUS_REJECT' THEN humidity END),
            MIN(CASE WHEN quality <> 'ANOMALOUS_REJECT' THEN humidity END),
            MAX(CASE WHEN quality <> 'ANOMALOUS_REJECT' THEN humidity END),
            SUM(CASE WHEN quality = 'GOOD' THEN 1 ELSE 0 END),
            SUM(CASE WHEN quality = 'SUSPECT_DRIFT' THEN 1 ELSE 0 END),
            SUM(CASE WHEN quality = 'ANOMALOUS_REJECT' THEN 1 ELSE 0 END),
            COUNT(*),
            SUM(is_breach)
    FROM src
    GROUP BY bucket, sensor_id;
    COMMIT;
END;
GO

/* sp_apply_retention: thay add_retention_policy. GSP thường yêu cầu lưu >= 5 năm — mặc định 1825 ngày */
CREATE PROCEDURE dbo.sp_apply_retention
    @raw_days    INT = 1825,
    @hourly_days INT = 3650
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.readings        WHERE reading_time < DATEADD(DAY, -@raw_days,    SYSUTCDATETIME());
    DELETE FROM dbo.readings_hourly WHERE bucket       < DATEADD(DAY, -@hourly_days, SYSUTCDATETIME());
END;
GO

/* zone_daily_summary (thay Continuous Aggregate thứ 2) */
CREATE VIEW dbo.v_zone_daily_summary AS
SELECT  CAST(bucket AS DATE) AS bucket_day,
        zone_id,
        CAST(SUM(avg_temp * (good_readings + suspect_readings)) / NULLIF(SUM(CASE WHEN avg_temp IS NOT NULL THEN good_readings + suspect_readings END), 0) AS DECIMAL(5,2)) AS avg_temp,
        MIN(min_temp) AS min_temp,
        MAX(max_temp) AS max_temp,
        CAST(SUM(avg_humidity * (good_readings + suspect_readings)) / NULLIF(SUM(CASE WHEN avg_humidity IS NOT NULL THEN good_readings + suspect_readings END), 0) AS DECIMAL(5,2)) AS avg_humidity,
        MIN(min_humidity) AS min_humidity,
        MAX(max_humidity) AS max_humidity,
        SUM(good_readings)     AS good_readings,
        SUM(suspect_readings)  AS suspect_readings,
        SUM(rejected_readings) AS rejected_readings,
        SUM(total_readings)    AS total_readings,
        SUM(breach_readings)   AS breach_readings
FROM dbo.readings_hourly
GROUP BY CAST(bucket AS DATE), zone_id;
GO
