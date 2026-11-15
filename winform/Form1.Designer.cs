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

    private System.Windows.Forms.Panel pnlSidebar;
    private System.Windows.Forms.Panel pnlLogo;
    private System.Windows.Forms.Label lblLogoTitle;
    private System.Windows.Forms.Label lblLogoSubtitle;
    private System.Windows.Forms.Button btnNavOverview;
    private System.Windows.Forms.Button btnNavZoneDetail;
    private System.Windows.Forms.Button btnNavSensors;
    private System.Windows.Forms.Button btnNavAlerts;
    private System.Windows.Forms.Button btnNavInspection;
    private System.Windows.Forms.Button btnNavReports;

    private System.Windows.Forms.Panel pnlTopBar;
    private System.Windows.Forms.Label lblActiveModuleTitle;
    private System.Windows.Forms.CheckBox chkAutoSync;
    private System.Windows.Forms.Button btnSyncNow;

    private System.Windows.Forms.Panel pnlContent;

    private System.Windows.Forms.StatusStrip statusStrip;
    private System.Windows.Forms.ToolStripStatusLabel statusDb;
    private System.Windows.Forms.ToolStripStatusLabel statusWokwi;
    private System.Windows.Forms.ToolStripStatusLabel statusLastSync;

    private System.Windows.Forms.Timer timerAutoSync;

    private void InitializeComponent()
    {
        this.components = new System.ComponentModel.Container();

        // Form settings (Chuẩn trắng đen công sở monochrome)
        this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
        this.ClientSize = new System.Drawing.Size(1260, 750);
        this.MinimumSize = new System.Drawing.Size(1100, 650);
        this.Text = "Hệ Thống Giám Sát Vi Khí Hậu Kho Vắc-Xin — IOT-04 (Honest Data & Microservices)";
        this.Font = new System.Drawing.Font("Segoe UI", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
        this.BackColor = StyleHelper.ColorBg;
        this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;

        // 1. Sidebar (Menu điều hướng bên trái)
        this.pnlSidebar = new System.Windows.Forms.Panel();
        this.pnlSidebar.Dock = System.Windows.Forms.DockStyle.Left;
        this.pnlSidebar.Width = 240;
        this.pnlSidebar.BackColor = System.Drawing.Color.FromArgb(240, 240, 240);
        this.pnlSidebar.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        // Logo / Title in Sidebar
        this.pnlLogo = new System.Windows.Forms.Panel();
        this.pnlLogo.Dock = System.Windows.Forms.DockStyle.Top;
        this.pnlLogo.Height = 70;
        this.pnlLogo.BackColor = System.Drawing.Color.White;
        this.pnlLogo.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        this.lblLogoTitle = new System.Windows.Forms.Label();
        this.lblLogoTitle.Text = "IOT-04 MONITOR";
        this.lblLogoTitle.Font = new System.Drawing.Font("Segoe UI", 11.5F, System.Drawing.FontStyle.Bold);
        this.lblLogoTitle.Location = new System.Drawing.Point(12, 12);
        this.lblLogoTitle.AutoSize = true;

        this.lblLogoSubtitle = new System.Windows.Forms.Label();
        this.lblLogoSubtitle.Text = "Honest Data & GSP Storage";
        this.lblLogoSubtitle.Font = new System.Drawing.Font("Segoe UI", 8.25F);
        this.lblLogoSubtitle.ForeColor = StyleHelper.ColorTextMuted;
        this.lblLogoSubtitle.Location = new System.Drawing.Point(13, 36);
        this.lblLogoSubtitle.AutoSize = true;

        this.pnlLogo.Controls.AddRange([this.lblLogoTitle, this.lblLogoSubtitle]);

        // 6 Menu Buttons
        int btnHeight = 44;
        int btnTop = 80;

        Button MakeNavBtn(string text, EventHandler handler)
        {
            var btn = new Button
            {
                Text = text,
                Top = btnTop,
                Left = 8,
                Width = 222,
                Height = btnHeight,
                BackColor = System.Drawing.Color.White,
                ForeColor = StyleHelper.ColorTextPrimary,
                FlatStyle = System.Windows.Forms.FlatStyle.Standard,
                Font = new System.Drawing.Font("Segoe UI", 9.25F, System.Drawing.FontStyle.Bold),
                TextAlign = System.Drawing.ContentAlignment.MiddleLeft,
                Cursor = System.Windows.Forms.Cursors.Hand
            };
            btn.Click += handler;
            btnTop += 50;
            return btn;
        }

        this.btnNavOverview = MakeNavBtn("1. Tổng Quan Kho", (s, e) => ShowModule(0));
        this.btnNavZoneDetail = MakeNavBtn("2. Chi Tiết Vùng & Biểu Đồ", (s, e) => ShowModule(1));
        this.btnNavSensors = MakeNavBtn("3. Quản Lý Cảm Biến", (s, e) => ShowModule(2));
        this.btnNavAlerts = MakeNavBtn("4. Trung Tâm Cảnh Báo", (s, e) => ShowModule(3));
        this.btnNavInspection = MakeNavBtn("5. Kiểm Tra & Hiệu Chuẩn", (s, e) => ShowModule(4));
        this.btnNavReports = MakeNavBtn("6. Báo Cáo Điều Kiện Kho", (s, e) => ShowModule(5));

        this.pnlSidebar.Controls.AddRange([
            this.pnlLogo,
            this.btnNavOverview,
            this.btnNavZoneDetail,
            this.btnNavSensors,
            this.btnNavAlerts,
            this.btnNavInspection,
            this.btnNavReports
        ]);

        // 2. TopBar
        this.pnlTopBar = new System.Windows.Forms.Panel();
        this.pnlTopBar.Dock = System.Windows.Forms.DockStyle.Top;
        this.pnlTopBar.Height = 50;
        this.pnlTopBar.BackColor = System.Drawing.Color.White;
        this.pnlTopBar.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;

        this.lblActiveModuleTitle = new System.Windows.Forms.Label();
        this.lblActiveModuleTitle.Text = "TỔNG QUAN PHÂN VÙNG KHO";
        this.lblActiveModuleTitle.Font = new System.Drawing.Font("Segoe UI", 11F, System.Drawing.FontStyle.Bold);
        this.lblActiveModuleTitle.Location = new System.Drawing.Point(15, 12);
        this.lblActiveModuleTitle.AutoSize = true;

        this.chkAutoSync = new System.Windows.Forms.CheckBox();
        this.chkAutoSync.Text = "Tự động đồng bộ (3s)";
        this.chkAutoSync.Checked = true;
        this.chkAutoSync.Location = new System.Drawing.Point(960, 14);
        this.chkAutoSync.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.chkAutoSync.AutoSize = true;
        this.chkAutoSync.CheckedChanged += (s, e) => timerAutoSync.Enabled = chkAutoSync.Checked;

        this.btnSyncNow = StyleHelper.CreateButton("Đồng Bộ Ngay", 120, 30);
        this.btnSyncNow.Location = new System.Drawing.Point(1120, 9);
        this.btnSyncNow.Anchor = System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right;
        this.btnSyncNow.Click += async (s, e) => await SyncCurrentModuleAsync();

        this.pnlTopBar.Controls.AddRange([this.lblActiveModuleTitle, this.chkAutoSync, this.btnSyncNow]);

        // 3. Content Panel (Nơi nhúng các Form con)
        this.pnlContent = new System.Windows.Forms.Panel();
        this.pnlContent.Dock = System.Windows.Forms.DockStyle.Fill;
        this.pnlContent.BackColor = StyleHelper.ColorBg;

        // 4. StatusStrip
        this.statusStrip = new System.Windows.Forms.StatusStrip();
        this.statusStrip.BackColor = System.Drawing.Color.FromArgb(235, 235, 235);

        this.statusDb = new System.Windows.Forms.ToolStripStatusLabel();
        this.statusDb.Text = "CSDL: .\\SQLEXPRESS (IOT04_WarehouseMonitor)";
        this.statusDb.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;

        this.statusWokwi = new System.Windows.Forms.ToolStripStatusLabel();
        this.statusWokwi.Text = "Wokwi Bridge: broker.hivemq.com (Port 1883)";
        this.statusWokwi.BorderSides = System.Windows.Forms.ToolStripStatusLabelBorderSides.Right;

        this.statusLastSync = new System.Windows.Forms.ToolStripStatusLabel();
        this.statusLastSync.Text = "Đồng bộ lúc: --:--:--";

        this.statusStrip.Items.AddRange([this.statusDb, this.statusWokwi, this.statusLastSync]);

        // 5. Timer tự động quét 3s
        this.timerAutoSync = new System.Windows.Forms.Timer(this.components);
        this.timerAutoSync.Interval = 3000;
        this.timerAutoSync.Tick += async (s, e) => await SyncCurrentModuleAsync();

        // Add controls to main form
        this.Controls.Add(this.pnlContent);
        this.Controls.Add(this.pnlTopBar);
        this.Controls.Add(this.pnlSidebar);
        this.Controls.Add(this.statusStrip);
    }

    #endregion
}
