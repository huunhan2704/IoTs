import React, { useState, useEffect } from 'react';
import { apiFetch } from '../api';
import {
  FileText,
  Download,
  Printer,
  Calendar,
  Layers,
  CheckCircle,
  AlertTriangle,
  RefreshCw
} from 'lucide-react';

export default function ReportGenerator() {
  const [warehouses, setWarehouses] = useState([]);
  const [selectedWarehouseId, setSelectedWarehouseId] = useState('');
  const [selectedZoneId, setSelectedZoneId] = useState('');

  // Default: past 7 days
  const [fromTime, setFromTime] = useState(
    new Date(Date.now() - 7 * 24 * 3600 * 1000).toISOString().slice(0, 16)
  );
  const [toTime, setToTime] = useState(
    new Date().toISOString().slice(0, 16)
  );

  const [previewData, setPreviewData] = useState(null);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    const fetchWh = async () => {
      try {
        const list = await apiFetch('/api/warehouses');
        setWarehouses(list);
        if (list.length > 0) {
          setSelectedWarehouseId(list[0].id);
        }
      } catch (err) {
        console.error(err);
      }
    };
    fetchWh();
  }, []);

  const handleGeneratePreview = async (e) => {
    if (e) e.preventDefault();
    if (!selectedWarehouseId) return;

    try {
      setLoading(true);
      let url = `/api/reports/preview?warehouseId=${selectedWarehouseId}&from=${new Date(fromTime).toISOString()}&to=${new Date(toTime).toISOString()}`;
      if (selectedZoneId) url += `&zoneId=${selectedZoneId}`;
      const res = await apiFetch(url);
      setPreviewData(res);
    } catch (err) {
      alert(err.message);
    } finally {
      setLoading(false);
    }
  };

  const handleExportCsv = () => {
    if (!selectedWarehouseId) return;
    let url = `/api/reports/export-csv?warehouseId=${selectedWarehouseId}&from=${new Date(fromTime).toISOString()}&to=${new Date(toTime).toISOString()}`;
    if (selectedZoneId) url += `&zoneId=${selectedZoneId}`;
    window.open(url, '_blank');
  };

  const currentWh = warehouses.find(w => w.id === selectedWarehouseId);
  const availableZones = currentWh?.zones || [];

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <div>
          <h1 style={{ fontSize: '1.5rem', fontWeight: 700 }}>Báo Cáo Điều Kiện Vi Khí Hậu & Độ Bao Phủ Dữ Liệu (UC10)</h1>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.875rem' }}>
            Tổng hợp minh bạch chỉ số <strong>Data Coverage (%)</strong> (BR15) và biến thiên nhiệt/ẩm theo chuẩn GSP/GDP
          </p>
        </div>

        {previewData && (
          <div style={{ display: 'flex', gap: 10 }}>
            <button className="btn btn-secondary" onClick={() => window.print()}>
              <Printer size={16} /> In báo cáo / Lưu PDF
            </button>
            <button className="btn btn-primary" onClick={handleExportCsv}>
              <Download size={16} /> Xuất CSV dữ liệu thô
            </button>
          </div>
        )}
      </div>

      {/* Configuration Form Card */}
      <div className="card" style={{ marginBottom: 24 }}>
        <div className="card-title">
          <span>Thiết Lập Tham Số Khảo Sát Báo Cáo</span>
        </div>

        <form onSubmit={handleGeneratePreview}>
          <div className="grid-4" style={{ marginBottom: 20 }}>
            <div>
              <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                Kho hàng
              </label>
              <select
                className="select"
                value={selectedWarehouseId}
                onChange={(e) => {
                  setSelectedWarehouseId(e.target.value);
                  setSelectedZoneId('');
                }}
              >
                {warehouses.map(w => (
                  <option key={w.id} value={w.id}>{w.name}</option>
                ))}
              </select>
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                Phân vùng (Zone)
              </label>
              <select
                className="select"
                value={selectedZoneId}
                onChange={(e) => setSelectedZoneId(e.target.value)}
              >
                <option value="">-- Toàn bộ kho --</option>
                {availableZones.map(z => (
                  <option key={z.id} value={z.id}>{z.name}</option>
                ))}
              </select>
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                Từ thời điểm
              </label>
              <input
                type="datetime-local"
                className="input"
                value={fromTime}
                onChange={(e) => setFromTime(e.target.value)}
                required
              />
            </div>

            <div>
              <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                Đến thời điểm
              </label>
              <input
                type="datetime-local"
                className="input"
                value={toTime}
                onChange={(e) => setToTime(e.target.value)}
                required
              />
            </div>
          </div>

          <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
            <button type="submit" className="btn btn-primary" disabled={loading}>
              <RefreshCw size={16} /> {loading ? 'Đang tổng hợp số liệu...' : 'Tổng hợp dữ liệu xem trước'}
            </button>
          </div>
        </form>
      </div>

      {/* Preview Result */}
      {previewData && (
        <div>
          <h2 style={{ fontSize: '1.25rem', fontWeight: 600, marginBottom: 16 }}>
            Kết Quả Tổng Hợp Đánh Giá Chất Lượng Kho Hàng
          </h2>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 20 }}>
            {previewData.zoneSummaries?.map(zs => {
              const isGspCompliant = zs.dataCoveragePercentage >= 80 && zs.breachCount === 0;

              return (
                <div key={zs.zoneId} className="card">
                  <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
                    <div>
                      <h3 style={{ fontSize: '1.125rem', fontWeight: 600 }}>{zs.zoneName}</h3>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
                        Kỳ vọng: {zs.expectedReadings} readings | Hợp lệ: {zs.validReadings} | Good: {zs.goodReadings}
                      </div>
                    </div>

                    <span className={`badge ${isGspCompliant ? 'badge-fresh' : 'badge-stale'}`} style={{ padding: '6px 12px', fontSize: '0.8125rem' }}>
                      {isGspCompliant ? 'ĐẠT CHUẨN GSP/GDP' : 'CÓ VI PHẠM / CẦN LƯU Ý'}
                    </span>
                  </div>

                  {/* Data Coverage % Meter (BR15) */}
                  <div style={{ marginBottom: 20 }}>
                    <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: 6, fontSize: '0.8125rem' }}>
                      <span style={{ fontWeight: 600 }}>
                        Độ bao phủ dữ liệu trung thực (Data Coverage) (BR15):
                      </span>
                      <strong style={{ color: zs.dataCoveragePercentage >= 95 ? '#34d399' : zs.dataCoveragePercentage >= 80 ? '#fbbf24' : '#f87171' }}>
                        {zs.dataCoveragePercentage}%
                      </strong>
                    </div>

                    <div style={{ height: 10, backgroundColor: '#0f172a', borderRadius: 9999, overflow: 'hidden' }}>
                      <div
                        style={{
                          height: '100%',
                          width: `${Math.min(100, zs.dataCoveragePercentage)}%`,
                          backgroundColor: zs.dataCoveragePercentage >= 95 ? '#10b981' : zs.dataCoveragePercentage >= 80 ? '#f59e0b' : '#ef4444',
                          transition: 'width 0.5s'
                        }}
                      />
                    </div>
                  </div>

                  {/* Summary Metric Cards */}
                  <div className="grid-4" style={{ fontSize: '0.875rem' }}>
                    <div style={{ padding: 12, background: '#0f172a', borderRadius: 6, border: '1px solid var(--border-color)' }}>
                      <div style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>Biến thiên nhiệt độ</div>
                      <div style={{ fontWeight: 600, color: '#38bdf8', marginTop: 4 }}>
                        {zs.tempAvg ? `${zs.tempAvg.toFixed(1)}°C (TB)` : '--'}
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-dim)' }}>
                        Min: {zs.tempMin ?? '--'}°C | Max: {zs.tempMax ?? '--'}°C
                      </div>
                    </div>

                    <div style={{ padding: 12, background: '#0f172a', borderRadius: 6, border: '1px solid var(--border-color)' }}>
                      <div style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>Biến thiên độ ẩm</div>
                      <div style={{ fontWeight: 600, color: '#06b6d4', marginTop: 4 }}>
                        {zs.humidityAvg ? `${zs.humidityAvg.toFixed(1)}% (TB)` : '--'}
                      </div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-dim)' }}>
                        Min: {zs.humidityMin ?? '--'}% | Max: {zs.humidityMax ?? '--'}%
                      </div>
                    </div>

                    <div style={{ padding: 12, background: '#0f172a', borderRadius: 6, border: '1px solid var(--border-color)' }}>
                      <div style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>Số lần vi phạm ngưỡng</div>
                      <div style={{ fontWeight: 700, color: zs.breachCount > 0 ? '#f87171' : '#34d399', fontSize: '1.25rem', marginTop: 2 }}>
                        {zs.breachCount} lần
                      </div>
                    </div>

                    <div style={{ padding: 12, background: '#0f172a', borderRadius: 6, border: '1px solid var(--border-color)' }}>
                      <div style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>Thời gian gián đoạn (DataGap)</div>
                      <div style={{ fontWeight: 600, color: zs.totalGapDurationSec > 0 ? '#fbbf24' : '#94a3b8', marginTop: 4 }}>
                        {zs.totalGapDurationSec > 0 ? `${Math.round(zs.totalGapDurationSec / 60)} phút` : '0 phút (100% Online)'}
                      </div>
                    </div>
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}
    </div>
  );
}
