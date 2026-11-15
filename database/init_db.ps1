# ==============================================================================
# SCRIPT KHỞI TẠO CƠ SỞ DỮ LIỆU SQL SERVER CHO ĐỀ TÀI IOT-04
# ==============================================================================
$ErrorActionPreference = "Stop"
$dbDir = $PSScriptRoot

Write-Host ">>> Đang tạo bảng và cấu trúc CSDL trên SQL Server (01_schema.sql)..." -ForegroundColor Cyan
sqlcmd -S .\SQLEXPRESS -E -C -b -I -f 65001 -i "$dbDir\01_schema.sql"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Lỗi khi chạy 01_schema.sql!" -ForegroundColor Red
    exit 1
}

Write-Host ">>> Đang nạp dữ liệu khởi tạo mẫu và 24h đo đạc (02_seed.sql)..." -ForegroundColor Cyan
sqlcmd -S .\SQLEXPRESS -E -C -b -I -f 65001 -i "$dbDir\02_seed.sql"
if ($LASTEXITCODE -ne 0) {
    Write-Host "Lỗi khi chạy 02_seed.sql!" -ForegroundColor Red
    exit 1
}

Write-Host ">>> Hoàn tất khởi tạo CSDL IOT04_WarehouseMonitor!" -ForegroundColor Green
