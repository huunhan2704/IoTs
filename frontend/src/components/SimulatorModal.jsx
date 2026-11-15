import React, { useState, useEffect } from 'react';
import { apiFetch } from '../api';
import { Play, Pause, Wifi, WifiOff, AlertTriangle, Activity, RefreshCw } from 'lucide-react';

export default function SimulatorModal({ isOpen, onClose }) {
  const [status, setStatus] = useState(null);
  const [loading, setLoading] = useState(false);

  const fetchStatus = async () => {
    try {
      const res = await apiFetch('/api/simulator/status');
      setStatus(res);
    } catch (err) {
      console.error(err);
    }
  };

  useEffect(() => {
    if (isOpen) {
      fetchStatus();
      const timer = setInterval(fetchStatus, 3000);
      return () => clearInterval(timer);
    }
  }, [isOpen]);

  if (!isOpen) return null;

  const toggleOffline = async (hw) => {
    try {
      setLoading(true);
      await apiFetch(`/api/simulator/sensor/${hw}/toggle-offline`, { method: 'POST' });
      await fetchStatus();
    } finally {
      setLoading(false);
    }
  };

  const setDrift = async (hw, rate) => {
    try {
      setLoading(true);
      await apiFetch(`/api/simulator/sensor/${hw}/drift?driftPerHour=${rate}`, { method: 'POST' });
      await fetchStatus();
    } finally {
      setLoading(false);
    }
  };

  const toggleExcursion = async (hw) => {
    try {
      setLoading(true);
      await apiFetch(`/api/simulator/sensor/${hw}/excursion`, { method: 'POST' });
      await fetchStatus();
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="modal-overlay">
      <div className="modal-content" style={{ maxWidth: 750 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 16 }}>
          <div>
            <h2 style={{ fontSize: '1.25rem', fontWeight: 600 }}>Bộ Giả Lập Mạng Cảm Biến IoT & Kịch Bản Thử Nghiệm</h2>
            <p style={{ color: 'var(--text-muted)', fontSize: '0.8125rem' }}>
              Kiểm thử trực tiếp FSM Disconnection (BR04), DataGap (BR07), CUSUM Drift (BR09) & Vượt ngưỡng (BR12)
            </p>
          </div>
          <button className="btn btn-outline btn-sm" onClick={fetchStatus}>
            <RefreshCw size={14} /> Làm mới
          </button>
        </div>

        {status && (
          <div style={{ marginBottom: 16, display: 'flex', gap: 12 }}>
            <div style={{ flex: 1, background: '#0f172a', padding: '10px 14px', borderRadius: 6, border: '1px solid var(--border-color)' }}>
              <div style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>MQTT Broker nhúng</div>
              <div style={{ fontWeight: 600, color: status.brokerRunning ? '#34d399' : '#f87171' }}>
                {status.brokerRunning ? `Đang lắng nghe (Cổng ${status.brokerPort})` : 'Chưa bật'}
              </div>
            </div>
            <div style={{ flex: 1, background: '#0f172a', padding: '10px 14px', borderRadius: 6, border: '1px solid var(--border-color)' }}>
              <div style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>Telemetry Ingested</div>
              <div style={{ fontWeight: 600, color: '#38bdf8' }}>
                {status.metrics?.accepted || 0} bản ghi hợp lệ
              </div>
            </div>
            <div style={{ flex: 1, background: '#0f172a', padding: '10px 14px', borderRadius: 6, border: '1px solid var(--border-color)' }}>
              <div style={{ color: 'var(--text-muted)', fontSize: '0.75rem' }}>Bản tin từ chối (Rejected)</div>
              <div style={{ fontWeight: 600, color: status.metrics?.rejectedTotal > 0 ? '#fbbf24' : '#94a3b8' }}>
                {status.metrics?.rejectedTotal || 0} bản tin
              </div>
            </div>
          </div>
        )}

        <div className="table-container" style={{ maxHeight: 360 }}>
          <table className="table">
            <thead>
              <tr>
                <th>Cảm biến</th>
                <th>Trạng thái mô phỏng</th>
                <th>Độ trôi (Drift)</th>
                <th>Mức Pin</th>
                <th>Hành động thử nghiệm</th>
              </tr>
            </thead>
            <tbody>
              {status?.sensors?.map(s => (
                <tr key={s.hardwareId}>
                  <td style={{ fontWeight: 600, fontFamily: 'monospace' }}>{s.hardwareId}</td>
                  <td>
                    {s.forcedOffline ? (
                      <span className="badge badge-disconnected"><WifiOff size={12} /> Đang ngắt mạng</span>
                    ) : (
                      <span className="badge badge-connected"><Wifi size={12} /> Gửi đều</span>
                    )}
                    {s.excursion && (
                      <span className="badge badge-expired" style={{ marginLeft: 4 }}>Vượt ngưỡng</span>
                    )}
                  </td>
                  <td>
                    {s.driftPerHour !== 0 ? (
                      <span style={{ color: '#fbbf24', fontWeight: 600 }}>+{s.driftPerHour}°C/h</span>
                    ) : (
                      <span style={{ color: 'var(--text-muted)' }}>0.00°C/h</span>
                    )}
                  </td>
                  <td>{Math.round(s.battery)}%</td>
                  <td>
                    <div style={{ display: 'flex', gap: 6 }}>
                      <button
                        className={`btn btn-sm ${s.forcedOffline ? 'btn-primary' : 'btn-danger'}`}
                        disabled={loading}
                        onClick={() => toggleOffline(s.hardwareId)}
                        title={s.forcedOffline ? 'Tái kết nối cảm biến' : 'Cắt tín hiệu để tạo DataGap'}
                      >
                        {s.forcedOffline ? <Wifi size={12} /> : <WifiOff size={12} />}
                        {s.forcedOffline ? 'Kết nối lại' : 'Cắt mạng'}
                      </button>

                      <button
                        className={`btn btn-sm ${s.excursion ? 'btn-secondary' : 'btn-outline'}`}
                        disabled={loading}
                        onClick={() => toggleExcursion(s.hardwareId)}
                        title="Tạo vi phạm ngưỡng nhiệt độ"
                      >
                        <AlertTriangle size={12} />
                        {s.excursion ? 'Hạ nhiệt' : 'Vọt nhiệt'}
                      </button>

                      {s.driftPerHour === 0 ? (
                        <button
                          className="btn btn-sm btn-outline"
                          disabled={loading}
                          onClick={() => setDrift(s.hardwareId, 0.20)}
                          title="Bơm sai số trôi dạt 0.20°C/h"
                        >
                          <Activity size={12} /> Bơm Drift
                        </button>
                      ) : (
                        <button
                          className="btn btn-sm btn-outline"
                          disabled={loading}
                          onClick={() => setDrift(s.hardwareId, 0)}
                          title="Xóa sai số trôi"
                        >
                          Reset Drift
                        </button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>

        <div style={{ marginTop: 20, display: 'flex', justifyContent: 'flex-end' }}>
          <button className="btn btn-secondary" onClick={onClose}>Đóng cửa sổ</button>
        </div>
      </div>
    </div>
  );
}
