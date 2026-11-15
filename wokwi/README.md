# Mô Phỏng Cảm Biến Kho Lạnh Vắc-Xin Bằng ESP32 Trên Wokwi

Thư mục này chứa mã nguồn và cấu hình phần cứng giả lập trên [Wokwi](https://wokwi.com) cho 5 tủ bảo quản vắc-xin (`SN-COLD-01-A` đến `SN-COLD-05-E`).

## Cách thiết lập trên Wokwi:

1. Mở trang tạo mới project ESP32 trên Wokwi: https://wokwi.com/projects/new/esp32
2. Dán nội dung các file tương ứng:
   - `sketch.ino`: Mã nguồn C++ đọc cảm biến DHT22 và gửi qua MQTT HiveMQ.
   - `diagram.json`: Sơ đồ chân nối ESP32 với cảm biến nhiệt độ DHT22 (chân D15).
   - `libraries.txt`: Danh sách 3 thư viện cần thiết (`PubSubClient`, `DHT sensor library for ESPx`, `ArduinoJson`).
3. Mở 5 tab Wokwi tương ứng 5 tủ lạnh vắc-xin, ở mỗi tab chỉ cần thay đổi dòng:
   ```cpp
   const char* HARDWARE_ID = "SN-COLD-01-A"; // Đổi từ A -> E cho 5 tủ
   ```
4. Bấm **Play (▶️)** để bắt đầu truyền dữ liệu về hệ thống giám sát.
