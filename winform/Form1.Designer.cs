namespace Iot04.WinForms;

partial class Form1
{
    private System.ComponentModel.IContainer components = null;

    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    private System.Windows.Forms.Panel pnlHeader;
    private System.Windows.Forms.Label lblTitle;
    private System.Windows.Forms.Label lblSubtitle;
    private System.Windows.Forms.Button btnRefresh;
    private System.Windows.Forms.CheckBox chkAutoRefresh;

    private System.Windows.Forms.TabControl tabControl;
    private System.Windows.Forms.TabPage tabLive;
    private System.Windows.Forms.TabPage tabAlerts;
    private System.Windows.Forms.TabPage tabCompliance;
    private System.Windows.Forms.TabPage tabHonestData;

    // Tab 1: Live
    private System.Windows.Forms.DataGridView dgvSensors;
    private System.Windows.Forms.Panel pnlLiveSummary;
    private System.Windows.Forms.Label lblSummaryTotal;
    private System.Windows.Forms.Label lblSummaryOnline;
    private System.Windows.Forms.Label lblSummaryExcursion;
    private System.Windows.Forms.Label lblSummaryAvgTemp;

    // Tab 2: Alerts
    private System.Windows.Forms.DataGridView dgvAlerts;
    private System.Windows.Forms.Panel pnlAlertActions;
    private System.Windows.Forms.Button btnResolveAlert;
    private System.Windows.Forms.Label lblAlertNotice;

    // Tab 3: Compliance
    private System.Windows.Forms.DataGridView dgvCompliance;
    private System.Windows.Forms.Panel pnlComplianceActions;
    private System.Windows.Forms.Button btnGenerateDailyReport;

    // Tab 4: Honest Data
    private System.Windows.Forms.Panel pnlHonestData;
    private System.Windows.Forms.Label lblHonestTitle;
    private System.Windows.Forms.TextBox txtHonestDescription;
    private System.Windows.Forms.Button btnTestHonestTrigger;
    private System.Windows.Forms.DataGridView dgvDataGaps;
    private System.Windows.Forms.Label lblGapsTitle;

    // Footer
    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel statusDb;
    private System.Windows.Forms.ToolStripStatusLabel statusWokwi;
    private System.Windows.Forms.ToolStripStatusLabel statusTime;

    private System.Windows.Forms.Timer timerAutoRefresh;

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();
        
