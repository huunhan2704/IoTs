using Dapper;

namespace Iot04.WinForms.Forms;

public class FormInspectionCalibration : Form
{
    private Panel pnlHeader = null!;
    private Label lblTitle = null!;
    private Label lblDesc = null!;
    private TabControl tabs = null!;
    private TabPage tabInspections = null!;
    private TabPage tabCalibrations = null!;

    // Inspections
    private DataGridView dgvInspections = null!;
    private Panel pnlInspActions = null!;
    private Button btnNewInspection = null!;
    private Button btnRefreshInsp = null!;

    // Calibrations
    private DataGridView dgvCalibrations = null!;
    private Panel pnlCalibActions = null!;
    private Button btnNewCalibration = null!;
    private Button btnRefreshCalib = null!;

    public FormInspectionCalibration()
    {
        InitializeUi();
        this.Load += async (_, _) => await RefreshAllAsync();
    }

    private void InitializeUi()
    {
        this.Text = "Kiểm Tra & Hiệu Chuẩn Thiết Bị";
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
            Text = "KIỂM TRA ĐỊNH KỲ VÀ HIỆU CHUẨN THIẾT BỊ (INSPECTION & CALIBRATION)",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Location = new Point(16, 10),
            AutoSize = true
        };
        lblDesc = new Label
        {
            Text = "Quản lý hồ sơ thẩm định GSP, biên bản kiểm tra vi khí hậu kho và lịch sử hiệu chuẩn sai số cảm biến",
            ForeColor = StyleHelper.ColorTextMuted,
            Location = new Point(17, 34),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange([lblTitle, lblDesc]);

        // Tabs
        tabs = new TabControl { Dock = DockStyle.Fill, Font = new Font("Segoe UI", 9.5F, FontStyle.Regular) };

        tabInspections = new TabPage { Text = " 1. Biên Bản Kiểm Tra Định Kỳ (Inspections) ", BackColor = Color.White };
        tabCalibrations = new TabPage { Text = " 2. Hồ Sơ Hiệu Chuẩn Cảm Biến (Calibration Records) ", BackColor = Color.White };

        tabs.Controls.AddRange([tabInspections, tabCalibrations]);

        // --- SUBTAB 1: INSPECTIONS ---
        pnlInspActions = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle
        };
        btnNewInspection = StyleHelper.CreateButton("+ Lập Biên Bản Kiểm Tra Mới", 230, 30);
        btnNewInspection.Location = new Point(15, 7);
        btnNewInspection.Click += BtnNewInspection_Click;

        btnRefreshInsp = StyleHelper.CreateButton("Làm Mới", 100, 30);
        btnRefreshInsp.Location = new Point(255, 7);
        btnRefreshInsp.Click += async (_, _) => await RefreshInspectionsAsync();

        pnlInspActions.Controls.AddRange([btnNewInspection, btnRefreshInsp]);

        dgvInspections = new DataGridView { Dock = DockStyle.Fill };
        StyleHelper.ApplyMonochromeGrid(dgvInspections);

        tabInspections.Controls.AddRange([dgvInspections, pnlInspActions]);

