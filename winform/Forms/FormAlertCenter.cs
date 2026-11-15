using System.Data;
using Dapper;

namespace Iot04.WinForms.Forms;

public class FormAlertCenter : Form
{
    private Panel pnlHeader = null!;
    private Label lblTitle = null!;
    private Label lblDesc = null!;
    private Panel pnlActions = null!;
    private ComboBox cboStatusFilter = null!;
    private Button btnAcknowledge = null!;
    private Button btnResolve = null!;
    private Button btnRefresh = null!;
    private DataGridView dgvAlerts = null!;

    public FormAlertCenter()
    {
        InitializeUi();
        this.Load += async (_, _) => await RefreshAlertsAsync();
    }

    private void InitializeUi()
    {
        this.Text = "Trung Tâm Xử Lý Cảnh Báo";
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
            Text = "TRUNG TÂM XỬ LÝ & ĐIỀU PHỐI CẢNH BÁO VI PHẠM (ALERT CENTER)",
            Font = new Font("Segoe UI", 12F, FontStyle.Bold),
            Location = new Point(16, 10),
            AutoSize = true
        };
        lblDesc = new Label
        {
            Text = "Tiếp nhận, xử lý sự cố vượt ngưỡng nhiệt độ/độ ẩm, mất tín hiệu cảm biến và sai số trôi dạt (CUSUM Drift)",
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

        var lblFilter = new Label { Text = "Lọc trạng thái:", Location = new Point(15, 14), AutoSize = true, Font = new Font("Segoe UI", 9F, FontStyle.Bold) };
        cboStatusFilter = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Location = new Point(105, 10),
            Width = 160
        };
        cboStatusFilter.Items.AddRange(["Cảnh báo Đang Mở (Active/Ack)", "Tất Cả Cảnh Báo", "Chỉ ACTIVE", "Chỉ ACKNOWLEDGED", "Chỉ RESOLVED"]);
        cboStatusFilter.SelectedIndex = 0;
        cboStatusFilter.SelectedIndexChanged += async (_, _) => await RefreshAlertsAsync();

        btnAcknowledge = StyleHelper.CreateButton("Tiếp Nhận (Acknowledge)", 180, 30);
        btnAcknowledge.Location = new Point(280, 7);
        btnAcknowledge.Click += BtnAcknowledge_Click;

        btnResolve = StyleHelper.CreateButton("Đóng Cảnh Báo (sp_resolve_alert)", 240, 30);
        btnResolve.Location = new Point(470, 7);
        btnResolve.Click += BtnResolve_Click;

        btnRefresh = StyleHelper.CreateButton("Làm Mới", 100, 30);
        btnRefresh.Location = new Point(720, 7);
        btnRefresh.Click += async (_, _) => await RefreshAlertsAsync();

        pnlActions.Controls.AddRange([lblFilter, cboStatusFilter, btnAcknowledge, btnResolve, btnRefresh]);

        // Grid
        dgvAlerts = new DataGridView { Dock = DockStyle.Fill };
        StyleHelper.ApplyMonochromeGrid(dgvAlerts);

