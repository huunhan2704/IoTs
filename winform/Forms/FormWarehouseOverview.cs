using Dapper;

namespace Iot04.WinForms.Forms;

public class FormWarehouseOverview : Form
{
    private Panel pnlHeader = null!;
    private Label lblTitle = null!;
    private Label lblDesc = null!;
    private Panel pnlCards = null!;
    private Label lblStatZones = null!;
    private Label lblStatSensors = null!;
    private Label lblStatAlerts = null!;
    private Label lblStatCondition = null!;
    private Label lblZoneGridTitle = null!;
    private DataGridView dgvZones = null!;
    private Label lblSensorGridTitle = null!;
    private DataGridView dgvSensors = null!;
    private Panel pnlActions = null!;
    private Button btnViewZoneDetail = null!;
    private Button btnRefresh = null!;

    public event Action? RequestOpenZoneDetail;

    public FormWarehouseOverview()
    {
        InitializeUi();
        this.Load += async (_, _) => await RefreshDataAsync();
    }

    private void InitializeUi()
    {
        this.Text = "Tổng Quan Phân Vùng Kho";
        this.BackColor = StyleHelper.ColorBg;
        this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        // 1. Header
        pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = StyleHelper.ColorPanel,
            BorderStyle = BorderStyle.FixedSingle
        };
        lblTitle = new Label
        {
            Text = "TỔNG QUAN PHÂN VÙNG KHO & VI KHÍ HẬU LƯU TRỮ",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Location = new Point(16, 10),
            AutoSize = true
        };
        lblDesc = new Label
        {
            Text = "Giám sát thời gian thực Kho Trung Tâm Logistics Tân Bình (WH-HCM-01) — Kho Lạnh Vắc-xin & Sinh phẩm",
            ForeColor = StyleHelper.ColorTextMuted,
            Location = new Point(17, 34),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange([lblTitle, lblDesc]);

        // 2. Summary cards (Monochrome)
        pnlCards = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle
        };

        lblStatZones = new Label
        {
            Text = "Phân vùng kho: 1 (ZONE-COLD)",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(20, 22),
            AutoSize = true
        };
        lblStatSensors = new Label
        {
            Text = "Cảm biến trực tuyến: Đang tải...",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(260, 22),
            AutoSize = true
        };
        lblStatAlerts = new Label
        {
            Text = "Cảnh báo Active: 0",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(540, 22),
            AutoSize = true
        };
        lblStatCondition = new Label
        {
            Text = "Tình trạng GSP: BÌNH THƯỜNG",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Location = new Point(780, 22),
            AutoSize = true
        };
        pnlCards.Controls.AddRange([lblStatZones, lblStatSensors, lblStatAlerts, lblStatCondition]);

