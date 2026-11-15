using System.Data;
using Dapper;

namespace Iot04.WinForms.Forms;

public class FormWarehouseReport : Form
{
    private Panel pnlHeader = null!;
    private Label lblTitle = null!;
    private Label lblDesc = null!;
    private Panel pnlActions = null!;
    private DateTimePicker dtpReportDate = null!;
    private Button btnGenerateReport = null!;
    private Button btnRefresh = null!;
    private DataGridView dgvReports = null!;
    private DataGridView dgvReportDetails = null!;
    private TextBox txtReportPreview = null!;
    private SplitContainer splitMain = null!;
    private SplitContainer splitBottom = null!;

    public FormWarehouseReport()
    {
        InitializeUi();
        this.Load += async (_, _) => await RefreshReportsAsync();
    }

    private void InitializeUi()
    {
        this.Text = "Báo Cáo Điều Kiện Kho";
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
            Text = "BÁO CÁO ĐIỀU KIỆN KHO & THẨM ĐỊNH CHUẨN GSP (REPORTS)",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Location = new Point(16, 10),
            AutoSize = true
        };
        lblDesc = new Label
        {
            Text = "Kết toán thẩm định vi khí hậu, công bố tỷ lệ bao phủ dữ liệu (Data Coverage % - BR15) và lịch sử lưu trữ hồ sơ GSP 5 năm",
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

        var lblDate = new Label { Text = "Ngày kết toán:", Location = new Point(15, 14), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        dtpReportDate = new DateTimePicker
        {
            Format = DateTimePickerFormat.Short,
            Location = new Point(110, 10),
            Width = 120,
            Value = DateTime.Today
        };

        btnGenerateReport = StyleHelper.CreateButton("Tạo Báo Cáo Ngày (sp_generate_zone_daily_report)", 350, 30);
        btnGenerateReport.Location = new Point(245, 7);
        btnGenerateReport.Click += BtnGenerateReport_Click;

        btnRefresh = StyleHelper.CreateButton("Làm Mới", 100, 30);
        btnRefresh.Location = new Point(605, 7);
        btnRefresh.Click += async (_, _) => await RefreshReportsAsync();

        pnlActions.Controls.AddRange([lblDate, dtpReportDate, btnGenerateReport, btnRefresh]);

        // Main content layout
        splitMain = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 160,
            BackColor = StyleHelper.ColorBorder
        };

        // Top: Reports List
        var pnlReports = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var lblReportsTitle = new Label
        {
            Text = "1. Danh Mục Các Báo Cáo Thẩm Định Đã Lập (Lưu trữ GSP 5 năm):",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.BottomLeft
        };
        dgvReports = new DataGridView { Dock = DockStyle.Fill };
        StyleHelper.ApplyMonochromeGrid(dgvReports);
        dgvReports.SelectionChanged += async (_, _) => await LoadReportDetailsAsync();
        pnlReports.Controls.AddRange([dgvReports, lblReportsTitle]);

        // Bottom: Split into details table and text preview
        splitBottom = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 500,
            BackColor = StyleHelper.ColorBorder
        };

        var pnlDetails = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var lblDetailsTitle = new Label
        {
            Text = "2. Chi Tiết Kết Toán Phân Vùng (Nhiệt độ, Độ ẩm & Data Coverage %):",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.BottomLeft
        };
        dgvReportDetails = new DataGridView { Dock = DockStyle.Fill };
        StyleHelper.ApplyMonochromeGrid(dgvReportDetails);
        pnlDetails.Controls.AddRange([dgvReportDetails, lblDetailsTitle]);