        this.Controls.Add(dgvAlerts);
        this.Controls.Add(pnlActions);
        this.Controls.Add(pnlHeader);
    }

    public async Task RefreshAlertsAsync()
    {
        try
        {
            await using var conn = await DbHelper.OpenAsync();

            var filterMode = cboStatusFilter.SelectedIndex;
            var sql = @"
                SELECT 
                    a.id AS [Mã Cảnh Báo],
                    s.hardware_id AS [Tủ Vi Phạm],
                    a.alert_type AS [Loại Cảnh Báo],
                    a.severity AS [Mức Độ],
                    a.message AS [Nội Dung],
                    FORMAT(a.metric_value, 'N2') AS [Số Đo],
                    FORMAT(a.threshold_value, 'N2') AS [Ngưỡng Chuẩn],
                    FORMAT(a.created_at, 'dd/MM/yyyy HH:mm') AS [Thời Điểm Tạo],
                    DATEDIFF(MINUTE, a.created_at, SYSUTCDATETIME()) AS [Thời Gian Mở (phút)],
                    a.occurrence_count AS [Số Lần Vi Phạm],
                    a.status AS [Trạng Thái],
                    u.full_name AS [Người Xử Lý],
                    a.resolution_notes AS [Ghi Chú Giải Trình GSP]
                FROM dbo.alerts a
                LEFT JOIN dbo.sensors s ON a.sensor_id = s.id
                LEFT JOIN dbo.users u ON a.resolved_by = u.id OR a.acknowledged_by = u.id
                WHERE 
                    (@filterMode = 0 AND a.status IN ('ACTIVE','ACKNOWLEDGED'))
                 OR (@filterMode = 1)
                 OR (@filterMode = 2 AND a.status = 'ACTIVE')
                 OR (@filterMode = 3 AND a.status = 'ACKNOWLEDGED')
                 OR (@filterMode = 4 AND a.status = 'RESOLVED')
                ORDER BY a.created_at DESC";

            var list = (await conn.QueryAsync(sql, new { filterMode })).ToList();
            dgvAlerts.DataSource = list;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi tải danh sách cảnh báo: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnAcknowledge_Click(object? sender, EventArgs e)
    {
        if (dgvAlerts.SelectedRows.Count == 0) return;
        var alertIdObj = dgvAlerts.SelectedRows[0].Cells["Mã Cảnh Báo"]?.Value;
        if (alertIdObj == null || !Guid.TryParse(alertIdObj.ToString(), out var alertId)) return;

        try
        {
            await using var conn = await DbHelper.OpenAsync();
            var userId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.users WHERE username='operator1'");

            await conn.ExecuteAsync(@"
                UPDATE dbo.alerts 
                SET status = 'ACKNOWLEDGED', acknowledged_at = SYSUTCDATETIME(), acknowledged_by = @userId 
                WHERE id = @alertId AND status = 'ACTIVE'", new { alertId, userId });

            MessageBox.Show("Đã tiếp nhận cảnh báo thành công!", "Thông Báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            await RefreshAlertsAsync();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private async void BtnResolve_Click(object? sender, EventArgs e)
    {
        if (dgvAlerts.SelectedRows.Count == 0) return;
        var alertIdObj = dgvAlerts.SelectedRows[0].Cells["Mã Cảnh Báo"]?.Value;
        if (alertIdObj == null || !Guid.TryParse(alertIdObj.ToString(), out var alertId)) return;

        using var prompt = new Form
        {
            Width = 500,
            Height = 220,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            Text = "Đóng Cảnh Báo — Bắt Buộc Giải Trình GSP",
            StartPosition = FormStartPosition.CenterParent,
            BackColor = Color.White
        };

        var lbl = new Label { Text = "Nhập nguyên nhân và biện pháp xử lý sự cố (tối thiểu 5 ký tự):", Left = 20, Top = 15, Width = 450 };
        var txt = new TextBox 
        { 
            Left = 20, Top = 40, Width = 450, Height = 65, Multiline = true, 
            Text = "Đã kiểm tra kỹ thuật tủ vắc-xin, nhiệt độ đã hạ về mức an toàn chuẩn GSP." 
        };
        var btn = StyleHelper.CreateButton("Xác Nhận Đóng (sp_resolve_alert)", 260, 32);
        btn.Location = new Point(210, 120);
        btn.DialogResult = DialogResult.OK;

        prompt.Controls.AddRange([lbl, txt, btn]);
        if (prompt.ShowDialog() == DialogResult.OK)
        {
            var note = txt.Text.Trim();
            try
            {
                await using var conn = await DbHelper.OpenAsync();
                var userId = await conn.QueryFirstOrDefaultAsync<Guid>("SELECT TOP 1 id FROM dbo.users WHERE username='manager1'");

                await conn.ExecuteAsync("dbo.sp_resolve_alert", 
                    new { alert_id = alertId, resolved_by_user = userId, resolution_notes = note }, 
                    commandType: CommandType.StoredProcedure);

                MessageBox.Show("Đã đóng cảnh báo thành công qua Stored Procedure sp_resolve_alert!", "Thành Công", MessageBoxButtons.OK, MessageBoxIcon.Information);
                await RefreshAlertsAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Lỗi: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}
