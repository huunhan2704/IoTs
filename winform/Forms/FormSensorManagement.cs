using Dapper;

namespace Iot04.WinForms.Forms;

public class FormSensorManagement : Form
{
    private Panel pnlHeader = null!;
    private Label lblTitle = null!;
    private Label lblDesc = null!;
    private Panel pnlActions = null!;
    private Button btnAddSensor = null!;
    private Button btnUpdateInterval = null!;
    private Button btnToggleStatus = null!;
    private Button btnRefresh = null!;
    private DataGridView dgvSensors = null!;

    public FormSensorManagement()
    {
        InitializeUi();
        this.Load += async (_, _) => await RefreshSensorsAsync();
    }

    private void InitializeUi()
    {
        this.Text = "Quản Lý Cảm Biến";
        this.BackColor = StyleHelper.ColorBg;
        this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        // Header
        pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = StyleHelper.ColorPanel,
            BorderStyle = BorderStyle.FixedSingle
        };
        lblTitle = new Label
        {
            Text = "QUẢN LÝ DANH MỤC THIẾT BỊ CẢM BIẾN IOT (SENSORS)",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Location = new Point(16, 10),
            AutoSize = true
        };
        lblDesc = new Label
        {
            Text = "Cấu hình chu kỳ lấy mẫu, trạng thái vòng đời thiết bị, theo dõi dung lượng pin và độ lệch hiệu chuẩn",
            ForeColor = StyleHelper.ColorTextMuted,
            Location = new Point(17, 34),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange([lblTitle, lblDesc]);

        // Actions
        pnlActions = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle
        };

        btnAddSensor = StyleHelper.CreateButton("+ Thêm Cảm Biến Mới", 160, 30);
        btnAddSensor.Location = new Point(15, 7);
        btnAddSensor.Click += BtnAddSensor_Click;

        btnUpdateInterval = StyleHelper.CreateButton("Sửa Chu Kỳ Đo (Interval)", 180, 30);
        btnUpdateInterval.Location = new Point(185, 7);
        btnUpdateInterval.Click += BtnUpdateInterval_Click;

        btnToggleStatus = StyleHelper.CreateButton("Đổi Trạng Thái (Bảo Trì)", 180, 30);
        btnToggleStatus.Location = new Point(375, 7);
        btnToggleStatus.Click += BtnToggleStatus_Click;

        btnRefresh = StyleHelper.CreateButton("Làm Mới", 100, 30);
        btnRefresh.Location = new Point(565, 7);
        btnRefresh.Click += async (_, _) => await RefreshSensorsAsync();

        pnlActions.Controls.AddRange([btnAddSensor, btnUpdateInterval, btnToggleStatus, btnRefresh]);

        // Grid
        dgvSensors = new DataGridView { Dock = DockStyle.Fill };
        StyleHelper.ApplyMonochromeGrid(dgvSensors);

