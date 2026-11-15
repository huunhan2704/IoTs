# IOT-04 — HỆ THỐNG THEO DÕI ĐIỀU KIỆN KHO BẰNG CẢM BIẾN
> **Kiến trúc Microservices & Triết lý Dữ liệu Trung thực (Honest Data, No Blind Interpolation)**

---

## 1. Tổng Quan & Cấu Trúc Đồ Án

Hệ thống được thiết kế và triển khai hoàn chỉnh theo đặc tả đồ án **IOT-04**, tối ưu hóa cho ngành logistics và chuỗi cung ứng dược phẩm tuân thủ **GSP/GDP**:

* **Cơ sở dữ liệu:** Microsoft SQL Server 2019/2022/2025 (Chuyển đổi tối ưu từ PostgreSQL 16 + TimescaleDB, tích hợp 4 Views, 3 Triggers, 4 Stored Procedures).
* **Backend:** ASP.NET Core (.NET 9), Dapper, SignalR WebSocket Hub, MQTT Broker nhúng (`MQTTnet` tại cổng 1883).
* **Frontend:** React 18, Vite, Recharts (`connectNulls={false}`, `GapOverlay`), Dark theme.
* **Desktop App (WinForms):** Ứng dụng C# Windows Forms (.NET 9) phong cách không màu / monochrome chuẩn văn phòng công sở, tự động đồng bộ thời gian thực với SQL Server.
* **Firmware:** ESP32 Wokwi C++ gửi dữ liệu về HiveMQ định kỳ 10 giây.

---

## 2. Điểm Nhấn Kỹ Thuật (Honest Data Principle)

1. **Không nội suy mù qua khoảng trống:** Biểu đồ vi khí hậu sử dụng cấu hình bắt buộc `connectNulls={false}` và phủ dải bóng đỏ nhạt `GapOverlay` tại các tọa độ `[start_time, end_time]` từ bảng `data_gaps`.
2. **Máy trạng thái hữu hạn FSM 3 mức:**
   * Sau $2\times$ interval ($300\text{s}$): Chuyển `STALE` (Cảnh báo vàng).
   * Sau $5\times$ interval ($750\text{s}$): Chuyển `DISCONNECTED` (Cảnh báo đỏ), tự động mở bản ghi `data_gaps`.
   * Khi tái kết nối: Tự động đóng `data_gaps`, tính toán `duration_sec`, chuyển về `CONNECTED`.
3. **Phát hiện trôi dạt chỉ số bằng thuật toán CUSUM:**
   * Định kỳ 15 phút so sánh với trung vị nhóm cảm biến cùng phân vùng.
   * Gắn nhãn `SUSPECT_DRIFT` khi độ trôi $\ge 0.1^\circ\text{C/h}$, bảo tồn tuyệt đối giá trị thô (`Raw Value Preserved`).
4. **Data Coverage % (BR15):**
   * Công thức: $\text{Coverage } (\%) = (\text{Số readings GOOD} / \text{Số readings kỳ vọng}) \times 100\%$.
   * Công bố minh bạch trên mọi báo cáo GSP/GDP.

---

## 3. Hướng Dẫn Khởi Chạy

### Cách 1: Khởi chạy Web App 1-Click (Khuyên dùng)
Mở PowerShell tại thư mục này và chạy:
```powershell
.\run.ps1
```
Trình duyệt sẽ tự động mở tại: **`http://localhost:5080`**.

### Cách 2: Khởi chạy Ứng dụng Desktop WinForms (Không Màu / Chuẩn Công Sở)
Double-click file `run_winform.bat` hoặc mở terminal chạy:
```powershell
cd winform
dotnet run
```

### Cách 3: Chế độ phát triển Frontend riêng (Hot Reload)
* Terminal 1 (Backend):
  ```powershell
  cd backend
  dotnet run
  ```
* Terminal 2 (Frontend):
  ```powershell
  cd frontend
  npm run dev
  ```
  Truy cập tại: **`http://localhost:5173`**.

---

## 4. Tài Khoản Đăng Nhập Mẫu

Mật khẩu mặc định của mọi tài khoản: **`123456`**

| Tên đăng nhập | Vai trò | Chức năng chính |
| :--- | :--- | :--- |
| `manager1` | WAREHOUSE_MANAGER | Quản lý kho, cấu hình ngưỡng vùng, phê duyệt báo cáo vi khí hậu |
| `operator1` | MONITORING_OPERATOR | Giám sát dashboard thời gian thực, tiếp nhận (Acknowledge) & đóng cảnh báo (Resolve) |
| `tech1` | TECHNICIAN | Đăng ký cảm biến mới, lập biên bản kiểm tra (Inspection), ghi nhận hiệu chuẩn (Calibration) |
| `admin` | ADMIN | Toàn quyền quản trị hệ thống |
