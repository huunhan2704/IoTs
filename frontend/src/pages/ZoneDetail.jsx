import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { apiFetch } from '../api';
import HonestChart from '../components/HonestChart';
import ConnectionBadge from '../components/ConnectionBadge';
import DataAgeBadge from '../components/DataAgeBadge';
import {
  ArrowLeft,
  Settings,
  RefreshCw,
  Thermometer,
  Droplets,
  Activity,
  AlertCircle
} from 'lucide-react';

export default function ZoneDetail() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [zoneData, setZoneData] = useState(null);
  const [chartData, setChartData] = useState(null);
  const [hours, setHours] = useState(24);
  const [loading, setLoading] = useState(true);

  // Threshold modal state
  const [isEditingThreshold, setIsEditingThreshold] = useState(false);
  const [thresholdForm, setThresholdForm] = useState({ tempMin: 0, tempMax: 0, humidityMin: 0, humidityMax: 0 });
  const [savingThreshold, setSavingThreshold] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  const loadData = async () => {
    try {
      setLoading(true);
      const [zRes, cRes] = await Promise.all([
        apiFetch(`/api/zones/${id}`),
        apiFetch(`/api/zones/${id}/chart-data?hours=${hours}`)
      ]);
      setZoneData(zRes);
      setChartData(cRes);
      if (zRes.zone) {
        setThresholdForm({
          tempMin: zRes.zone.tempMin,
          tempMax: zRes.zone.tempMax,
          humidityMin: zRes.zone.humidityMin,
          humidityMax: zRes.zone.humidityMax
        });
      }
    } catch (err) {
      console.error(err);
      setErrorMsg(err.message);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    const timer = setInterval(loadData, 15000);
    return () => clearInterval(timer);
  }, [id, hours]);

  const handleSaveThresholds = async (e) => {
    e.preventDefault();
    try {
      setSavingThreshold(true);
      await apiFetch(`/api/zones/${id}/thresholds`, {
        method: 'PUT',
        body: JSON.stringify({
          tempMin: parseFloat(thresholdForm.tempMin),
          tempMax: parseFloat(thresholdForm.tempMax),
          humidityMin: parseFloat(thresholdForm.humidityMin),
          humidityMax: parseFloat(thresholdForm.humidityMax)
        })
      });
      setIsEditingThreshold(false);
      await loadData();
    } catch (err) {
      alert(err.message);
    } finally {
      setSavingThreshold(false);
    }
  };

  if (!zoneData?.zone) {
    return (
      <div style={{ textAlign: 'center', padding: '60px 0' }}>
        <p>Đang tải dữ liệu phân vùng...</p>
      </div>
    );
  }

  const { zone, sensors } = zoneData;

  // Prepare series data for Honest Chart
  const readings = chartData?.readings || [];
  const gaps = chartData?.gaps || [];

  return (
    <div>
      {/* Top back navigation and actions */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <button className="btn btn-outline" onClick={() => navigate('/')}>
          <ArrowLeft size={16} /> Quay lại danh sách kho
        </button>

        <div style={{ display: 'flex', gap: 10, alignItems: 'center' }}>
          <select
            className="select"
            style={{ width: 140 }}
            value={hours}
            onChange={(e) => setHours(parseInt(e.target.value))}
          >
            <option value={6}>6 giờ qua</option>
            <option value={12}>12 giờ qua</option>
            <option value={24}>24 giờ qua</option>
            <option value={48}>48 giờ qua</option>
            <option value={168}>7 ngày qua</option>
          </select>

          <button className="btn btn-secondary" onClick={() => setIsEditingThreshold(true)}>
            <Settings size={16} /> Cấu hình ngưỡng (BR02)
          </button>

          <button className="btn btn-outline" onClick={loadData}>
            <RefreshCw size={16} />
          </button>
        </div>
      </div>

      {/* Zone Header Info Card */}
      <div className="card" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 4 }}>
              <h1 style={{ fontSize: '1.5rem', fontWeight: 700 }}>{zone.name}</h1>
              <span className="badge badge-aging">{zone.zoneType}</span>
            </div>
            <div style={{ color: 'var(--text-muted)', fontSize: '0.875rem' }}>
              Kho: <strong>{zone.warehouseName}</strong> | Mã vùng: <strong>{zone.code}</strong>
            </div>
          </div>

          <div style={{ display: 'flex', gap: 20 }}>
            <div style={{ padding: '8px 16px', background: '#0f172a', borderRadius: 6, border: '1px solid var(--border-color)' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Dải nhiệt độ an toàn</div>
              <div style={{ fontSize: '1.125rem', fontWeight: 700, color: '#38bdf8' }}>
                {zone.tempMin}°C - {zone.tempMax}°C
              </div>
            </div>

            <div style={{ padding: '8px 16px', background: '#0f172a', borderRadius: 6, border: '1px solid var(--border-color)' }}>
              <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Dải độ ẩm an toàn</div>
              <div style={{ fontSize: '1.125rem', fontWeight: 700, color: '#06b6d4' }}>
                {zone.humidityMin}% - {zone.humidityMax}%
              </div>
            </div>
          </div>
        </div>
      </div>

      {/* Honest Data Principle Notice */}
      <div style={{
        padding: '10px 16px',
        background: 'rgba(56, 189, 248, 0.08)',
        border: '1px solid rgba(56, 189, 248, 0.25)',
        borderRadius: 6,
        marginBottom: 24,
        display: 'flex',
        alignItems: 'center',
        gap: 10,
        fontSize: '0.8125rem',
        color: '#bae6fd'
      }}>
        <AlertCircle size={18} style={{ color: '#38bdf8', flexShrink: 0 }} />
        <div>
          <strong>Triết lý Honest Data (BR08, BR10):</strong> Đồ thị tuyệt đối không nội suy qua khoảng thời gian mất tín hiệu (connectNulls=false).
          Các vùng tô bóng đỏ nhạt đại diện cho khoảng gián đoạn <strong>DataGap</strong> thực tế.
          Điểm vàng: <strong>SUSPECT_DRIFT</strong> (Bảo tồn giá trị thô).
        </div>
      </div>

      {/* Realtime Temperature Chart (BR08) */}
      <div className="card" style={{ marginBottom: 24 }}>
        <div className="card-title">
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Thermometer size={18} style={{ color: '#38bdf8' }} />
            <span>Biểu đồ Nhiệt độ Thời gian thực ({hours}h)</span>
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
            Ngưỡng an toàn: [{zone.tempMin}°C, {zone.tempMax}°C]
          </div>
        </div>

        <HonestChart
          data={readings.map(r => ({ ...r, value: r.temperature }))}
          gaps={gaps}
          dataKey="value"
          name="Nhiệt độ"
          unit="°C"
          minThreshold={zone.tempMin}
          maxThreshold={zone.tempMax}
          color="#38bdf8"
        />
      </div>

      {/* Realtime Humidity Chart (BR08) */}
      <div className="card" style={{ marginBottom: 24 }}>
        <div className="card-title">
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Droplets size={18} style={{ color: '#06b6d4' }} />
            <span>Biểu đồ Độ ẩm Thời gian thực ({hours}h)</span>
          </div>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>
            Ngưỡng an toàn: [{zone.humidityMin}%, {zone.humidityMax}%]
          </div>
        </div>

        <HonestChart
          data={readings.map(r => ({ ...r, value: r.humidity }))}
          gaps={gaps}
          dataKey="value"
          name="Độ ẩm"
          unit="%"
          minThreshold={zone.humidityMin}
          maxThreshold={zone.humidityMax}
          color="#06b6d4"
        />
      </div>

      {/* Sensor Table In Zone */}
      <div className="card">
        <div className="card-title">
          <span>Danh Sách Cảm Biến Lắp Đặt Tại Phân Vùng ({sensors.length} thiết bị)</span>
        </div>

        <div className="table-container">
          <table className="table">
            <thead>
              <tr>
                <th>Mã phần cứng (MAC/Serial)</th>
                <th>Trạng thái FSM (BR04)</th>
                <th>Tuổi dữ liệu (BR11)</th>
                <th>Giá trị hiện tại</th>
                <th>Độ trôi dạt (CUSUM)</th>
                <th>Mức Pin</th>
                <th>Sóng RSSI</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {sensors.map(s => (
                <tr key={s.id}>
                  <td style={{ fontWeight: 600, fontFamily: 'monospace' }}>{s.hardwareId}</td>
                  <td>
                    <ConnectionBadge state={s.connectionState} />
                  </td>
                  <td>
                    <DataAgeBadge lastReadingAt={s.lastReadingAt} />
                  </td>
                  <td>
                    <span style={{ color: '#38bdf8', fontWeight: 600 }}>
                      {s.latestTemp !== null && s.latestTemp !== undefined ? `${s.latestTemp.toFixed(1)}°C` : '--'}
                    </span>
                    {' / '}
                    <span style={{ color: '#06b6d4' }}>
                      {s.latestHumidity !== null && s.latestHumidity !== undefined ? `${s.latestHumidity.toFixed(1)}%` : '--'}
                    </span>
                  </td>
                  <td>
                    {s.tempDriftSeverity && s.tempDriftSeverity !== 'NONE' ? (
                      <span className="badge badge-stale">
                        {s.tempDriftRate ? `+${s.tempDriftRate}°C/h (${s.tempDriftSeverity})` : s.tempDriftSeverity}
                      </span>
                    ) : (
                      <span style={{ color: '#34d399', fontSize: '0.8125rem' }}>An toàn</span>
                    )}
                  </td>
                  <td>{s.batteryLevel !== null ? `${s.batteryLevel}%` : '--'}</td>
                  <td>{s.lastRssi !== null ? `${s.lastRssi} dBm` : '--'}</td>
                  <td>
                    <button
                      className="btn btn-outline btn-sm"
                      onClick={() => navigate(`/sensors/${s.id}`)}
                    >
                      Chi tiết thiết bị
                    </button>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </div>

      {/* Threshold Modal */}
      {isEditingThreshold && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3 style={{ fontSize: '1.125rem', fontWeight: 600, marginBottom: 16 }}>
              Cập nhật ngưỡng vi khí hậu an toàn (BR02)
            </h3>
            <p style={{ color: 'var(--text-muted)', fontSize: '0.8125rem', marginBottom: 16 }}>
              Mọi thay đổi sẽ được lưu vết vào bảng <code>zone_threshold_history</code> phục vụ kiểm toán chuẩn GSP/GDP.
            </p>

            <form onSubmit={handleSaveThresholds}>
              <div className="grid-2" style={{ marginBottom: 16 }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6 }}>Nhiệt độ Min (°C)</label>
                  <input
                    type="number"
                    step="0.1"
                    className="input"
                    value={thresholdForm.tempMin}
                    onChange={(e) => setThresholdForm({ ...thresholdForm, tempMin: e.target.value })}
                    required
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6 }}>Nhiệt độ Max (°C)</label>
                  <input
                    type="number"
                    step="0.1"
                    className="input"
                    value={thresholdForm.tempMax}
                    onChange={(e) => setThresholdForm({ ...thresholdForm, tempMax: e.target.value })}
                    required
                  />
                </div>
              </div>

              <div className="grid-2" style={{ marginBottom: 24 }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6 }}>Độ ẩm Min (%)</label>
                  <input
                    type="number"
                    step="0.1"
                    className="input"
                    value={thresholdForm.humidityMin}
                    onChange={(e) => setThresholdForm({ ...thresholdForm, humidityMin: e.target.value })}
                    required
                  />
                </div>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6 }}>Độ ẩm Max (%)</label>
                  <input
                    type="number"
                    step="0.1"
                    className="input"
                    value={thresholdForm.humidityMax}
                    onChange={(e) => setThresholdForm({ ...thresholdForm, humidityMax: e.target.value })}
                    required
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setIsEditingThreshold(false)}
                  disabled={savingThreshold}
                >
                  Hủy bỏ
                </button>
                <button type="submit" className="btn btn-primary" disabled={savingThreshold}>
                  {savingThreshold ? 'Đang lưu...' : 'Lưu cấu hình mới'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