        this.Controls.Add(dgvSensors);
        this.Controls.Add(pnlActions);
        this.Controls.Add(pnlHeader);
    }

    public async Task RefreshSensorsAsync()
    {
        try
        {
            await using var conn = await DbHelper.OpenAsync();
            var list = (await conn.QueryAsync(@"
                SELECT 
                    s.hardware_id AS [Mã Cảm Biến],
                    z.name AS [Phân Vùng Gán],
                    s.sensor_type AS [Loại Cảm Biến],
                    s.status AS [Trạng Thái Hoạt Động],
                    s.connection_state AS [Kết Nối FSM],
                    CAST(s.expected_interval_sec AS VARCHAR) + 's' AS [Chu Kỳ Đo],
                    CAST(s.battery_level AS VARCHAR) + ' %' AS [Mức Pin],
                    CAST(s.last_rssi AS VARCHAR) + ' dBm' AS [Sóng WiFi],
                    FORMAT(s.calibration_offset, 'N3') + ' °C' AS [Offset Hiệu Chuẩn],
                    FORMAT(s.last_reading_at, 'dd/MM/yyyy HH:mm:ss') AS [Số Đo Gần Nhất],
                    FORMAT(s.next_calibration_due, 'dd/MM/yyyy') AS [Hạn Hiệu Chuẩn Kế]
                FROM dbo.sensors s
                LEFT JOIN dbo.zones z ON s.zone_id = z.id
                ORDER BY s.hardware_id ASC
            ")).ToList();

            dgvSensors.DataSource = list;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải danh mục cảm biến: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnAddSensor_Click(object? sender, EventArgs e)
    {
        using var prompt = new Form
        {
            Width = 420,
            Height = 260,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Thêm Cảm Biến Mới",
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.White
        };

        var lblHw = new Label { Text = "Mã Hardware ID (VD: SN-COLD-06-F):", Left = 20, Top = 15, Width = 360 };
        var txtHw = new TextBox { Left = 20, Top = 38, Width = 360, Text = "SN-COLD-06-F" };

        var lblInterval = new Label { Text = "Chu kỳ gửi số đo kỳ vọng (30 - 3600 giây):", Left = 20, Top = 75, Width = 360 };
        var txtInterval = new TextBox { Left = 20, Top = 98, Width = 360, Text = "60" };

        var btnOk = StyleHelper.CreateButton("Lưu Cảm Biến", 150, 32);
        btnOk.Location = new Point(230, 160);
        btnOk.DialogResult = DialogResult.OK;

        prompt.Controls.AddRange([lblHw, txtHw, lblInterval, txtInterval, btnOk]);
        prompt.AcceptButton = btnOk;

        if (prompt.ShowDialog() == DialogResult.OK)
        {
            var hw = txtHw.Text.Trim();
            if (string.IsNullOrEmpty(hw)) return;
            _ = int.TryParse(txtInterval.Text, out var interval);
            if (interval < 30) interval = 30;

            try
            {
                await using var conn = await DbHelper.OpenAsync();
                var zoneId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.zones WHERE code='ZONE-COLD'");

                await conn.ExecuteAsync(@"
                    INSERT INTO dbo.sensors (hardware_id, zone_id, sensor_type, expected_interval_sec, status, connection_state)
                    VALUES (@hw, @zoneId, 'COMBO', @interval, 'ONLINE', 'NEVER_CONNECTED')",
                    new { hw, zoneId, interval });

                MessageBox.Show($"Đã đăng ký cảm biến {hw} thành công!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await RefreshSensorsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void BtnUpdateInterval_Click(object? sender, EventArgs e)
    {
        if (dgvSensors.SelectedRows.Count == 0) return;
        var hw = dgvSensors.SelectedRows[0].Cells["Mã Cảm Biến"]?.Value?.ToString();
        if (string.IsNullOrEmpty(hw)) return;

        using var prompt = new Form
        {
            Width = 380,
            Height = 180,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = $"Sửa Chu Kỳ Đo: {hw}",
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.White
        };

        var lbl = new Label { Text = "Chu kỳ đo mới (giây):", Left = 20, Top = 20, Width = 320 };
        var txt = new TextBox { Left = 20, Top = 45, Width = 320, Text = "60" };
        var btn = StyleHelper.CreateButton("Cập Nhật", 120, 30);
        btn.Location = new Point(220, 90);
        btn.DialogResult = DialogResult.OK;

        prompt.Controls.AddRange([lbl, txt, btn]);
        if (prompt.ShowDialog() == DialogResult.OK && int.TryParse(txt.Text, out var newInterval))
        {
            if (newInterval < 30) newInterval = 30;
            await using var conn = await DbHelper.OpenAsync();
            await conn.ExecuteAsync("UPDATE dbo.sensors SET expected_interval_sec = @newInterval WHERE hardware_id = @hw", new { newInterval, hw });
            await RefreshSensorsAsync();
        }
    }

    private async void BtnToggleStatus_Click(object? sender, EventArgs e)
    {
        if (dgvSensors.SelectedRows.Count == 0) return;
        var hw = dgvSensors.SelectedRows[0].Cells["Mã Cảm Biến"]?.Value?.ToString();
        var currentStatus = dgvSensors.SelectedRows[0].Cells["Trạng Thái Hoạt Động"]?.Value?.ToString();
        if (string.IsNullOrEmpty(hw)) return;

        var nextStatus = currentStatus == "CALIBRATING" ? "ONLINE" : "CALIBRATING";

        await using var conn = await DbHelper.OpenAsync();
        await conn.ExecuteAsync("UPDATE dbo.sensors SET status = @nextStatus WHERE hardware_id = @hw", new { nextStatus, hw });
        MessageBox.Show($"Đã chuyển cảm biến {hw} sang trạng thái: {nextStatus}", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        await RefreshSensorsAsync();
    }
}
