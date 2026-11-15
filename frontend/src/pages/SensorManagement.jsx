import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiFetch } from '../api';
import ConnectionBadge from '../components/ConnectionBadge';
import DataAgeBadge from '../components/DataAgeBadge';
import {
  Cpu,
  Plus,
  RefreshCw,
  Search,
  Filter,
  Wrench,
  ChevronRight,
  ShieldCheck
} from 'lucide-react';

export default function SensorManagement() {
  const navigate = useNavigate();
  const [sensors, setSensors] = useState([]);
  const [zones, setZones] = useState([]);
  const [search, setSearch] = useState('');
  const [zoneFilter, setZoneFilter] = useState('');
  const [loading, setLoading] = useState(true);

  // New Sensor Modal
  const [isRegistering, setIsRegistering] = useState(false);
  const [form, setForm] = useState({
    hardwareId: '',
    sensorType: 'COMBO',
    zoneId: '',
    expectedIntervalSec: 150,
    firmwareVersion: 'v2.1.0'
  });
  const [submitting, setSubmitting] = useState(false);

  const loadData = async () => {
    try {
      setLoading(true);
      const [sList, whList] = await Promise.all([
        apiFetch('/api/sensors'),
        apiFetch('/api/warehouses')
      ]);
      setSensors(sList);
      const allZones = whList.flatMap(w => w.zones || []);
      setZones(allZones);
      if (allZones.length > 0 && !form.zoneId) {
        setForm(prev => ({ ...prev, zoneId: allZones[0].id }));
      }
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
  }, []);

  const handleRegisterSubmit = async (e) => {
    e.preventDefault();
    try {
      setSubmitting(true);
      await apiFetch('/api/sensors', {
        method: 'POST',
        body: JSON.stringify({
          hardwareId: form.hardwareId.trim(),
          sensorType: form.sensorType,
          zoneId: form.zoneId || null,
          expectedIntervalSec: parseInt(form.expectedIntervalSec),
          firmwareVersion: form.firmwareVersion
        })
      });
      setIsRegistering(false);
      setForm({
        hardwareId: '',
        sensorType: 'COMBO',
        zoneId: zones[0]?.id || '',
        expectedIntervalSec: 150,
        firmwareVersion: 'v2.1.0'
      });
      await loadData();
      alert('Đăng ký cảm biến thành công!');
    } catch (err) {
      alert(err.message);
    } finally {
      setSubmitting(false);
    }
  };

  const filteredSensors = sensors.filter(s => {
    const matchSearch = s.hardwareId.toLowerCase().includes(search.toLowerCase());
    const matchZone = zoneFilter ? s.zoneId === zoneFilter : true;
    return matchSearch && matchZone;
  });

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <div>
          <h1 style={{ fontSize: '1.5rem', fontWeight: 700 }}>Quản Lý Vòng Đời Cảm Biến (Sensor Management — UC02)</h1>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.875rem' }}>
            Khai báo thiết bị, cấu hình chu kỳ gửi dữ liệu (BR03) và tự động tính toán ngưỡng FSM Stale/Disconnect (BR05, BR06)
          </p>
        </div>

        <div style={{ display: 'flex', gap: 10 }}>
          <button className="btn btn-primary" onClick={() => setIsRegistering(true)}>
            <Plus size={16} /> Đăng ký cảm biến mới
          </button>
          <button className="btn btn-outline" onClick={loadData}>
            <RefreshCw size={16} />
          </button>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <div className="card" style={{ marginBottom: 20, padding: 14 }}>
        <div style={{ display: 'flex', gap: 14, alignItems: 'center', flexWrap: 'wrap' }}>
          <div style={{ position: 'relative', flex: 1, minWidth: 240 }}>
            <Search size={16} style={{ position: 'absolute', left: 10, top: 10, color: 'var(--text-muted)' }} />
            <input
              type="text"
              className="input"
              style={{ paddingLeft: 34 }}
              placeholder="Tìm theo Hardware ID (MAC / Serial)..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: '0.8125rem', color: 'var(--text-muted)' }}>Lọc phân vùng:</span>
            <select
              className="select"
              style={{ width: 200 }}
              value={zoneFilter}
              onChange={(e) => setZoneFilter(e.target.value)}
            >
              <option value="">Tất cả phân vùng</option>
              {zones.map(z => (
                <option key={z.id} value={z.id}>{z.name}</option>
              ))}
            </select>
          </div>
        </div>
      </div>

      {/* Sensors Table */}
      <div className="card">
        <div className="table-container">
          <table className="table">
            <thead>
              <tr>
                <th>Hardware ID</th>
                <th>Phân vùng lắp đặt</th>
                <th>Loại & Chu kỳ</th>
                <th>Trạng thái FSM</th>
                <th>Tuổi dữ liệu</th>
                <th>Mức Pin</th>
                <th>Độ trôi (CUSUM)</th>
                <th>Hạn kiểm định</th>
                <th>Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {filteredSensors.length === 0 ? (
                <tr>
                  <td colSpan={9} style={{ textAlign: 'center', padding: '32px 0', color: 'var(--text-muted)' }}>
                    Không tìm thấy cảm biến nào.
                  </td>
                </tr>
              ) : (
                filteredSensors.map(s => (
                  <tr key={s.id}>
                    <td style={{ fontWeight: 600, fontFamily: 'monospace' }}>{s.hardwareId}</td>
                    <td>{s.zoneName || <span style={{ color: 'var(--text-dim)' }}>Chưa gán</span>}</td>
                    <td>
                      <div>{s.sensorType}</div>
                      <div style={{ fontSize: '0.75rem', color: 'var(--text-dim)' }}>{s.expectedIntervalSec}s (2.5m)</div>
                    </td>
                    <td>
                      <ConnectionBadge state={s.connectionState} />
                    </td>
                    <td>
                      <DataAgeBadge lastReadingAt={s.lastReadingAt} />
                    </td>
                    <td>{s.batteryLevel !== null ? `${s.batteryLevel}%` : '--'}</td>
                    <td>
                      {s.tempDriftSeverity && s.tempDriftSeverity !== 'NONE' ? (
                        <span className="badge badge-stale">{s.tempDriftSeverity}</span>
                      ) : (
                        <span style={{ color: '#34d399', fontSize: '0.8125rem' }}>An toàn</span>
                      )}
                    </td>
                    <td>
                      {s.nextCalibrationDue ? new Date(s.nextCalibrationDue).toLocaleDateString('vi-VN') : '--'}
                    </td>
                    <td>
                      <button
                        className="btn btn-outline btn-sm"
                        onClick={() => navigate(`/sensors/${s.id}`)}
                      >
                        Chi tiết <ChevronRight size={14} />
                      </button>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Register Modal */}
      {isRegistering && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3 style={{ fontSize: '1.25rem', fontWeight: 600, marginBottom: 12 }}>
              Đăng Ký Cảm Biến Mới (UC02)
            </h3>
            <p style={{ color: 'var(--text-muted)', fontSize: '0.8125rem', marginBottom: 20 }}>
              Khai báo Hardware ID và chu kỳ gửi dữ liệu. Hệ thống tự động tính toán:
              <strong> Stale = 2x</strong> ({form.expectedIntervalSec * 2}s) và <strong>Disconnect = 5x</strong> ({form.expectedIntervalSec * 5}s).
            </p>

            <form onSubmit={handleRegisterSubmit}>
              <div style={{ marginBottom: 16 }}>
                <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                  Mã phần cứng (Hardware ID / MAC) (*)
                </label>
                <input
                  type="text"
                  className="input"
                  placeholder="Ví dụ: SN-COLD-04-D"
                  value={form.hardwareId}
                  onChange={(e) => setForm({ ...form, hardwareId: e.target.value })}
                  required
                />
              </div>

              <div className="grid-2" style={{ marginBottom: 16 }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                    Loại cảm biến
                  </label>
                  <select
                    className="select"
                    value={form.sensorType}
                    onChange={(e) => setForm({ ...form, sensorType: e.target.value })}
                  >
                    <option value="COMBO">COMBO (Nhiệt & Độ ẩm)</option>
                    <option value="TEMPERATURE">Chỉ Nhiệt độ</option>
                    <option value="HUMIDITY">Chỉ Độ ẩm</option>
                  </select>
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                    Phân vùng lắp đặt
                  </label>
                  <select
                    className="select"
                    value={form.zoneId}
                    onChange={(e) => setForm({ ...form, zoneId: e.target.value })}
                  >
                    <option value="">-- Kho dự phòng (Chưa gán) --</option>
                    {zones.map(z => (
                      <option key={z.id} value={z.id}>{z.name}</option>
                    ))}
                  </select>
                </div>
              </div>

              <div className="grid-2" style={{ marginBottom: 24 }}>
                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                    Chu kỳ gửi tin expectedInterval (30s - 3600s) (BR03)
                  </label>
                  <input
                    type="number"
                    min={30}
                    max={3600}
                    className="input"
                    value={form.expectedIntervalSec}
                    onChange={(e) => setForm({ ...form, expectedIntervalSec: e.target.value })}
                    required
                  />
                </div>

                <div>
                  <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                    Phiên bản Firmware
                  </label>
                  <input
                    type="text"
                    className="input"
                    value={form.firmwareVersion}
                    onChange={(e) => setForm({ ...form, firmwareVersion: e.target.value })}
                  />
                </div>
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setIsRegistering(false)}
                  disabled={submitting}
                >
                  Hủy bỏ
                </button>
                <button type="submit" className="btn btn-primary" disabled={submitting}>
                  {submitting ? 'Đang kích hoạt...' : 'Kích hoạt cảm biến'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