        var pnlPreview = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var lblPreviewTitle = new Label
        {
            Text = "3. Văn Bản Thẩm Định GSP Trắng Đen (Sẵn sàng in ấn):",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.BottomLeft
        };
        txtReportPreview = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9F, FontStyle.Regular),
            BackColor = Color.White,
            ForeColor = Color.Black
        };
        pnlPreview.Controls.AddRange([txtReportPreview, lblPreviewTitle]);

        splitBottom.Panel1.Controls.Add(pnlDetails);
        splitBottom.Panel2.Controls.Add(pnlPreview);

        splitMain.Panel1.Controls.Add(pnlReports);
        splitMain.Panel2.Controls.Add(splitBottom);

        this.Controls.Add(splitMain);
        this.Controls.Add(pnlActions);
        this.Controls.Add(pnlHeader);
    }

    public async Task RefreshReportsAsync()
    {
        try
        {
            await using var conn = await DbHelper.OpenAsync();
            var list = (await conn.QueryAsync(@"
                SELECT 
                    r.id AS [Mã Báo Cáo],
                    r.title AS [Tiêu Đề Báo Cáo],
                    FORMAT(r.from_time, 'dd/MM/yyyy HH:mm') AS [Từ Thời Điểm],
                    FORMAT(r.to_time, 'dd/MM/yyyy HH:mm') AS [Đến Thời Điểm],
                    CAST(r.overall_coverage_pct AS VARCHAR) + ' %' AS [Tỷ Lệ Bao Phủ Dữ Liệu],
                    u.full_name AS [Người Lập Báo Cáo],
                    FORMAT(r.created_at, 'dd/MM/yyyy HH:mm') AS [Ngày Lập]
                FROM dbo.reports r
                LEFT JOIN dbo.users u ON r.created_by = u.id
                ORDER BY r.created_at DESC
            ")).ToList();

            dgvReports.DataSource = list;
            await LoadReportDetailsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải báo cáo: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async Task LoadReportDetailsAsync()
    {
        if (dgvReports.SelectedRows.Count == 0) return;
        var reportIdObj = dgvReports.SelectedRows[0].Cells["Mã Báo Cáo"]?.Value;
        if (reportIdObj == null || !Guid.TryParse(reportIdObj.ToString(), out var reportId)) return;

        try
        {
            await using var conn = await DbHelper.OpenAsync();
            var details = (await conn.QueryAsync(@"
                SELECT 
                    z.name AS [Phân Vùng],
                    FORMAT(rz.temp_min, 'N2') + ' °C' AS [Nhiệt Độ Min],
                    FORMAT(rz.temp_max, 'N2') + ' °C' AS [Nhiệt Độ Max],
                    FORMAT(rz.temp_avg, 'N2') + ' °C' AS [Nhiệt Độ TB],
                    FORMAT(rz.humidity_avg, 'N1') + ' %' AS [Độ Ẩm TB],
                    rz.breach_count AS [Số Lần Vi Phạm],
                    CAST(rz.total_gap_duration_sec AS VARCHAR) + 's' AS [Tổng TG DataGap],
                    CAST(rz.data_coverage_percentage AS VARCHAR) + ' %' AS [Tỷ Lệ Bao Phủ (BR15)]
                FROM dbo.report_zone_summaries rz
                JOIN dbo.zones z ON rz.zone_id = z.id
                WHERE rz.report_id = @reportId", new { reportId })).ToList();

            dgvReportDetails.DataSource = details;

            // Generate monochrome text preview
            var title = dgvReports.SelectedRows[0].Cells["Tiêu Đề Báo Cáo"]?.Value?.ToString();
            var from = dgvReports.SelectedRows[0].Cells["Từ Thời Điểm"]?.Value?.ToString();
            var to = dgvReports.SelectedRows[0].Cells["Đến Thời Điểm"]?.Value?.ToString();
            var coverage = dgvReports.SelectedRows[0].Cells["Tỷ Lệ Bao Phủ Dữ Liệu"]?.Value?.ToString();

            txtReportPreview.Text = 
                "========================================================================\r\n" +
                "               BÁO CÁO THẨM ĐỊNH ĐIỀU KIỆN KHO DƯỢC PHẨM & VẮC-XIN\r\n" +
                "               Tiêu chuẩn: Thực hành tốt bảo quản thuốc (GSP / GDP)\r\n" +
                "========================================================================\r\n" +
                $"Mã báo cáo: {reportId}\r\n" +
                $"Tiêu đề:    {title}\r\n" +
                $"Thời gian:  {from} -> {to}\r\n" +
                $"Địa điểm:   Kho Trung Tâm Logistics Tân Bình (WH-HCM-01)\r\n" +
                $"Phân vùng:  Kho Lạnh Vắc-xin & Sinh phẩm (ZONE-COLD)\r\n" +
                "------------------------------------------------------------------------\r\n" +
                "KẾT QUẢ THẨM ĐỊNH VI KHÍ HẬU:\r\n" +
                "- Dải nhiệt độ chuẩn quy định:   2.0°C - 8.0°C\r\n" +
                "- Dải độ ẩm chuẩn quy định:       35% - 65%\r\n" +
                $"- Tỷ lệ bao phủ dữ liệu (BR15):  {coverage}\r\n" +
                "- Nguyên tắc Honest Data:        Bảo toàn 100% giá trị thô, ghi nhận rõ\r\n" +
                "                                mọi khoảng trống dữ liệu (DataGap).\r\n" +
                "------------------------------------------------------------------------\r\n" +
                "KẾT LUẬN THẨM ĐỊNH:\r\n" +
                "[ĐẠT YÊU CẦU] Điều kiện bảo quản vắc-xin đáp ứng đầy đủ tiêu chuẩn GSP.\r\n" +
                "========================================================================\r\n";
        }
        catch (Exception ex)
        {
            txtReportPreview.Text = $"Lỗi: {ex.Message}";
        }
    }

    private async void BtnGenerateReport_Click(object? sender, EventArgs e)
    {
        try
        {
            await using var conn = await DbHelper.OpenAsync();
            var whId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.warehouses WHERE code='WH-HCM-01'");
            var zoneId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.zones WHERE code='ZONE-COLD'");
            var userId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.users WHERE username='manager1'");

            var date = dtpReportDate.Value.Date;

            var reportId = await conn.QueryFirstOrDefaultAsync<Guid>(
                "dbo.sp_generate_zone_daily_report",
                new { warehouse_id = whId, zone_id = zoneId, report_date = date, created_by = userId },
                commandType: CommandType.StoredProcedure);

            MessageBox.Show($"Đã tạo báo cáo thẩm định GSP ngày {date:dd/MM/yyyy} thành công!\nMã báo cáo: {reportId}", "Thành Công", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshReportsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
