/* ============================================================================
   IOT-04 — DỮ LIỆU KHỞI TẠO MẪU (SEED DATA) cho SQL Server
   Mật khẩu mặc định của mọi tài khoản: 123456
   (password_hash = 'RESET_ON_STARTUP' -> backend băm PBKDF2 khi khởi động lần đầu)
   ============================================================================ */
USE IOT04_WarehouseMonitor;
GO
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
GO
SET NOCOUNT ON;

INSERT INTO dbo.users (id, username, password_hash, full_name, email, role) VALUES
('A0000000-0000-0000-0000-000000000000', 'admin',     'RESET_ON_STARTUP', N'Quản Trị Hệ Thống',   'admin@logistics.com',    'ADMIN'),
('A0000000-0000-0000-0000-000000000001', 'manager1',  'RESET_ON_STARTUP', N'Nguyễn Văn Quản Lý',  'manager@logistics.com',  'WAREHOUSE_MANAGER'),
('A0000000-0000-0000-0000-000000000002', 'operator1', 'RESET_ON_STARTUP', N'Trần Thị Giám Sát',   'operator@logistics.com', 'MONITORING_OPERATOR'),
('A0000000-0000-0000-0000-000000000003', 'tech1',     'RESET_ON_STARTUP', N'Lê Văn Kỹ Thuật',     'tech@logistics.com',     'TECHNICIAN');

INSERT INTO dbo.warehouses (id, code, name, address, capacity_m2, description) VALUES
('B0000000-0000-0000-0000-000000000001', 'WH-HCM-01', N'Kho Trung Tâm Logistics Tân Bình',
 N'KCN Tân Bình, Tây Thạnh, Tân Phú, TP. Hồ Chí Minh', 12000,
 N'Kho tổng phân phối dược phẩm và chuỗi cung ứng thực phẩm tươi sống');

INSERT INTO dbo.zones (id, warehouse_id, code, name, zone_type, temp_min, temp_max, humidity_min, humidity_max) VALUES
('C0000000-0000-0000-0000-000000000001', 'B0000000-0000-0000-0000-000000000001', 'ZONE-COLD', N'Kho Lạnh Vắc-xin & Sinh phẩm',   'COLD_STORAGE',    2, 8, 35, 65),
('C0000000-0000-0000-0000-000000000002', 'B0000000-0000-0000-0000-000000000001', 'ZONE-COOL', N'Kho Mát Dược Liệu & Thuốc Viên', 'COOL_STORAGE',   15, 25, 40, 70),
('C0000000-0000-0000-0000-000000000003', 'B0000000-0000-0000-0000-000000000001', 'ZONE-DOCK', N'Dock Tiếp Nhận Hàng Hoá',        'RECEIVING_DOCK', 10, 30, 30, 80),
('C0000000-0000-0000-0000-000000000004', 'B0000000-0000-0000-0000-000000000001', 'ZONE-DRY',  N'Kho Khô Bao Bì',                 'DRY_STORAGE',    15, 30, 30, 60);

/* Lịch sử ngưỡng ban đầu (hiệu lực từ 30 ngày trước) */
INSERT INTO dbo.zone_threshold_history (zone_id, temp_min, temp_max, humidity_min, humidity_max, effective_from, changed_by)
SELECT id, temp_min, temp_max, humidity_min, humidity_max, DATEADD(DAY, -30, SYSUTCDATETIME()), 'A0000000-0000-0000-0000-000000000001'
FROM dbo.zones;

INSERT INTO dbo.sensors (id, hardware_id, zone_id, sensor_type, expected_interval_sec, firmware_version, battery_level, created_at) VALUES
('D0000000-0000-0000-0000-000000000001', 'SN-COLD-01-A', 'C0000000-0000-0000-0000-000000000001', 'COMBO', 150, 'v2.1.0', 95, DATEADD(DAY,-30,SYSUTCDATETIME())),
('D0000000-0000-0000-0000-000000000002', 'SN-COLD-02-B', 'C0000000-0000-0000-0000-000000000001', 'COMBO', 150, 'v2.1.0', 88, DATEADD(DAY,-30,SYSUTCDATETIME())),
('D0000000-0000-0000-0000-000000000003', 'SN-COOL-01-A', 'C0000000-0000-0000-0000-000000000002', 'COMBO', 150, 'v2.0.3', 78, DATEADD(DAY,-30,SYSUTCDATETIME())),
('D0000000-0000-0000-0000-000000000004', 'SN-COOL-02-B', 'C0000000-0000-0000-0000-000000000002', 'COMBO', 150, 'v2.0.3', 18, DATEADD(DAY,-30,SYSUTCDATETIME())),
('D0000000-0000-0000-0000-000000000005', 'SN-DOCK-01-A', 'C0000000-0000-0000-0000-000000000003', 'COMBO', 150, 'v2.1.0', 92, DATEADD(DAY,-30,SYSUTCDATETIME())),
('D0000000-0000-0000-0000-000000000006', 'SN-DOCK-02-B', 'C0000000-0000-0000-0000-000000000003', 'COMBO', 150, 'v1.9.8', 40, DATEADD(DAY,-30,SYSUTCDATETIME())),
('D0000000-0000-0000-0000-000000000007', 'SN-DRY-01-A',  'C0000000-0000-0000-0000-000000000004', 'COMBO', 150, 'v2.1.0', 99, DATEADD(DAY,-30,SYSUTCDATETIME())),
('D0000000-0000-0000-0000-000000000008', 'SN-COLD-03-C', 'C0000000-0000-0000-0000-000000000001', 'COMBO', 150, 'v2.1.0', 97, DATEADD(DAY,-30,SYSUTCDATETIME()));