        // Form properties (Không màu - Giao diện chuẩn công sở trắng đen xám)
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1080, 680);
        this.MinimumSize = new System.Drawing.Size(950, 600);
        this.Text = "Hệ Thống Giám Sát Vi Khí Hậu Kho Vắc-Xin — IOT-04 (Honest Data & GSP)";
        this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.BackColor = System.Drawing.Color.FromArgb(245, 245, 245);
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

        // 1. Header Panel
        this.pnlHeader = new System.Windows.Forms.Panel();
        this.pnlHeader.Dock = System.Windows.Forms.DockStyle.Top;
        this.pnlHeader.Height = 70;
        this.pnlHeader.BackColor = System.Drawing.Color.White;
        this.pnlHeader.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        this.lblTitle = new System.Windows.Forms.Label();
        this.lblTitle.Text = "HỆ THỐNG GIÁM SÁT VI KHÍ HẬU KHO VẮC-XIN (GSP VACCINE STORAGE)";
        this.lblTitle.Font = new System.Drawing.Font("Segoe UI", 12F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.lblTitle.ForeColor = System.Drawing.Color.Black;
        this.lblTitle.Location = new System.Drawing.Point(15, 12);
        this.lblTitle.AutoSize = true;

        this.lblSubtitle = new System.Windows.Forms.Label();
        this.lblSubtitle.Text = "Đồ án IOT-04 | Triết lý Dữ liệu Trung thực (Honest Data) | SQL Server 2025 | 5 Tủ Vắc-xin ESP32";
        this.lblSubtitle.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.lblSubtitle.ForeColor = System.Drawing.Color.FromArgb(80, 80, 80);
        this.lblSubtitle.Location = new System.Drawing.Point(16, 38);
        this.lblSubtitle.AutoSize = true;

        this.chkAutoRefresh = new System.Windows.Forms.CheckBox();
        this.chkAutoRefresh.Text = "Tự động cập nhật (3s)";
        this.chkAutoRefresh.Checked = true;
        this.chkAutoRefresh.Location = new System.Drawing.Point(800, 24);
        this.chkAutoRefresh.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.chkAutoRefresh.AutoSize = true;
        this.chkAutoRefresh.CheckedChanged += new System.EventHandler(this.ChkAutoRefresh_CheckedChanged);

        this.btnRefresh = new System.Windows.Forms.Button();
        this.btnRefresh.Text = "Làm mới (F5)";
        this.btnRefresh.Size = new System.Drawing.Size(100, 30);
        this.btnRefresh.Location = new System.Drawing.Point(955, 19);
        this.btnRefresh.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.btnRefresh.BackColor = System.Drawing.Color.White;
        this.btnRefresh.ForeColor = System.Drawing.Color.Black;
        this.btnRefresh.FlatStyle = System.Windows.Forms.FlatStyle.Standard;
        this.btnRefresh.Click += new System.EventHandler(this.BtnRefresh_Click);

        this.pnlHeader.Controls.Add(this.lblTitle);
        this.pnlHeader.Controls.Add(this.lblSubtitle);
        this.pnlHeader.Controls.Add(this.chkAutoRefresh);
        this.pnlHeader.Controls.Add(this.btnRefresh);

        // 2. TabControl
        this.tabControl = new System.Windows.Forms.TabControl();
        this.tabControl.Dock = System.Windows.Forms.DockStyle.Fill;
        this.tabControl.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);

        this.tabLive = new System.Windows.Forms.TabPage();
        this.tabLive.Text = " 1. Theo Dõi 5 Tủ Vắc-xin (Live) ";
        this.tabLive.BackColor = System.Drawing.Color.White;

        this.tabAlerts = new System.Windows.Forms.TabPage();
        this.tabAlerts.Text = " 2. Cảnh Báo Vi Phạm (Alerts) ";
        this.tabAlerts.BackColor = System.Drawing.Color.White;

        this.tabCompliance = new System.Windows.Forms.TabPage();
        this.tabCompliance.Text = " 3. Thẩm Định Tuân Thủ GSP (24h) ";
        this.tabCompliance.BackColor = System.Drawing.Color.White;

        this.tabHonestData = new System.Windows.Forms.TabPage();
        this.tabHonestData.Text = " 4. Kiểm Chứng Honest Data (Trigger) ";
        this.tabHonestData.BackColor = System.Drawing.Color.White;

        this.tabControl.Controls.Add(this.tabLive);
        this.tabControl.Controls.Add(this.tabAlerts);
        this.tabControl.Controls.Add(this.tabCompliance);
        this.tabControl.Controls.Add(this.tabHonestData);

        // --- CẤU HÌNH TAB 1: LIVE ---
        this.pnlLiveSummary = new System.Windows.Forms.Panel();
        this.pnlLiveSummary.Dock = System.Windows.Forms.DockStyle.Top;
        this.pnlLiveSummary.Height = 45;
        this.pnlLiveSummary.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
        this.pnlLiveSummary.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        this.lblSummaryTotal = new System.Windows.Forms.Label();
        this.lblSummaryTotal.Text = "Tổng tủ vắc-xin: 5";
        this.lblSummaryTotal.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.lblSummaryTotal.Location = new System.Drawing.Point(15, 12);
        this.lblSummaryTotal.AutoSize = true;

        this.lblSummaryOnline = new System.Windows.Forms.Label();
        this.lblSummaryOnline.Text = "Đang kết nối: 0 / 5";
        this.lblSummaryOnline.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.lblSummaryOnline.Location = new System.Drawing.Point(200, 12);
        this.lblSummaryOnline.AutoSize = true;

        this.lblSummaryExcursion = new System.Windows.Forms.Label();
        this.lblSummaryExcursion.Text = "Vi phạm ngưỡng (2-8°C): 0";
        this.lblSummaryExcursion.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.lblSummaryExcursion.Location = new System.Drawing.Point(400, 12);
        this.lblSummaryExcursion.AutoSize = true;

        this.lblSummaryAvgTemp = new System.Windows.Forms.Label();
        this.lblSummaryAvgTemp.Text = "Nhiệt độ TB kho: -- °C";
        this.lblSummaryAvgTemp.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.lblSummaryAvgTemp.Location = new System.Drawing.Point(660, 12);
        this.lblSummaryAvgTemp.AutoSize = true;

        this.pnlLiveSummary.Controls.Add(this.lblSummaryTotal);
        this.pnlLiveSummary.Controls.Add(this.lblSummaryOnline);
        this.pnlLiveSummary.Controls.Add(this.lblSummaryExcursion);
        this.pnlLiveSummary.Controls.Add(this.lblSummaryAvgTemp);

        this.dgvSensors = new System.Windows.Forms.DataGridView();
        this.dgvSensors.Dock = System.Windows.Forms.DockStyle.Fill;
        this.dgvSensors.BackgroundColor = System.Drawing.Color.White;
        this.dgvSensors.BorderStyle = System.Windows.Forms.BorderStyle.None;
        this.dgvSensors.AllowUserToAddRows = false;
        this.dgvSensors.AllowUserToDeleteRows = false;
        this.dgvSensors.ReadOnly = true;
        this.dgvSensors.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        this.dgvSensors.RowHeadersVisible = false;
        this.dgvSensors.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
        this.dgvSensors.RowTemplate.Height = 32;

        this.tabLive.Controls.Add(this.dgvSensors);
        this.tabLive.Controls.Add(this.pnlLiveSummary);

        // --- CẤU HÌNH TAB 2: ALERTS ---
        this.pnlAlertActions = new System.Windows.Forms.Panel();
        this.pnlAlertActions.Dock = System.Windows.Forms.DockStyle.Top;
        this.pnlAlertActions.Height = 45;
        this.pnlAlertActions.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
        this.pnlAlertActions.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        this.btnResolveAlert = new System.Windows.Forms.Button();
        this.btnResolveAlert.Text = "Đóng cảnh báo đã chọn (sp_resolve_alert)";
        this.btnResolveAlert.Size = new System.Drawing.Size(260, 30);
        this.btnResolveAlert.Location = new System.Drawing.Point(15, 7);
        this.btnResolveAlert.BackColor = System.Drawing.Color.White;
        this.btnResolveAlert.Click += new System.EventHandler(this.BtnResolveAlert_Click);

        this.lblAlertNotice = new System.Windows.Forms.Label();
        this.lblAlertNotice.Text = "Chuẩn GSP: Bắt buộc nhập giải trình xử lý sự cố trước khi đóng cảnh báo.";
        this.lblAlertNotice.Location = new System.Drawing.Point(290, 13);
        this.lblAlertNotice.AutoSize = true;
        this.lblAlertNotice.ForeColor = System.Drawing.Color.FromArgb(100, 100, 100);

        this.pnlAlertActions.Controls.Add(this.btnResolveAlert);
        this.pnlAlertActions.Controls.Add(this.lblAlertNotice);

        this.dgvAlerts = new System.Windows.Forms.DataGridView();
        this.dgvAlerts.Dock = System.Windows.Forms.DockStyle.Fill;
        this.dgvAlerts.BackgroundColor = System.Drawing.Color.White;
        this.dgvAlerts.BorderStyle = System.Windows.Forms.BorderStyle.None;
        this.dgvAlerts.AllowUserToAddRows = false;
        this.dgvAlerts.ReadOnly = true;
        this.dgvAlerts.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        this.dgvAlerts.RowHeadersVisible = false;
        this.dgvAlerts.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
        this.dgvAlerts.RowTemplate.Height = 30;

        this.tabAlerts.Controls.Add(this.dgvAlerts);
        this.tabAlerts.Controls.Add(this.pnlAlertActions);

        // --- CẤU HÌNH TAB 3: COMPLIANCE ---
        this.pnlComplianceActions = new System.Windows.Forms.Panel();
        this.pnlComplianceActions.Dock = System.Windows.Forms.DockStyle.Top;
        this.pnlComplianceActions.Height = 45;
        this.pnlComplianceActions.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
        this.pnlComplianceActions.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        this.btnGenerateDailyReport = new System.Windows.Forms.Button();
        this.btnGenerateDailyReport.Text = "Tạo Báo Cáo Thẩm Định Ngày (sp_generate_zone_daily_report)";
        this.btnGenerateDailyReport.Size = new System.Drawing.Size(360, 30);
        this.btnGenerateDailyReport.Location = new System.Drawing.Point(15, 7);
        this.btnGenerateDailyReport.BackColor = System.Drawing.Color.White;
        this.btnGenerateDailyReport.Click += new System.EventHandler(this.BtnGenerateDailyReport_Click);

        this.pnlComplianceActions.Controls.Add(this.btnGenerateDailyReport);

        this.dgvCompliance = new System.Windows.Forms.DataGridView();
        this.dgvCompliance.Dock = System.Windows.Forms.DockStyle.Fill;
        this.dgvCompliance.BackgroundColor = System.Drawing.Color.White;
        this.dgvCompliance.BorderStyle = System.Windows.Forms.BorderStyle.None;
        this.dgvCompliance.AllowUserToAddRows = false;
        this.dgvCompliance.ReadOnly = true;
        this.dgvCompliance.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        this.dgvCompliance.RowHeadersVisible = false;
        this.dgvCompliance.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
        this.dgvCompliance.RowTemplate.Height = 30;

        this.tabCompliance.Controls.Add(this.dgvCompliance);
        this.tabCompliance.Controls.Add(this.pnlComplianceActions);

        // --- CẤU HÌNH TAB 4: HONEST DATA ---
        this.pnlHonestData = new System.Windows.Forms.Panel();
        this.pnlHonestData.Dock = System.Windows.Forms.DockStyle.Top;
        this.pnlHonestData.Height = 170;
        this.pnlHonestData.BackColor = System.Drawing.Color.FromArgb(250, 250, 250);
        this.pnlHonestData.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        this.lblHonestTitle = new System.Windows.Forms.Label();
        this.lblHonestTitle.Text = "KIỂM CHỨNG TRIẾT LÝ DỮ LIỆU TRUNG THỰC (HONEST DATA PRINCIPLE)";
        this.lblHonestTitle.Font = new System.Drawing.Font("Segoe UI", 10F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.lblHonestTitle.Location = new System.Drawing.Point(15, 10);
        this.lblHonestTitle.AutoSize = true;

        this.txtHonestDescription = new System.Windows.Forms.TextBox();
        this.txtHonestDescription.Multiline = true;
        this.txtHonestDescription.ReadOnly = true;
        this.txtHonestDescription.BorderStyle = System.Windows.Forms.BorderStyle.None;
        this.txtHonestDescription.BackColor = System.Drawing.Color.FromArgb(250, 250, 250);
        this.txtHonestDescription.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.txtHonestDescription.Text = 
            "1. Dữ liệu thô từ cảm biến là bằng chứng pháp lý — TUYỆT ĐỐI KHÔNG ĐƯỢC CHỈNH SỬA (Append-Only).\r\n" +
            "2. Trigger [trg_readings_honest_data_guard] ở tầng CSDL SQL Server chặn đứng mọi lệnh UPDATE.\r\n" +
            "3. Khi cảm biến mất mạng, hệ thống tự động ghi nhận khoảng trống dữ liệu [data_gaps], không vẽ nội suy mù.";
        this.txtHonestDescription.Location = new System.Drawing.Point(18, 35);
        this.txtHonestDescription.Size = new System.Drawing.Size(950, 65);

        this.btnTestHonestTrigger = new System.Windows.Forms.Button();
        this.btnTestHonestTrigger.Text = "Thử chạy lệnh UPDATE vào bảng readings (Kiểm chứng Trigger chặn lại)";
        this.btnTestHonestTrigger.Size = new System.Drawing.Size(450, 35);
        this.btnTestHonestTrigger.Location = new System.Drawing.Point(18, 115);
        this.btnTestHonestTrigger.BackColor = System.Drawing.Color.White;
        this.btnTestHonestTrigger.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);
        this.btnTestHonestTrigger.Click += new System.EventHandler(this.BtnTestHonestTrigger_Click);

        this.pnlHonestData.Controls.Add(this.lblHonestTitle);
        this.pnlHonestData.Controls.Add(this.txtHonestDescription);
        this.pnlHonestData.Controls.Add(this.btnTestHonestTrigger);

        this.lblGapsTitle = new System.Windows.Forms.Label();
        this.lblGapsTitle.Text = "Danh sách các khoảng trống mất kết nối đã ghi nhận (Bảng dbo.data_gaps):";
        this.lblGapsTitle.Dock = System.Windows.Forms.DockStyle.Top;
        this.lblGapsTitle.Height = 30;
        this.lblGapsTitle.TextAlign = System.Drawing.ContentAlignment.BottomLeft;
        this.lblGapsTitle.Font = new System.Drawing.Font("Segoe UI", 9.5F, System.Drawing.FontStyle.Bold, System.Drawing.GraphicsUnit.Point);

        this.dgvDataGaps = new System.Windows.Forms.DataGridView();
        this.dgvDataGaps.Dock = System.Windows.Forms.DockStyle.Fill;
        this.dgvDataGaps.BackgroundColor = System.Drawing.Color.White;
        this.dgvDataGaps.BorderStyle = System.Windows.Forms.BorderStyle.None;
        this.dgvDataGaps.AllowUserToAddRows = false;
        this.dgvDataGaps.ReadOnly = true;
        this.dgvDataGaps.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
        this.dgvDataGaps.RowHeadersVisible = false;
        this.dgvDataGaps.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
        this.dgvDataGaps.RowTemplate.Height = 28;

        this.tabHonestData.Controls.Add(this.dgvDataGaps);
        this.tabHonestData.Controls.Add(this.lblGapsTitle);
        this.tabHonestData.Controls.Add(this.pnlHonestData);

        // 3. StatusStrip (Chân trang)
        this.statusStrip = new System.Windows.Forms.StatusStrip();
        this.statusStrip.BackColor = System.Drawing.Color.FromArgb(235, 235, 235);

        this.statusDb = new System.Windows.Forms.ToolStripStatusLabel();
        this.statusDb.Text = "CSDL: .\\SQLEXPRESS (IOT04_WarehouseMonitor)";
        this.statusDb.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;

        this.statusWokwi = new System.Windows.Forms.ToolStripStatusLabel();
        this.statusWokwi.Text = "ESP32 Wokwi: broker.hivemq.com";
        this.statusWokwi.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;

        this.statusTime = new System.Windows.Forms.ToolStripStatusLabel();
        this.statusTime.Text = "Cập nhật lúc: --:--:--";

        this.statusStrip.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.statusDb,
            this.statusWokwi,
            this.statusTime
        });

        // 4. Timer tự động làm mới
        this.timerAutoRefresh = new System.Windows.Forms.Timer(this.components);
        this.timerAutoRefresh.Interval = 3000; // 3 giây
        this.timerAutoRefresh.Tick += new System.EventHandler(this.TimerAutoRefresh_Tick);

        // Ghép các thành phần vào Form
        this.Controls.Add(this.tabControl);
        this.Controls.Add(this.pnlHeader);
        this.Controls.Add(this.statusStrip);
    }

    #endregion
}