        // 3. Action bar
        pnlActions = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 50,
            BackColor = StyleHelper.ColorPanel,
            BorderStyle = BorderStyle.FixedSingle
        };
        btnViewZoneDetail = StyleHelper.CreateButton("Xem Chi Tiết Phân Vùng & Biểu Đồ DataGap >", 340, 34);
        btnViewZoneDetail.Location = new Point(16, 8);
        btnViewZoneDetail.Click += (_, _) => RequestOpenZoneDetail?.Invoke();

        btnRefresh = StyleHelper.CreateButton("Làm Mới Số Liệu", 140, 34);
        btnRefresh.Location = new Point(370, 8);
        btnRefresh.Click += async (_, _) => await RefreshDataAsync();

        pnlActions.Controls.AddRange([btnViewZoneDetail, btnRefresh]);

        // 4. Content split: Upper is Zone summary, Lower is Sensor list
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 140,
            BackColor = StyleHelper.ColorBorder
        };

        var pnlUpper = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        lblZoneGridTitle = new Label
        {
            Text = "1. Bảng Thông Tin Phân Vùng Kho & Ngưỡng An Toàn:",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.BottomLeft
        };
        dgvZones = new DataGridView { Dock = DockStyle.Fill };
        StyleHelper.ApplyMonochromeGrid(dgvZones);
        pnlUpper.Controls.AddRange([dgvZones, lblZoneGridTitle]);

        var pnlLower = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        lblSensorGridTitle = new Label
        {
            Text = "2. Trạng Thái Chi Tiết 5 Tủ Bảo Quản Vắc-Xin Trong Vùng (Cảm Biến IoT):",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.BottomLeft
        };
        dgvSensors = new DataGridView { Dock = DockStyle.Fill };
        StyleHelper.ApplyMonochromeGrid(dgvSensors);
        pnlLower.Controls.AddRange([dgvSensors, lblSensorGridTitle]);

        split.Panel1.Controls.Add(pnlUpper);
        split.Panel2.Controls.Add(pnlLower);

        this.Controls.Add(split);
        this.Controls.Add(pnlActions);
        this.Controls.Add(pnlCards);
        this.Controls.Add(pnlHeader);
    }

    public async Task RefreshDataAsync()
    {
        try
        {
            await using var conn = await DbHelper.OpenAsync();

            // Load zones summary
            var zones = (await conn.QueryAsync(@"
                SELECT 
                    z.code AS [Mã Phân Vùng],
                    z.name AS [Tên Phân Vùng],
                    z.zone_type AS [Loại Kho Lưu Trữ],
                    FORMAT(z.temp_min, 'N1') + ' - ' + FORMAT(z.temp_max, 'N1') + ' °C' AS [Ngưỡng Nhiệt Độ Chuẩn],
                    FORMAT(z.humidity_min, 'N0') + ' - ' + FORMAT(z.humidity_max, 'N0') + ' %' AS [Ngưỡng Độ Ẩm Chuẩn],
                    COUNT(s.id) AS [Tổng Cảm Biến],
                    SUM(CASE WHEN s.connection_state = 'CONNECTED' THEN 1 ELSE 0 END) AS [Đang Online],
                    ISNULL(FORMAT(AVG(r.temperature), 'N2') + ' °C', '--') AS [Nhiệt Độ TB],
                    ISNULL(FORMAT(AVG(r.humidity), 'N2') + ' %', '--') AS [Độ Ẩm TB]
                FROM dbo.zones z
                LEFT JOIN dbo.sensors s ON s.zone_id = z.id
                OUTER APPLY (
                    SELECT TOP 1 temperature, humidity 
                    FROM dbo.readings 
                    WHERE sensor_id = s.id 
                    ORDER BY reading_time DESC
                ) r
                GROUP BY z.code, z.name, z.zone_type, z.temp_min, z.temp_max, z.humidity_min, z.humidity_max
            ")).ToList();
            dgvZones.DataSource = zones;

            // Load live sensors status
            var sensors = (await conn.QueryAsync(@"
                SELECT 
                    hardware_id AS [Mã Cảm Biến / Tủ Vắc-xin],
                    zone_name AS [Phân Vùng Gán],
                    FORMAT(latest_temperature, 'N2') + ' °C' AS [Nhiệt Độ Hiện Tại],
                    FORMAT(latest_humidity, 'N2') + ' %' AS [Độ Ẩm Hiện Tại],
                    CAST(battery_level AS VARCHAR) + ' %' AS [Mức Pin],
                    CAST(rssi AS VARCHAR) + ' dBm' AS [Sóng WiFi],
                    CAST(data_age_seconds AS VARCHAR) + 's' AS [Tuổi Dữ Liệu],
                    data_freshness AS [Tình Trạng (Freshness)],
                    CASE 
                        WHEN condition_status = 'NORMAL' THEN 'Bình thường (2-8°C)'
                        WHEN condition_status = 'EXCURSION' THEN '[!] VƯỢT NGƯỠNG'
                        ELSE 'Chưa có dữ liệu'
                    END AS [Đánh Giá Chuẩn GSP],
                    connection_state AS [Trạng Thái Kết Nối]
                FROM dbo.v_sensor_live_status
                ORDER BY hardware_id ASC
            ")).ToList();
            dgvSensors.DataSource = sensors;

            // Update stats
            var totalSensors = sensors.Count;
            var onlineSensors = sensors.Count(s => (string)((IDictionary<string, object>)s)["Trạng Thái Kết Nối"] == "CONNECTED");
            var alertsCount = await conn.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM dbo.alerts WHERE status = 'ACTIVE'");

            lblStatSensors.Text = $"Cảm biến trực tuyến: {onlineSensors} / {totalSensors}";
            lblStatAlerts.Text = $"Cảnh báo Active: {alertsCount}";
            lblStatCondition.Text = alertsCount > 0 ? "[!] CÓ CẢNH BÁO VI PHẠM" : "Tình trạng GSP: ĐẠT TIÊU CHUẨN";
        }
        catch (Exception ex)
        {
            lblStatCondition.Text = $"Lỗi đọc CSDL: {ex.Message}";
        }
    }
}
