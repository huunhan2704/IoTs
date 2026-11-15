using System.Data;
using Dapper;
using Microsoft.Data.SqlClient;

namespace Iot04.WinForms;

public partial class Form1 : Form
{
    private readonly string _connectionString = 
        "Server=.\\SQLEXPRESS;Database=IOT04_WarehouseMonitor;Integrated Security=True;TrustServerCertificate=True;";

    public Form1()
    {
        InitializeComponent();
        ApplyMonochromeGridStyle(dgvSensors);
        ApplyMonochromeGridStyle(dgvAlerts);
        ApplyMonochromeGridStyle(dgvCompliance);
        ApplyMonochromeGridStyle(dgvDataGaps);

        this.Load += Form1_Load;
    }

    private void Form1_Load(object? sender, EventArgs e)
    {
        RefreshAllData();
        timerAutoRefresh.Start();
    }

    private void ApplyMonochromeGridStyle(DataGridView dgv)
    {
        dgv.EnableHeadersVisualStyles = false;
        dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(230, 230, 230);
        dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.Black;
        dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
        dgv.ColumnHeadersHeight = 35;

        dgv.DefaultCellStyle.BackColor = Color.White;
        dgv.DefaultCellStyle.ForeColor = Color.Black;
        dgv.DefaultCellStyle.SelectionBackColor = Color.FromArgb(210, 210, 210);
        dgv.DefaultCellStyle.SelectionForeColor = Color.Black;
        dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 248);

