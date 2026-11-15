using System.Drawing.Drawing2D;
using Dapper;

namespace Iot04.WinForms.Forms;

public class FormZoneDetail : Form
{
    private Panel pnlHeader = null!;
    private Label lblTitle = null!;
    private Label lblZoneMeta = null!;
    private Panel pnlControls = null!;
    private ComboBox cboSensor = null!;
    private ComboBox cboTimeRange = null!;
    private Button btnRefresh = null!;
    private PictureBox picChart = null!;
    private DataGridView dgvReadings = null!;
    private SplitContainer splitContent = null!;

    private sealed class ReadingPoint
    {
        public DateTime Time { get; set; }
        public decimal Temperature { get; set; }
        public string HardwareId { get; set; } = "";
    }

    private sealed class GapRegion
    {
        public DateTime Start { get; set; }
        public DateTime? End { get; set; }
        public string HardwareId { get; set; } = "";
    }

    private List<ReadingPoint> _readings = [];
    private List<GapRegion> _gaps = [];
    private decimal _tempMin = 2.0m;
    private decimal _tempMax = 8.0m;

    public FormZoneDetail()
    {
        InitializeUi();
        this.Load += async (_, _) => await LoadDataAsync();
    }

    private void InitializeUi()
    {
        this.Text = "Chi Tiết Phân Vùng & Biểu Đồ DataGap";
        this.BackColor = StyleHelper.ColorBg;
        this.Font = new Font("Segoe UI", 9F, FontStyle.Regular);

        // 1. Header
        pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = StyleHelper.ColorPanel,
            BorderStyle = BorderStyle.FixedSingle
        };
        lblTitle = new Label
        {
            Text = "CHI TIẾT PHÂN VÙNG: KHO LẠNH VẮC-XIN & SINH PHẨM (ZONE-COLD)",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Location = new Point(16, 10),
            AutoSize = true
        };
        lblZoneMeta = new Label
        {
            Text = "Chuẩn bảo quản GSP: 2.0°C đến 8.0°C | Độ ẩm: 35% đến 65% | 5 Tủ Vắc-xin Giám Sát Độc Lập",
            ForeColor = StyleHelper.ColorTextMuted,
            Location = new Point(17, 36),
            AutoSize = true
        };
        pnlHeader.Controls.AddRange([lblTitle, lblZoneMeta]);

        // 2. Control bar
        pnlControls = new Panel
        {
            Dock = DockStyle.Top,
            Height = 45,
            BackColor = Color.FromArgb(240, 240, 240),
            BorderStyle = BorderStyle.FixedSingle
        };

        var lblFilterSensor = new Label { Text = "Tủ vắc-xin:", Location = new Point(15, 14), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        cboSensor = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(95, 10),
            Width = 180
        };
        cboSensor.Items.AddRange(["Tất cả 5 tủ vắc-xin", "SN-COLD-01-A", "SN-COLD-02-B", "SN-COLD-03-C", "SN-COLD-04-D", "SN-COLD-05-E"]);
        cboSensor.SelectedIndex = 0;
        cboSensor.SelectedIndexChanged += async (_, _) => await LoadDataAsync();

        var lblFilterTime = new Label { Text = "Khoảng thời gian:", Location = new Point(290, 14), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        cboTimeRange = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(410, 10),
            Width = 140
        };
        cboTimeRange.Items.AddRange(["1 Giờ qua", "6 Giờ qua", "24 Giờ qua", "7 Ngày qua"]);
        cboTimeRange.SelectedIndex = 2; // 24h
        cboTimeRange.SelectedIndexChanged += async (_, _) => await LoadDataAsync();

        btnRefresh = StyleHelper.CreateButton("Cập Nhật Biểu Đồ", 150, 28);
        btnRefresh.Location = new Point(570, 8);
        btnRefresh.Click += async (_, _) => await LoadDataAsync();

        pnlControls.Controls.AddRange([lblFilterSensor, cboSensor, lblFilterTime, cboTimeRange, btnRefresh]);

