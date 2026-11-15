using Iot04.WinForms.Forms;

namespace Iot04.WinForms;

public partial class Form1 : Form
{
    private readonly Form[] _modules = new Form[6];
    private int _currentModuleIndex = -1;

    public Form1()
    {
        InitializeComponent();

        // Khởi tạo 6 Form nghiệp vụ chuyên sâu
        var frmOverview = new FormWarehouseOverview();
        frmOverview.RequestOpenZoneDetail += () => ShowModule(1);

        _modules[0] = frmOverview;
        _modules[1] = new FormZoneDetail();
        _modules[2] = new FormSensorManagement();
        _modules[3] = new FormAlertCenter();
        _modules[4] = new FormInspectionCalibration();
        _modules[5] = new FormWarehouseReport();

        foreach (var m in _modules)
        {
            m.TopLevel = false;
            m.FormBorderStyle = FormBorderStyle.None;
            m.Dock = DockStyle.Fill;
        }

        this.Load += (_, _) =>
        {
            ShowModule(0);
            timerAutoSync.Start();
        };
    }

    public void ShowModule(int index)
    {
        if (index < 0 || index >= _modules.Length) return;
        if (_currentModuleIndex == index) return;

        pnlContent.SuspendLayout();
        pnlContent.Controls.Clear();

        var child = _modules[index];
        pnlContent.Controls.Add(child);
        child.Show();
        pnlContent.ResumeLayout();

        _currentModuleIndex = index;

        // Cập nhật tiêu đề TopBar
        lblActiveModuleTitle.Text = index switch
        {
            0 => "1. TỔNG QUAN PHÂN VÙNG KHO (WAREHOUSE OVERVIEW)",
            1 => "2. CHI TIẾT PHÂN VÙNG KHO VẮC-XIN & BIỂU ĐỒ DATAGAP",
            2 => "3. QUẢN LÝ THIẾT BỊ CẢM BIẾN IOT (SENSOR MANAGEMENT)",
            3 => "4. TRUNG TÂM XỬ LÝ & ĐIỀU PHỐI CẢNH BÁO (ALERT CENTER)",
            4 => "5. KIỂM TRA ĐỊNH KỲ VÀ HIỆU CHUẨN THIẾT BỊ (INSPECTION & CALIBRATION)",
            5 => "6. BÁO CÁO ĐIỀU KIỆN KHO & THẨM ĐỊNH GSP (REPORTS)",
            _ => "HỆ THỐNG GIÁM SÁT VI KHÍ HẬU KHO"
        };

        // Cập nhật giao diện nút Menu (Monochrome active state)
        Button[] navBtns = [btnNavOverview, btnNavZoneDetail, btnNavSensors, btnNavAlerts, btnNavInspection, btnNavReports];
        for (int i = 0; i < navBtns.Length; i++)
        {
            if (i == index)
            {
                navBtns[i].BackColor = Color.FromArgb(220, 220, 220);
                navBtns[i].Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            }
            else
            {
                navBtns[i].BackColor = Color.White;
                navBtns[i].Font = new Font("Segoe UI", 9F, FontStyle.Regular);
            }
        }

        _ = SyncCurrentModuleAsync();
    }

    private async Task SyncCurrentModuleAsync()
    {
        try
        {
            switch (_currentModuleIndex)
            {
                case 0:
                    if (_modules[0] is FormWarehouseOverview f0) await f0.RefreshDataAsync();
                    break;
                case 1:
                    if (_modules[1] is FormZoneDetail f1) await f1.LoadDataAsync();
                    break;
                case 2:
                    if (_modules[2] is FormSensorManagement f2) await f2.RefreshSensorsAsync();
                    break;
                case 3:
                    if (_modules[3] is FormAlertCenter f3) await f3.RefreshAlertsAsync();
                    break;
                case 4:
                    if (_modules[4] is FormInspectionCalibration f4) await f4.RefreshAllAsync();
                    break;
                case 5:
                    if (_modules[5] is FormWarehouseReport f5) await f5.RefreshReportsAsync();
                    break;
            }

            statusLastSync.Text = $"Đồng bộ lúc: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            statusLastSync.Text = $"Lỗi: {ex.Message}";
        }
    }
}