INSERT INTO dbo.sensor_assignments (sensor_id, zone_id, assigned_from, assigned_by)
SELECT id, zone_id, created_at, 'A0000000-0000-0000-0000-000000000003' FROM dbo.sensors;

INSERT INTO dbo.drift_profiles (sensor_id, metric_type)
SELECT s.id, m.metric FROM dbo.sensors s
CROSS JOIN (VALUES ('TEMPERATURE'), ('HUMIDITY')) m(metric);

/* ----------------------------------------------------------------------------
   Lịch sử đo 24 giờ qua (576 bản ghi/cảm biến @150s) để dashboard & báo cáo có dữ liệu:
   - SN-DOCK-02-B: mất kết nối 23 phút (08:12 -> 08:35 theo khung giờ tương đối) -> DataGap thật
   - SN-COLD-02-B: lệch dần +0.15°C/giờ trong 6 giờ cuối -> CUSUM sẽ phát hiện MODERATE
   ------------------------------------------------------------------------- */
DECLARE @now DATETIME2(3) = DATEADD(SECOND, -60, SYSUTCDATETIME());
;WITH n AS (
    SELECT TOP (576) ROW_NUMBER() OVER (ORDER BY (SELECT NULL)) - 1 AS i
    FROM sys.all_objects a CROSS JOIN sys.all_objects b
),
base AS (
    SELECT s.id AS sensor_id, s.zone_id, s.hardware_id, n.i,
           DATEADD(SECOND, -150 * n.i, @now) AS t,
           CASE s.zone_id
                WHEN 'C0000000-0000-0000-0000-000000000001' THEN 5.0
                WHEN 'C0000000-0000-0000-0000-000000000002' THEN 20.0
                WHEN 'C0000000-0000-0000-0000-000000000003' THEN 24.0
                ELSE 22.0 END AS t_base,
           CASE s.zone_id
                WHEN 'C0000000-0000-0000-0000-000000000001' THEN 50.0
                WHEN 'C0000000-0000-0000-0000-000000000002' THEN 55.0
                WHEN 'C0000000-0000-0000-0000-000000000003' THEN 60.0
                ELSE 45.0 END AS h_base
    FROM dbo.sensors s CROSS JOIN n
)
INSERT INTO dbo.readings (sensor_id, reading_time, zone_id, temperature, humidity, battery_level, rssi, quality, received_at)
SELECT sensor_id, t, zone_id,
       CAST(t_base + 1.2 * SIN(i / 45.0) + (ABS(CHECKSUM(NEWID())) % 30 - 15) / 100.0
            + CASE WHEN hardware_id = 'SN-COLD-02-B' AND i < 144 THEN (144 - i) * 0.15 / 24.0 ELSE 0 END AS DECIMAL(5,2)),
       CAST(h_base + 4 * SIN(i / 60.0) + (ABS(CHECKSUM(NEWID())) % 100 - 50) / 100.0 AS DECIMAL(5,2)),
       90, -60 - ABS(CHECKSUM(NEWID())) % 15, 'GOOD', DATEADD(MILLISECOND, 300, t)
FROM base
WHERE NOT (hardware_id = 'SN-DOCK-02-B' AND i BETWEEN 300 AND 309);   -- khoảng mất kết nối ~25 phút

/* DataGap đã đóng tương ứng với khoảng mất kết nối ở trên */
INSERT INTO dbo.data_gaps (sensor_id, zone_id, start_time, end_time, reason)
SELECT 'D0000000-0000-0000-0000-000000000006', 'C0000000-0000-0000-0000-000000000003',
       DATEADD(SECOND, -150 * 310, @now), DATEADD(SECOND, -150 * 299, @now), 'NETWORK_FAILURE';

UPDATE s SET last_reading_at = r.mx, connection_state = 'CONNECTED'
FROM dbo.sensors s JOIN (SELECT sensor_id, MAX(reading_time) mx FROM dbo.readings GROUP BY sensor_id) r ON r.sensor_id = s.id;

DECLARE @from DATETIME2(3) = DATEADD(DAY, -2, SYSUTCDATETIME()), @to DATETIME2(3) = DATEADD(HOUR, 1, SYSUTCDATETIME());
EXEC dbo.sp_refresh_readings_hourly @from, @to;

INSERT INTO dbo.calibration_records (sensor_id, technician_id, calibration_date, old_offset, new_offset, standard_instrument_info, next_due_date, notes)
VALUES ('D0000000-0000-0000-0000-000000000001', 'A0000000-0000-0000-0000-000000000003', DATEADD(DAY,-60,SYSUTCDATETIME()), 0, 0.05,
        N'Nhiệt kế chuẩn Fluke 1524 (SN 2219-A)', DATEADD(DAY,120,SYSUTCDATETIME()), N'Hiệu chuẩn định kỳ 6 tháng');
UPDATE dbo.sensors SET last_calibrated_at = DATEADD(DAY,-60,SYSUTCDATETIME()), next_calibration_due = DATEADD(DAY,120,SYSUTCDATETIME()), calibration_offset = 0.05
WHERE id = 'D0000000-0000-0000-0000-000000000001';

SELECT (SELECT COUNT(*) FROM dbo.readings) AS readings, (SELECT COUNT(*) FROM dbo.readings_hourly) AS hourly_rows;
GO
