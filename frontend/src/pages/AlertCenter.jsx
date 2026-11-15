import React, { useState, useEffect } from 'react';
import { apiFetch } from '../api';
import {
  AlertTriangle,
  CheckCircle2,
  Clock,
  Filter,
  RefreshCw,
  Search,
  Check,
  FileCheck
} from 'lucide-react';

export default function AlertCenter() {
  const [alerts, setAlerts] = useState([]);
  const [statusFilter, setStatusFilter] = useState('');
  const [severityFilter, setSeverityFilter] = useState('');
  const [loading, setLoading] = useState(true);

  // Resolve modal state
  const [resolvingAlert, setResolvingAlert] = useState(null);
  const [resolutionNotes, setResolutionNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const loadAlerts = async () => {
    try {
      setLoading(true);
      let query = [];
      if (statusFilter) query.push(`status=${statusFilter}`);
      if (severityFilter) query.push(`severity=${severityFilter}`);
      const qs = query.length > 0 ? `?${query.join('&')}` : '';

      const res = await apiFetch(`/api/alerts${qs}`);
      setAlerts(res);
    } catch (err) {
      console.error(err);
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    loadAlerts();
    const timer = setInterval(loadAlerts, 10000);
    return () => clearInterval(timer);
  }, [statusFilter, severityFilter]);

  const handleAcknowledge = async (id) => {
    try {
      await apiFetch(`/api/alerts/${id}/acknowledge`, { method: 'POST' });
      await loadAlerts();
    } catch (err) {
      alert(err.message);
    }
  };

  const handleResolveSubmit = async (e) => {
    e.preventDefault();
    if (!resolvingAlert) return;
    try {
      setSubmitting(true);
      await apiFetch(`/api/alerts/${resolvingAlert.id}/resolve`, {
        method: 'POST',
        body: JSON.stringify({ resolutionNotes })
      });
      setResolvingAlert(null);
      setResolutionNotes('');
      await loadAlerts();
    } catch (err) {
      alert(err.message);
    } finally {
      setSubmitting(false);
    }
  };

  const emergencyCount = alerts.filter(a => a.severity === 'EMERGENCY' && a.status === 'ACTIVE').length;
  const criticalCount = alerts.filter(a => a.severity === 'CRITICAL' && a.status === 'ACTIVE').length;
  const warningCount = alerts.filter(a => a.severity === 'WARNING' && a.status === 'ACTIVE').length;
  const infoCount = alerts.filter(a => a.severity === 'INFO' && a.status === 'ACTIVE').length;

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <div>
          <h1 style={{ fontSize: '1.5rem', fontWeight: 700 }}>Trung Tâm Tiếp Nhận & Xử Lý Cảnh Báo (AlertCenter — UC07)</h1>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.875rem' }}>
            Phân cấp 4 mức độ nghiêm trọng (BR13) và quy trình xử lý vòng đời: ACTIVE &rarr; ACKNOWLEDGED &rarr; RESOLVED
          </p>
        </div>

        <button className="btn btn-outline" onClick={loadAlerts}>
          <RefreshCw size={16} /> Làm mới
        </button>
      </div>

      {/* Severity Pill Counters */}
      <div style={{ display: 'flex', gap: 12, marginBottom: 24, flexWrap: 'wrap' }}>
        <div className="card" style={{ flex: 1, padding: '12px 16px', background: emergencyCount > 0 ? '#450a0a' : '#1e293b' }}>
          <div style={{ fontSize: '0.75rem', color: '#f87171', fontWeight: 700 }}>EMERGENCY</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#f87171' }}>{emergencyCount}</div>
        </div>

        <div className="card" style={{ flex: 1, padding: '12px 16px', background: criticalCount > 0 ? '#450a0a' : '#1e293b' }}>
          <div style={{ fontSize: '0.75rem', color: '#f87171', fontWeight: 700 }}>CRITICAL</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#f87171' }}>{criticalCount}</div>
        </div>

        <div className="card" style={{ flex: 1, padding: '12px 16px', background: warningCount > 0 ? '#78350f' : '#1e293b' }}>
          <div style={{ fontSize: '0.75rem', color: '#fbbf24', fontWeight: 700 }}>WARNING</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#fbbf24' }}>{warningCount}</div>
        </div>

        <div className="card" style={{ flex: 1, padding: '12px 16px', background: '#1e293b' }}>
          <div style={{ fontSize: '0.75rem', color: '#38bdf8', fontWeight: 700 }}>INFO</div>
          <div style={{ fontSize: '1.5rem', fontWeight: 700, color: '#38bdf8' }}>{infoCount}</div>
        </div>
      </div>

      {/* Filter Bar */}
      <div className="card" style={{ marginBottom: 20, padding: 14 }}>
        <div style={{ display: 'flex', gap: 14, alignItems: 'center', flexWrap: 'wrap' }}>
          <Filter size={16} style={{ color: 'var(--text-muted)' }} />

          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: '0.8125rem', color: 'var(--text-muted)' }}>Trạng thái:</span>
            <select
              className="select"
              style={{ width: 150 }}
              value={statusFilter}
              onChange={(e) => setStatusFilter(e.target.value)}
            >
              <option value="">Tất cả</option>
              <option value="ACTIVE">ACTIVE</option>
              <option value="ACKNOWLEDGED">ACKNOWLEDGED</option>
              <option value="RESOLVED">RESOLVED</option>
            </select>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <span style={{ fontSize: '0.8125rem', color: 'var(--text-muted)' }}>Mức độ (Severity):</span>
            <select
              className="select"
              style={{ width: 150 }}
              value={severityFilter}
              onChange={(e) => setSeverityFilter(e.target.value)}
            >
              <option value="">Tất cả</option>
              <option value="EMERGENCY">EMERGENCY</option>
              <option value="CRITICAL">CRITICAL</option>
              <option value="WARNING">WARNING</option>
              <option value="INFO">INFO</option>
            </select>
          </div>
        </div>
      </div>

      {/* Alerts Table */}
      <div className="card">
        <div className="table-container">
          <table className="table">
            <thead>
              <tr>
                <th>Thời gian phát sinh</th>
                <th>Mức độ (Severity)</th>
                <th>Phân vùng & Cảm biến</th>
                <th>Thông báo sự cố</th>
                <th>Lần xuất hiện</th>
                <th>Trạng thái & Thao tác</th>
              </tr>
            </thead>
            <tbody>
              {alerts.length === 0 ? (
                <tr>
                  <td colSpan={6} style={{ textAlign: 'center', padding: '32px 0', color: 'var(--text-muted)' }}>
                    Không tìm thấy cảnh báo nào phù hợp với bộ lọc.
                  </td>
                </tr>
              ) : (
                alerts.map(a => (
                  <tr key={a.id}>
                    <td>
                      <div>{new Date(a.createdAt).toLocaleTimeString('vi-VN')}</div>
                      <div style={{ fontSize: '0.6875rem', color: 'var(--text-dim)' }}>
                        {new Date(a.createdAt).toLocaleDateString('vi-VN')}
                      </div>
                    </td>
                    <td>
                      <span className={`badge ${a.severity === 'EMERGENCY' || a.severity === 'CRITICAL' ? 'badge-expired' : a.severity === 'WARNING' ? 'badge-stale' : 'badge-aging'}`}>
                        {a.severity}
                      </span>
                    </td>
                    <td>
                      <div style={{ fontWeight: 600 }}>{a.zoneName || 'Cấp phân vùng'}</div>
                      <div style={{ fontSize: '0.75rem', fontFamily: 'monospace', color: 'var(--text-muted)' }}>
                        {a.hardwareId || '--'}
                      </div>
                    </td>
                    <td style={{ maxWidth: 300 }}>
                      <div style={{ fontWeight: 500 }}>{a.message}</div>
                      {a.resolutionNotes && (
                        <div style={{ fontSize: '0.75rem', color: '#34d399', marginTop: 4 }}>
                          Khắc phục: {a.resolutionNotes} ({a.resolvedByName})
                        </div>
                      )}
                    </td>
                    <td style={{ textAlign: 'center', fontWeight: 600 }}>
                      {a.occurrenceCount > 1 ? (
                        <span style={{ color: '#fbbf24' }}>{a.occurrenceCount}x</span>
                      ) : (
                        <span style={{ color: 'var(--text-dim)' }}>1</span>
                      )}
                    </td>
                    <td>
                      <div style={{ display: 'flex', gap: 6, alignItems: 'center' }}>
                        {a.status === 'ACTIVE' && (
                          <button
                            className="btn btn-sm btn-secondary"
                            onClick={() => handleAcknowledge(a.id)}
                            title="Xác nhận tiếp nhận sự cố"
                          >
                            <Check size={12} /> Tiếp nhận
                          </button>
                        )}

                        {a.status !== 'RESOLVED' ? (
                          <button
                            className="btn btn-sm btn-primary"
                            onClick={() => {
                              setResolvingAlert(a);
                              setResolutionNotes('');
                            }}
                            title="Giải quyết và đóng cảnh báo kèm biên bản"
                          >
                            <FileCheck size={12} /> Đóng cảnh báo
                          </button>
                        ) : (
                          <span className="badge badge-fresh">Đã đóng</span>
                        )}
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Resolve Modal Dialog */}
      {resolvingAlert && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3 style={{ fontSize: '1.25rem', fontWeight: 600, marginBottom: 12 }}>
              Đóng Cảnh Báo Sự Cố (Resolve Alert)
            </h3>
            <p style={{ color: 'var(--text-muted)', fontSize: '0.8125rem', marginBottom: 16 }}>
              Vùng: <strong>{resolvingAlert.zoneName}</strong> | Cảm biến: <strong>{resolvingAlert.hardwareId || '--'}</strong>
              <br />
              Nội dung: {resolvingAlert.message}
            </p>

            <form onSubmit={handleResolveSubmit}>
              <div style={{ marginBottom: 20 }}>
                <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                  Ghi chú nguyên nhân & Biện pháp khắc phục (*)
                </label>
                <textarea
                  className="input"
                  rows={4}
                  placeholder="Ví dụ: Đã kiểm tra hệ thống điều hòa kho lạnh, cửa kho đã được đóng kín, cảm biến đã hồi phục tín hiệu ổn định..."
                  value={resolutionNotes}
                  onChange={(e) => setResolutionNotes(e.target.value)}
                  required
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setResolvingAlert(null)}
                  disabled={submitting}
                >
                  Hủy bỏ
                </button>
                <button type="submit" className="btn btn-primary" disabled={submitting}>
                  {submitting ? 'Đang lưu...' : 'Hoàn tất & Đóng cảnh báo'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
