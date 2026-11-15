import React, { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiFetch } from '../api';
import DataAgeBadge from '../components/DataAgeBadge';
import ConnectionBadge from '../components/ConnectionBadge';
import {
  Thermometer,
  Droplets,
  AlertTriangle,
  ChevronRight,
  RefreshCw,
  Layers,
  Activity,
  Cpu
} from 'lucide-react';

export default function WarehouseDashboard({ realTimeTelemetry, realTimeAlerts }) {
  const navigate = useNavigate();
  const [warehouses, setWarehouses] = useState([]);
  const [selectedWarehouseId, setSelectedWarehouseId] = useState('');
  const [zones, setZones] = useState([]);
  const [alerts, setAlerts] = useState([]);
  const [loading, setLoading] = useState(true);

  const loadData = async () => {
    try {
      setLoading(true);
      const [whList, zoneList, alertList] = await Promise.all([
        apiFetch('/api/warehouses'),
        apiFetch('/api/zones/overview'),
        apiFetch('/api/alerts?status=ACTIVE')
      ]);

      setWarehouses(whList);
      if (whList.length > 0 && !selectedWarehouseId) {
        setSelectedWarehouseId(whList[0].id);
      }
      setZones(zoneList);
      setAlerts(alertList);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadData();
    const timer = setInterval(loadData, 10000);
    return () => clearInterval(timer);
  }, []);

  const filteredZones = selectedWarehouseId
    ? zones.filter(z => z.warehouseId === selectedWarehouseId)
    : zones;

  const totalSensors = filteredZones.reduce((sum, z) => sum + z.totalSensors, 0);
  const onlineSensors = filteredZones.reduce((sum, z) => sum + z.onlineSensors, 0);
  const activeAlertsCount = alerts.length;

  return (
    <div>
      {/* Top filter bar */}
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 24, flexWrap: 'wrap', gap: 16 }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
          <label style={{ fontSize: '0.875rem', color: 'var(--text-muted)', fontWeight: 500 }}>
            Kho hàng:
          </label>
          <select
            className="select"
            style={{ width: 280 }}
            value={selectedWarehouseId}
            onChange={(e) => setSelectedWarehouseId(e.target.value)}
          >
            {warehouses.map(w => (
              <option key={w.id} value={w.id}>{w.name} ({w.code})</option>
            ))}
          </select>
        </div>

        {/* Header KPI Summary Bar */}
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <div className="card" style={{ padding: '8px 16px', display: 'flex', alignItems: 'center', gap: 10, background: '#1e293b' }}>
            <Layers size={18} style={{ color: '#38bdf8' }} />
            <div>
              <div style={{ fontSize: '0.6875rem', color: 'var(--text-muted)' }}>Phân vùng</div>
              <div style={{ fontWeight: 700, fontSize: '0.9375rem' }}>{filteredZones.length} Zone</div>
            </div>
          </div>

          <div className="card" style={{ padding: '8px 16px', display: 'flex', alignItems: 'center', gap: 10, background: '#1e293b' }}>
            <Cpu size={18} style={{ color: '#34d399' }} />
            <div>
              <div style={{ fontSize: '0.6875rem', color: 'var(--text-muted)' }}>Cảm biến Online</div>
              <div style={{ fontWeight: 700, fontSize: '0.9375rem' }}>{onlineSensors} / {totalSensors}</div>
            </div>
          </div>

          <div className="card" style={{ padding: '8px 16px', display: 'flex', alignItems: 'center', gap: 10, background: activeAlertsCount > 0 ? '#450a0a' : '#1e293b', border: activeAlertsCount > 0 ? '1px solid #ef4444' : undefined }}>
            <AlertTriangle size={18} style={{ color: activeAlertsCount > 0 ? '#f87171' : '#94a3b8' }} />
            <div>
              <div style={{ fontSize: '0.6875rem', color: 'var(--text-muted)' }}>Cảnh báo Active</div>
              <div style={{ fontWeight: 700, fontSize: '0.9375rem', color: activeAlertsCount > 0 ? '#f87171' : 'inherit' }}>
                {activeAlertsCount}
              </div>
            </div>
          </div>

          <button className="btn btn-outline" onClick={loadData} title="Làm mới dữ liệu">
            <RefreshCw size={16} />
          </button>
        </div>
      </div>

      {/* Grid of ZoneCards */}
      <h2 style={{ fontSize: '1.125rem', fontWeight: 600, marginBottom: 16 }}>
        Danh Sách Phân Vùng Lưu Trữ & Giám Sát Vi Khí Hậu
      </h2>

      <div className="grid-3" style={{ marginBottom: 32 }}>
        {filteredZones.map(zone => {
          const isTempBreached = zone.currentTemp !== null && (zone.currentTemp < zone.tempMin || zone.currentTemp > zone.tempMax);
          const isHumidityBreached = zone.currentHumidity !== null && (zone.currentHumidity < zone.humidityMin || zone.currentHumidity > zone.humidityMax);

          return (
            <div
              key={zone.id}
              className="card"
              style={{
                cursor: 'pointer',
                transition: 'transform 0.15s, border-color 0.15s',
                border: isTempBreached || isHumidityBreached ? '1px solid #ef4444' : '1px solid var(--border-color)',
                display: 'flex',
                flexDirection: 'column',
                justifyContent: 'space-between'
              }}
              onClick={() => navigate(`/zones/${zone.id}`)}
              onMouseEnter={(e) => e.currentTarget.style.transform = 'translateY(-2px)'}
              onMouseLeave={(e) => e.currentTarget.style.transform = 'translateY(0)'}
            >
              <div>
                <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: 12 }}>
                  <div>
                    <h3 style={{ fontSize: '1.0625rem', fontWeight: 600, color: '#f8fafc' }}>{zone.name}</h3>
                    <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)' }}>Loại: {zone.zoneType} ({zone.code})</div>
                  </div>
                  <DataAgeBadge
                    lastReadingAt={zone.lastReadingAt}
                    initialStatus={zone.dataAgeStatus}
                    initialSeconds={zone.dataAgeSeconds}
                  />
                </div>

                <div style={{ display: 'flex', gap: 20, margin: '20px 0 16px' }}>
                  <div style={{ flex: 1 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, color: 'var(--text-muted)', fontSize: '0.75rem', marginBottom: 4 }}>
                      <Thermometer size={14} style={{ color: '#38bdf8' }} /> Nhiệt độ
                    </div>
                    <div style={{ fontSize: '1.75rem', fontWeight: 700, color: isTempBreached ? '#f87171' : '#38bdf8' }}>
                      {zone.currentTemp !== null && zone.currentTemp !== undefined ? `${zone.currentTemp.toFixed(1)} °C` : '--'}
                    </div>
                    <div style={{ fontSize: '0.6875rem', color: 'var(--text-dim)' }}>
                      Ngưỡng: {zone.tempMin}°C - {zone.tempMax}°C
                    </div>
                  </div>

                  <div style={{ flex: 1 }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: 6, color: 'var(--text-muted)', fontSize: '0.75rem', marginBottom: 4 }}>
                      <Droplets size={14} style={{ color: '#06b6d4' }} /> Độ ẩm
                    </div>
                    <div style={{ fontSize: '1.75rem', fontWeight: 700, color: isHumidityBreached ? '#f87171' : '#06b6d4' }}>
                      {zone.currentHumidity !== null && zone.currentHumidity !== undefined ? `${zone.currentHumidity.toFixed(1)} %` : '--'}
                    </div>
                    <div style={{ fontSize: '0.6875rem', color: 'var(--text-dim)' }}>
                      Ngưỡng: {zone.humidityMin}% - {zone.humidityMax}%
                    </div>
                  </div>
                </div>
              </div>

              <div style={{
                borderTop: '1px solid var(--border-color)',
                paddingTop: 12,
                display: 'flex',
                alignItems: 'center',
                justifyContent: 'space-between',
                fontSize: '0.8125rem'
              }}>
                <div style={{ color: 'var(--text-muted)' }}>
                  Cảm biến: <strong>{zone.onlineSensors}/{zone.totalSensors} Online</strong>
                </div>
                <div style={{ color: 'var(--primary)', fontWeight: 600, display: 'flex', alignItems: 'center', gap: 4 }}>
                  Chi tiết vùng <ChevronRight size={14} />
                </div>
              </div>
            </div>
          );
        })}
      </div>

      {/* Recent Alerts Panel (UC07) */}
      <div className="card">
        <div className="card-title">
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <AlertTriangle size={18} style={{ color: '#f59e0b' }} />
            <span>Sự Cố & Cảnh Báo Gần Đây (Real-time Stream)</span>
          </div>
          <button className="btn btn-outline btn-sm" onClick={() => navigate('/alerts')}>
            Xem tất cả cảnh báo
          </button>
        </div>

        {alerts.length === 0 ? (
          <div style={{ textAlign: 'center', padding: '24px 0', color: 'var(--text-muted)' }}>
            Không có cảnh báo nào đang hoạt động. Môi trường kho ổn định.
          </div>
        ) : (
          <div className="table-container">
            <table className="table">
              <thead>
                <tr>
                  <th>Thời gian</th>
                  <th>Mức độ</th>
                  <th>Phân vùng</th>
                  <th>Cảm biến</th>
                  <th>Nội dung vi phạm</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {alerts.slice(0, 5).map(a => (
                  <tr key={a.id}>
                    <td>{new Date(a.createdAt).toLocaleTimeString('vi-VN')}</td>
                    <td>
                      <span className={`badge ${a.severity === 'EMERGENCY' || a.severity === 'CRITICAL' ? 'badge-expired' : 'badge-stale'}`}>
                        {a.severity}
                      </span>
                    </td>
                    <td>{a.zoneName || 'Toàn vùng'}</td>
                    <td style={{ fontFamily: 'monospace' }}>{a.hardwareId || '--'}</td>
                    <td>{a.message}</td>
                    <td>
                      <span className="badge badge-stale">{a.status}</span>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>
    </div>
  );
}