        // 3. Content: Chart on top, Readings table on bottom
        splitContent = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 320,
            BackColor = StyleHelper.ColorBorder
        };

        picChart = new PictureBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White
        };
        picChart.Paint += PicChart_Paint;
        picChart.Resize += (_, _) => picChart.Invalidate();

        var pnlTable = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
        var lblTableTitle = new Label
        {
            Text = "Danh Sách Số Đo Thô Gốc Từ Cảm Biến (Append-Only):",
            Font = new Font("Segoe UI", 9.5F, FontStyle.Bold),
            Dock = DockStyle.Top,
            Height = 28,
            TextAlign = ContentAlignment.BottomLeft
        };
        dgvReadings = new DataGridView { Dock = DockStyle.Fill };
        StyleHelper.ApplyMonochromeGrid(dgvReadings);
        pnlTable.Controls.AddRange([dgvReadings, lblTableTitle]);

        splitContent.Panel1.Controls.Add(picChart);
        splitContent.Panel2.Controls.Add(pnlTable);

        this.Controls.Add(splitContent);
        this.Controls.Add(pnlControls);
        this.Controls.Add(pnlHeader);
    }

    public async Task LoadDataAsync()
    {
        try
        {
            var hours = cboTimeRange.SelectedIndex switch
            {
                0 => 1,
                1 => 6,
                2 => 24,
                3 => 168,
                _ => 24
            };

            var selectedSensor = cboSensor.SelectedItem?.ToString();
            var hwFilter = (selectedSensor == null || selectedSensor.StartsWith("Tất cả")) ? null : selectedSensor;

            await using var conn = await DbHelper.OpenAsync();

            var fromTime = DateTime.UtcNow.AddHours(-hours);

            // 1. Load readings for chart
            var sqlReadings = @"
                SELECT r.reading_time AS [Time], r.temperature AS [Temperature], s.hardware_id AS [HardwareId]
                FROM dbo.readings r
                JOIN dbo.sensors s ON r.sensor_id = s.id
                WHERE r.reading_time >= @fromTime
                  AND (@hwFilter IS NULL OR s.hardware_id = @hwFilter)
                ORDER BY r.reading_time ASC";

            _readings = (await conn.QueryAsync<ReadingPoint>(sqlReadings, new { fromTime, hwFilter })).ToList();

            // 2. Load gaps for Honest Data overlays
            var sqlGaps = @"
                SELECT g.start_time AS [Start], g.end_time AS [End], s.hardware_id AS [HardwareId]
                FROM dbo.data_gaps g
                JOIN dbo.sensors s ON g.sensor_id = s.id
                WHERE (g.end_time IS NULL OR g.end_time >= @fromTime)
                  AND (@hwFilter IS NULL OR s.hardware_id = @hwFilter)";

            _gaps = (await conn.QueryAsync<GapRegion>(sqlGaps, new { fromTime, hwFilter })).ToList();

            // 3. Load tabular readings (top 50)
            var rawList = (await conn.QueryAsync(@"
                SELECT TOP 50
                    s.hardware_id AS [Tủ Vắc-xin],
                    FORMAT(r.reading_time, 'dd/MM/yyyy HH:mm:ss') AS [Thời Điểm Đo],
                    FORMAT(r.temperature, 'N2') + ' °C' AS [Nhiệt Độ Thô],
                    FORMAT(r.humidity, 'N2') + ' %' AS [Độ Ẩm Thô],
                    CAST(r.battery_level AS VARCHAR) + ' %' AS [Mức Pin],
                    CAST(r.rssi AS VARCHAR) + ' dBm' AS [Sóng WiFi],
                    r.quality AS [Chất Lượng Dữ Liệu]
                FROM dbo.readings r
                JOIN dbo.sensors s ON r.sensor_id = s.id
                WHERE (@hwFilter IS NULL OR s.hardware_id = @hwFilter)
                ORDER BY r.reading_time DESC", new { hwFilter })).ToList();

            dgvReadings.DataSource = rawList;

            picChart.Invalidate();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải dữ liệu: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void PicChart_Paint(object? sender, PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var w = picChart.Width;
        var h = picChart.Height;

        // Margins
        int marginLeft = 60;
        int marginRight = 30;
        int marginTop = 40;
        int marginBottom = 45;

        var chartRect = new Rectangle(marginLeft, marginTop, Math.Max(10, w - marginLeft - marginRight), Math.Max(10, h - marginTop - marginBottom));

        // Background
        g.FillRectangle(Brushes.White, chartRect);
        g.DrawRectangle(Pens.Gray, chartRect);

        // Title and Legend
        var fontTitle = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        var fontAxis = new Font("Segoe UI", 8.5F, FontStyle.Regular);
        var fontLegend = new Font("Segoe UI", 8.5F, FontStyle.Bold);

        g.DrawString("BIỂU ĐỒ NHIỆT ĐỘ THỜI GIAN THỰC — TRIẾT LÝ HONEST DATA (KHÔNG NỘI SUY MÙ KHI MẤT TÍN HIỆU)", fontTitle, Brushes.Black, 15, 12);

        // Y-axis range: 0°C to 16°C
        float yMin = 0f;
        float yMax = 16f;

        float YToPixel(float temp)
        {
            var clamped = Math.Clamp(temp, yMin, yMax);
            return chartRect.Bottom - ((clamped - yMin) / (yMax - yMin)) * chartRect.Height;
        }

        // Draw horizontal grid lines & Y labels
        for (int t = 0; t <= 16; t += 2)
        {
            float py = YToPixel(t);
            using var gridPen = new Pen(Color.FromArgb(235, 235, 235));
            g.DrawLine(gridPen, chartRect.Left, py, chartRect.Right, py);
            g.DrawString($"{t}°C", fontAxis, Brushes.Gray, chartRect.Left - 40, py - 7);
        }

        // Draw GSP Threshold lines (2.0°C and 8.0°C) with dashed lines
        using var dashedPen = new Pen(Color.Black, 1.5f) { DashStyle = DashStyle.Dash };
        float pyMin = YToPixel((float)_tempMin);
        float pyMax = YToPixel((float)_tempMax);

        g.DrawLine(dashedPen, chartRect.Left, pyMin, chartRect.Right, pyMin);
        g.DrawLine(dashedPen, chartRect.Left, pyMax, chartRect.Right, pyMax);

        g.DrawString("← Ngưỡng Max: 8.0°C", fontLegend, Brushes.Black, chartRect.Right - 140, pyMax - 16);
        g.DrawString("← Ngưỡng Min: 2.0°C", fontLegend, Brushes.Black, chartRect.Right - 140, pyMin + 2);

        if (_readings.Count == 0)
        {
            g.DrawString("Chưa có số đo trong khoảng thời gian đã chọn", fontTitle, Brushes.Gray, chartRect.Left + 40, chartRect.Top + 60);
            return;
        }

        var minTime = _readings.Min(r => r.Time);
        var maxTime = DateTime.UtcNow;
        var totalSec = Math.Max(1, (maxTime - minTime).TotalSeconds);

        float XToPixel(DateTime dt)
        {
            var sec = (dt - minTime).TotalSeconds;
            return chartRect.Left + (float)(sec / totalSec) * chartRect.Width;
        }

        // 1. Draw DataGap Overlay Regions (Honest Data: shaded gray hatched area)
        foreach (var gap in _gaps)
        {
            var gx1 = XToPixel(gap.Start < minTime ? minTime : gap.Start);
            var gx2 = XToPixel(gap.End ?? maxTime);
            if (gx2 > gx1)
            {
                var gapWidth = Math.Max(4, gx2 - gx1);
                var gapRect = new RectangleF(gx1, chartRect.Top, gapWidth, chartRect.Height);

                using var hatchBrush = new HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(180, 180, 180), Color.FromArgb(245, 245, 245));
                g.FillRectangle(hatchBrush, gapRect);
                g.DrawRectangle(Pens.Gray, gapRect.X, gapRect.Y, gapRect.Width, gapRect.Height);

                if (gapWidth > 50)
                {
                    g.DrawString("[DATA GAP]", fontAxis, Brushes.Black, gapRect.X + 4, chartRect.Top + 10);
                }
            }
        }

        // 2. Draw Reading lines (Per Sensor) — BREAKING LINE OVER GAPS (connectNulls = false)
        var grouped = _readings.GroupBy(r => r.HardwareId);
        foreach (var grp in grouped)
        {
            var points = grp.OrderBy(p => p.Time).ToList();
            using var linePen = new Pen(Color.Black, 2.0f);

            for (int i = 0; i < points.Count - 1; i++)
            {
                var p1 = points[i];
                var p2 = points[i + 1];

                // Nếu khoảng cách giữa 2 điểm đo > 2 lần chu kỳ (ví dụ > 30s) -> KHÔNG NỐI DÂY (Honest Data)
                if ((p2.Time - p1.Time).TotalSeconds > 35)
                {
                    continue; // Bỏ qua, để hở khoảng trống!
                }

                float x1 = XToPixel(p1.Time);
                float y1 = YToPixel((float)p1.Temperature);
                float x2 = XToPixel(p2.Time);
                float y2 = YToPixel((float)p2.Temperature);

                g.DrawLine(linePen, x1, y1, x2, y2);
                g.FillEllipse(Brushes.Black, x1 - 2, y1 - 2, 4, 4);
            }
        }

        // X-axis time labels
        g.DrawString(minTime.ToString("HH:mm:ss"), fontAxis, Brushes.Black, chartRect.Left, chartRect.Bottom + 6);
        g.DrawString(maxTime.ToString("HH:mm:ss"), fontAxis, Brushes.Black, chartRect.Right - 50, chartRect.Bottom + 6);
    }
}