        // --- SUBTAB 2: CALIBRATIONS ---
        pnlCalibActions = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle
        };
        btnNewCalibration = StyleHelper.CreateButton("+ Ghi Nhận Hiệu Chuẩn Cảm Biến", 250, 30);
        btnNewCalibration.Location = new Point(15, 7);
        btnNewCalibration.Click += BtnNewCalibration_Click;

        btnRefreshCalib = StyleHelper.CreateButton("Làm Mới", 100, 30);
        btnRefreshCalib.Location = new Point(275, 7);
        btnRefreshCalib.Click += async (_, _) => await RefreshCalibrationsAsync();

        pnlCalibActions.Controls.AddRange([btnNewCalibration, btnRefreshCalib]);

        dgvCalibrations = new DataGridView { Dock = DockStyle.Fill };
        StyleHelper.ApplyMonochromeGrid(dgvCalibrations);

        tabCalibrations.Controls.AddRange([dgvCalibrations, pnlCalibActions]);

        this.Controls.Add(tabs);
        this.Controls.Add(pnlHeader);
    }

    public async Task RefreshAllAsync()
    {
        await RefreshInspectionsAsync();
        await RefreshCalibrationsAsync();
    }

    public async Task RefreshInspectionsAsync()
    {
        try
        {
            await using var conn = await DbHelper.OpenAsync();
            var list = (await conn.QueryAsync(@"
                SELECT 
                    i.id AS [Mã Biên Bản],
                    z.name AS [Phân Vùng Kiểm Tra],
                    FORMAT(i.inspection_date, 'dd/MM/yyyy') AS [Ngày Kiểm Tra],
                    i.overall_result AS [Kết Quả Đánh Giá],
                    i.status AS [Trạng Thái],
                    u.full_name AS [Kiểm Định Viên],
                    i.notes AS [Ghi Chú Kết Luận],
                    FORMAT(i.created_at, 'dd/MM/yyyy HH:mm') AS [Thời Điểm Lập]
                FROM dbo.inspections i
                LEFT JOIN dbo.zones z ON i.zone_id = z.id
                LEFT JOIN dbo.users u ON i.inspector_id = u.id
                ORDER BY i.created_at DESC
            ")).ToList();
            dgvInspections.DataSource = list;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải biên bản kiểm tra: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    public async Task RefreshCalibrationsAsync()
    {
        try
        {
            await using var conn = await DbHelper.OpenAsync();
            var list = (await conn.QueryAsync(@"
                SELECT 
                    c.id AS [Mã Hiệu Chuẩn],
                    s.hardware_id AS [Tủ Vắc-xin],
                    FORMAT(c.calibration_date, 'dd/MM/yyyy') AS [Ngày Hiệu Chuẩn],
                    c.standard_instrument_info AS [Thiết Bị Chuẩn Đối Chiếu],
                    FORMAT(c.old_offset, 'N3') + ' °C' AS [Sai Số Cũ],
                    FORMAT(c.new_offset, 'N3') + ' °C' AS [Sai Số Mới (Offset)],
                    FORMAT(c.next_due_date, 'dd/MM/yyyy') AS [Hạn Hiệu Chuẩn Tiếp],
                    u.full_name AS [Kỹ Thuật Viên],
                    c.notes AS [Ghi Chú Kỹ Thuật]
                FROM dbo.calibration_records c
                JOIN dbo.sensors s ON c.sensor_id = s.id
                LEFT JOIN dbo.users u ON c.technician_id = u.id
                ORDER BY c.calibration_date DESC
            ")).ToList();
            dgvCalibrations.DataSource = list;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải hồ sơ hiệu chuẩn: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnNewInspection_Click(object? sender, EventArgs e)
    {
        using var prompt = new Form
        {
            Width = 460,
            Height = 240,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Lập Biên Bản Kiểm Tra Định Kỳ Mới",
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.White
        };

        var lblResult = new Label { Text = "Kết quả đánh giá:", Left = 20, Top = 15, Width = 400 };
        var cboResult = new ComboBox { Left = 20, Top = 38, Width = 400, DropDownStyle = ComboBoxStyle.DropDownList };
        cboResult.Items.AddRange(["PASS (ĐẠT CHUẨN GSP)", "FAIL (KHÔNG ĐẠT)"]);
        cboResult.SelectedIndex = 0;

        var lblNote = new Label { Text = "Ghi chú kết luận kiểm định:", Left = 20, Top = 75, Width = 400 };
        var txtNote = new TextBox { Left = 20, Top = 98, Width = 400, Text = "Hệ thống cảm biến và điều kiện nhiệt độ kho lạnh hoạt động đạt chuẩn GSP." };

        var btn = StyleHelper.CreateButton("Lưu Biên Bản", 140, 32);
        btn.Location = new Point(280, 145);
        btn.DialogResult = DialogResult.OK;

        prompt.Controls.AddRange([lblResult, cboResult, lblNote, txtNote, btn]);
        if (prompt.ShowDialog() == DialogResult.OK)
        {
            var res = cboResult.SelectedIndex == 0 ? "PASS" : "FAIL";
            var note = txtNote.Text.Trim();
            try
            {
                await using var conn = await DbHelper.OpenAsync();
                var zoneId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.zones WHERE code='ZONE-COLD'");
                var inspectorId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.users WHERE username='tech1'");

                await conn.ExecuteAsync(@"
                    INSERT INTO dbo.inspections (zone_id, inspector_id, inspection_date, status, overall_result, notes)
                    VALUES (@zoneId, @inspectorId, CAST(GETUTCDATE() AS DATE), 'COMPLETED', @res, @note)",
                    new { zoneId, inspectorId, res, note });

                MessageBox.Show("Đã lưu biên bản kiểm tra định kỳ thành công!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await RefreshInspectionsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void BtnNewCalibration_Click(object? sender, EventArgs e)
    {
        using var prompt = new Form
        {
            Width = 460,
            Height = 300,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Ghi Nhận Hiệu Chuẩn Cảm Biến",
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.White
        };

        var lblSensor = new Label { Text = "Chọn tủ vắc-xin cần hiệu chuẩn:", Left = 20, Top = 15, Width = 400 };
        var cboSensor = new ComboBox { Left = 20, Top = 38, Width = 400, DropDownStyle = ComboBoxStyle.DropDownList };
        cboSensor.Items.AddRange(["SN-COLD-01-A", "SN-COLD-02-B", "SN-COLD-03-C", "SN-COLD-04-D", "SN-COLD-05-E"]);
        cboSensor.SelectedIndex = 0;

        var lblInstrument = new Label { Text = "Thiết bị đo chuẩn đối chiếu:", Left = 20, Top = 75, Width = 400 };
        var txtInstrument = new TextBox { Left = 20, Top = 98, Width = 400, Text = "NIST Calibrated Fluke 1524" };

        var lblOffset = new Label { Text = "Độ lệch hiệu chuẩn mới (Offset °C):", Left = 20, Top = 135, Width = 400 };
        var txtOffset = new TextBox { Left = 20, Top = 158, Width = 400, Text = "0.050" };

        var btn = StyleHelper.CreateButton("Lưu Hiệu Chuẩn", 160, 32);
        btn.Location = new Point(260, 210);
        btn.DialogResult = DialogResult.OK;

        prompt.Controls.AddRange([lblSensor, cboSensor, lblInstrument, txtInstrument, lblOffset, txtOffset, btn]);
        if (prompt.ShowDialog() == DialogResult.OK)
        {
            var hw = cboSensor.SelectedItem?.ToString() ?? "SN-COLD-01-A";
            var instrument = txtInstrument.Text.Trim();
            _ = decimal.TryParse(txtOffset.Text, out var newOffset);

            try
            {
                await using var conn = await DbHelper.OpenAsync();
                var sensor = await conn.QueryFirstOrDefaultAsync<dynamic>("SELECT id, calibration_offset FROM dbo.sensors WHERE hardware_id=@hw", new { hw });
                var techId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.users WHERE username='tech1'");

                if (sensor != null)
                {
                    Guid sId = sensor.id;
                    decimal oldOffset = sensor.calibration_offset ?? 0m;

                    await conn.ExecuteAsync(@"
                        INSERT INTO dbo.calibration_records (sensor_id, technician_id, calibration_date, old_offset, new_offset, standard_instrument_info, next_due_date, notes)
                        VALUES (@sId, @techId, SYSUTCDATETIME(), @oldOffset, @newOffset, @instrument, DATEADD(YEAR, 1, SYSUTCDATETIME()), N'Hiệu chuẩn định kỳ GSP hàng năm');
                        
                        UPDATE dbo.sensors 
                        SET calibration_offset = @newOffset, last_calibrated_at = SYSUTCDATETIME(), next_calibration_due = DATEADD(YEAR, 1, SYSUTCDATETIME())
                        WHERE id = @sId;",
                        new { sId, techId, oldOffset, newOffset, instrument });

                    MessageBox.Show($"Đã ghi nhận hiệu chuẩn cho tủ {hw} thành công!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    await RefreshCalibrationsAsync();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