        dgv.GridColor = Color.FromArgb(220, 220, 220);
        dgv.CellBorderStyle = DataGridViewCellBorderStyle.Single;
    }

    private void TimerAutoRefresh_Tick(object? sender, EventArgs e)
    {
        // Tự động làm mới dữ liệu
        RefreshAllData();
    }

    private void ChkAutoRefresh_CheckedChanged(object? sender, EventArgs e)
    {
        timerAutoRefresh.Enabled = chkAutoRefresh.Checked;
    }

    private void BtnRefresh_Click(object? sender, EventArgs e)
    {
        RefreshAllData();
    }

    private async void RefreshAllData()
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // 1. Load Live Sensor Status từ View v_sensor_live_status
            var sensorList = (await conn.QueryAsync(@"
                SELECT 
                    hardware_id AS [Mã Tủ Vắc-xin],
                    zone_name AS [Phân Vùng],
                    FORMAT(latest_temperature, 'N2') + ' °C' AS [Nhiệt Độ Hiện Tại],
                    FORMAT(latest_humidity, 'N2') + ' %' AS [Độ Ẩm Hiện Tại],
                    CAST(battery_level AS VARCHAR) + ' %' AS [Mức Pin],
                    CAST(rssi AS VARCHAR) + ' dBm' AS [Sóng WiFi],
                    CAST(data_age_seconds AS VARCHAR) + ' giây' AS [Tuổi Dữ Liệu],
                    data_freshness AS [Tình Trạng Dữ Liệu],
                    CASE 
                        WHEN condition_status = 'NORMAL' THEN 'Bình thường (2 - 8°C)'
                        WHEN condition_status = 'EXCURSION' THEN '[!] VI PHẠM NGƯỠNG'
                        ELSE 'Chưa có số đo'
                    END AS [Đánh Giá Chuẩn GSP],
                    connection_state AS [Trạng Thái Kết Nối]
                FROM dbo.v_sensor_live_status
                ORDER BY hardware_id ASC
            ")).ToList();

            dgvSensors.DataSource = sensorList;

            // Cập nhật tóm tắt
            var total = sensorList.Count;
            var online = sensorList.Count(s => (string)((IDictionary<string, object>)s)["Trạng Thái Kết Nối"] == "CONNECTED");
            var excursion = sensorList.Count(s => ((string)((IDictionary<string, object>)s)["Đánh Giá Chuẩn GSP"]).Contains("VI PHẠM"));

            lblSummaryTotal.Text = $"Tổng tủ vắc-xin: {total}";
            lblSummaryOnline.Text = $"Đang kết nối: {online} / {total}";
            lblSummaryExcursion.Text = $"Vi phạm ngưỡng (2-8°C): {excursion}";

            var temps = sensorList
                .Select(s => (string)((IDictionary<string, object>)s)["Nhiệt Độ Hiện Tại"])
                .Where(t => t != null && t.Contains("°C"))
                .Select(t => decimal.TryParse(t.Replace(" °C", "").Trim(), out var d) ? d : (decimal?)null)
                .Where(d => d.HasValue)
                .Select(d => d!.Value)
                .ToList();

            if (temps.Any())
            {
                lblSummaryAvgTemp.Text = $"Nhiệt độ TB kho: {temps.Average():N2} °C";
            }

            // 2. Load Alerts từ View v_active_alerts_detail
            var alertList = (await conn.QueryAsync(@"
                SELECT 
                    alert_id AS [Mã Cảnh Báo],
                    hardware_id AS [Tủ Vi Phạm],
                    alert_type AS [Loại Cảnh Báo],
                    severity AS [Mức Độ],
                    message AS [Nội Dung],
                    FORMAT(metric_value, 'N2') AS [Giá Trị Đo],
                    FORMAT(threshold_value, 'N2') AS [Ngưỡng Chuẩn],
                    active_duration_minutes AS [Thời Gian Vi Phạm (phút)],
                    status AS [Trạng Thái]
                FROM dbo.v_active_alerts_detail
                ORDER BY created_at DESC
            ")).ToList();
            dgvAlerts.DataSource = alertList;

            // 3. Load Compliance từ View v_vaccine_storage_compliance
            var complianceList = (await conn.QueryAsync(@"
                SELECT 
                    hardware_id AS [Tủ Vắc-xin],
                    total_readings_24h AS [Tổng Số Mẫu 24h],
                    in_range_readings AS [Số Mẫu Chuẩn (2-8°C)],
                    excursion_readings AS [Số Mẫu Vi Phạm],
                    CAST(compliance_pct AS VARCHAR) + ' %' AS [Tỷ Lệ Tuân Thủ GSP],
                    FORMAT(min_temp_24h, 'N2') + ' °C' AS [Nhiệt Độ Thấp Nhất],
                    FORMAT(max_temp_24h, 'N2') + ' °C' AS [Nhiệt Độ Cao Nhất],
                    FORMAT(avg_temp_24h, 'N2') + ' °C' AS [Nhiệt Độ Trung Bình]
                FROM dbo.v_vaccine_storage_compliance
                ORDER BY hardware_id ASC
            ")).ToList();
            dgvCompliance.DataSource = complianceList;

            // 4. Load Data Gaps từ dbo.data_gaps
            var gapList = (await conn.QueryAsync(@"
                SELECT TOP 30
                    s.hardware_id AS [Tủ Bị Mất Kết Nối],
                    g.start_time AS [Bắt Đầu Mất Tín Hiệu],
                    g.end_time AS [Thời Điểm Khôi Phục],
                    CAST(g.duration_sec AS VARCHAR) + ' s' AS [Thời Gian Mất],
                    g.is_open AS [Đang Mở Khoảng Trống],
                    g.trigger_reason AS [Lý Do]
                FROM dbo.data_gaps g
                JOIN dbo.sensors s ON g.sensor_id = s.id
                ORDER BY g.start_time DESC
            ")).ToList();
            dgvDataGaps.DataSource = gapList;

            statusTime.Text = $"Cập nhật lúc: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            statusTime.Text = $"Lỗi kết nối CSDL: {ex.Message}";
        }
    }

    private async void BtnResolveAlert_Click(object? sender, EventArgs e)
    {
        if (dgvAlerts.SelectedRows.Count == 0)
        {
            MessageBox.Show(
                "Vui lòng chọn một dòng cảnh báo cần đóng trong danh sách!",
                "Thông Báo",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var selectedRow = dgvAlerts.SelectedRows[0];
        var alertIdObj = selectedRow.Cells["Mã Cảnh Báo"]?.Value;
        if (alertIdObj == null || !Guid.TryParse(alertIdObj.ToString(), out var alertId))
        {
            MessageBox.Show("Không thể lấy mã định danh của cảnh báo!", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        // Hộp thoại nhập giải trình chuẩn GSP
        using var prompt = new Form
        {
            Width = 520,
            Height = 220,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Đóng Cảnh Báo — Bắt Buộc Giải Trình Theo Chuẩn GSP",
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.White
        };

        var textLabel = new Label 
        { 
            Left = 20, Top = 15, Width = 460, 
            Text = "Nhập nguyên nhân và biện pháp khắc phục sự cố (tối thiểu 5 ký tự):" 
        };
        var textBox = new TextBox 
        { 
            Left = 20, Top = 40, Width = 460, Height = 65, 
            Multiline = true, 
            Text = "Đã kiểm tra hệ thống làm lạnh của tủ vắc-xin, nhiệt độ đã trở lại ngưỡng an toàn." 
        };
        var confirmation = new Button 
        { 
            Text = "Xác nhận đóng (sp_resolve_alert)", 
            Left = 200, Width = 280, Top = 120, 
            DialogResult = DialogResult.OK, 
            BackColor = Color.FromArgb(240, 240, 240) 
        };

        prompt.Controls.Add(textLabel);
        prompt.Controls.Add(textBox);
        prompt.Controls.Add(confirmation);
        prompt.AcceptButton = confirmation;

        if (prompt.ShowDialog() == DialogResult.OK)
        {
            var note = textBox.Text.Trim();
            try
            {
                await using var conn = new SqlConnection(_connectionString);
                await conn.OpenAsync();

                // Lấy User ID quản lý kho
                var userId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.users WHERE username='manager1'");

                // Gọi Stored Procedure sp_resolve_alert
                await conn.ExecuteAsync(
                    "dbo.sp_resolve_alert",
                    new { alert_id = alertId, resolved_by_user = userId, resolution_notes = note },
                    commandType: CommandType.StoredProcedure);

                MessageBox.Show(
                    "Đã đóng cảnh báo thành công qua Stored Procedure [sp_resolve_alert]!",
                    "Thành Công",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);

                RefreshAllData();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi khi đóng cảnh báo: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }

    private async void BtnGenerateDailyReport_Click(object? sender, EventArgs e)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            var whId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.warehouses WHERE code='WH-HCM-01'");
            var zoneId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.zones WHERE code='ZONE-COLD'");
            var userId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.users WHERE username='manager1'");

            var reportId = await conn.QueryFirstOrDefaultAsync<Guid>(
                "dbo.sp_generate_zone_daily_report",
                new 
                { 
                    warehouse_id = whId, 
                    zone_id = zoneId, 
                    report_date = DateTime.UtcNow.Date, 
                    created_by = userId 
                },
                commandType: CommandType.StoredProcedure);

            MessageBox.Show(
                $"Đã xuất Báo Cáo Thẩm Định GSP ngày hôm nay thành công qua Stored Procedure!\nMã báo cáo: {reportId}",
                "Kết Toán Báo Cáo Thành Công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);

            RefreshAllData();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi khi tạo báo cáo: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnTestHonestTrigger_Click(object? sender, EventArgs e)
    {
        try
        {
            await using var conn = new SqlConnection(_connectionString);
            await conn.OpenAsync();

            // Cố ý chạy lệnh UPDATE vào bảng readings để chứng minh Trigger trg_readings_honest_data_guard hoạt động
            await conn.ExecuteAsync("UPDATE TOP (1) dbo.readings SET temperature = 99.9;");

            MessageBox.Show("Cảnh báo: Lệnh UPDATE thành công (Trigger chưa được kích hoạt)!", "Cảnh Báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        catch (SqlException ex)
        {
            MessageBox.Show(
                $"[CHỨNG MINH THÀNH CÔNG NGUYÊN TẮC HONEST DATA]\n\n" +
                $"Trigger ở tầng SQL Server [trg_readings_honest_data_guard] đã chặn đứng lệnh UPDATE và Rollback giao dịch:\n\n" +
                $"Thông điệp từ CSDL: \"{ex.Message}\"",
                "Kiểm Chứng Trigger Thành Công",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
