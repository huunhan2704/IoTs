# ==============================================================================
# HỆ THỐNG IOT-04: THEO DÕI ĐIỀU KIỆN KHO BẰNG CẢM BIẾN (HONEST DATA)
# RUN SCRIPT — KHỞI CHẠY TOÀN BỘ HỆ THỐNG
# ==============================================================================
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host "  IOT-04: WAREHOUSE CONDITION MONITORING (HONEST DATA)" -ForegroundColor Yellow
Write-Host "==================================================================" -ForegroundColor Cyan
Write-Host ""

# 1. Kiểm tra CSDL SQL Server
Write-Host "[1/3] Kiểm tra kết nối SQL Server (database: IOT04_WarehouseMonitor)..." -ForegroundColor White
$dbCheck = sqlcmd -S .\SQLEXPRESS -E -C -h -1 -W -Q "SET NOCOUNT ON; SELECT DB_ID('IOT04_WarehouseMonitor');" 2>$null
if ($LASTEXITCODE -ne 0 -or [string]::IsNullOrWhiteSpace($dbCheck) -or $dbCheck -eq "NULL") {
    Write-Host "  CSDL chưa được khởi tạo. Đang tạo CSDL và nạp dữ liệu mẫu..." -ForegroundColor Yellow
    & "$PSScriptRoot\database\init_db.ps1"
} else {
    Write-Host "  CSDL SQL Server đã sẵn sàng." -ForegroundColor Green
}

# 2. Khởi chạy Backend ASP.NET Core (.NET 9)
Write-Host "[2/3] Đang khởi chạy Backend API & Mạng Cảm Biến Giả Lập..." -ForegroundColor White
Write-Host "  - Địa chỉ Web App: http://localhost:5080" -ForegroundColor Cyan
Write-Host "  - MQTT Broker:    tcp://localhost:1883" -ForegroundColor Cyan
Write-Host "  - Tài khoản mẫu:  manager1 / 123456 (Quản lý) | operator1 / 123456 (Giám sát)" -ForegroundColor Gray
Write-Host ""

Start-Process "http://localhost:5080"

cd "$PSScriptRoot\backend"
dotnet run
