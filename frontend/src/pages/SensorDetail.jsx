import React, { useState, useEffect } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { apiFetch } from '../api';
import ConnectionBadge from '../components/ConnectionBadge';
import DataAgeBadge from '../components/DataAgeBadge';
import {
  ArrowLeft,
  Cpu,
  Activity,
  History,
  Wrench,
  Clock,
  Battery,
  Radio,
  CheckCircle2,
  AlertTriangle
} from 'lucide-react';

export default function SensorDetail() {
  const { id } = useParams();
  const navigate = useNavigate();

  const [data, setData] = useState(null);
  const [loading, setLoading] = useState(true);
  const [isCalibrating, setIsCalibrating] = useState(false);
  const [calibForm, setCalibForm] = useState({
    newOffset: 0,
    standardInstrumentInfo: 'Nhiệt kế chuẩn Fluke 1524',
    nextDueDate: new Date(Date.now() + 180 * 24 * 3600 * 1000).toISOString().split('T')[0],
    notes: 'Hiệu chuẩn định kỳ tại hiện trường'
  });
  const [submitting, setSubmitting] = useState(false);

  const loadData = async () => {
    try {
      setLoading(true);
      const res = await apiFetch(`/api/sensors/${id}`);
      setData(res);
      if (res.sensor) {
        setCalibForm(prev => ({ ...prev, newOffset: res.sensor.calibrationOffset || 0 }));
      }
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
  }, [id]);

  const handleCalibrationSubmit = async (e) => {
    e.preventDefault();
    try {
      setSubmitting(true);
      await apiFetch(`/api/sensors/${id}/calibration`, {
        method: 'POST',
        body: JSON.stringify({
          newOffset: parseFloat(calibForm.newOffset),
          standardInstrumentInfo: calibForm.standardInstrumentInfo,
          nextDueDate: new Date(calibForm.nextDueDate).toISOString(),
          notes: calibForm.notes
        })
      });
      setIsCalibrating(false);
      await loadData();
      alert('Đã lưu kết quả hiệu chuẩn và đặt lại hồ sơ CUSUM Drift về 0 thành công (BR09, BR10).');
    } catch (err) {
      alert(err.message);
    } finally {
      setSubmitting(false);
    }
  };

  if (!data?.sensor) {
    return (
      <div style={{ textAlign: 'center', padding: '60px 0' }}>
        <p>Đang tải thông tin chi tiết cảm biến...</p>
      </div>
    );
  }

  const { sensor, driftProfiles = [], dataGaps = [], calibrations = [] } = data;

  const fsmSteps = ['NEVER_CONNECTED', 'CONNECTED', 'STALE', 'DISCONNECTED'];
  const currentState = sensor.connectionState || 'NEVER_CONNECTED';

  const tempDrift = driftProfiles.find(p => p.metricType === 'TEMPERATURE');
  const humDrift = driftProfiles.find(p => p.metricType === 'HUMIDITY');

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <button className="btn btn-outline" onClick={() => navigate(-1)}>
          <ArrowLeft size={16} /> Quay lại
        </button>

        <button className="btn btn-primary" onClick={() => setIsCalibrating(true)}>
          <Wrench size={16} /> Ghi nhận hiệu chuẩn (UC09)
        </button>
      </div>

      {/* Top Header Card */}
      <div className="card" style={{ marginBottom: 24 }}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', flexWrap: 'wrap', gap: 16 }}>
          <div>
            <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 6 }}>
              <h1 style={{ fontSize: '1.5rem', fontWeight: 700, fontFamily: 'monospace' }}>
                {sensor.hardwareId}
              </h1>
              <ConnectionBadge state={sensor.connectionState} />
              <span className={`badge ${sensor.status === 'ONLINE' ? 'badge-fresh' : sensor.status === 'CALIBRATING' ? 'badge-stale' : 'badge-expired'}`}>
                {sensor.status}
              </span>
            </div>
            <div style={{ color: 'var(--text-muted)', fontSize: '0.875rem' }}>
              Vùng lắp đặt: <strong>{sensor.zoneName || 'Chưa gán (Kho dự phòng)'}</strong> | Kho: <strong>{sensor.warehouseName || '--'}</strong>
            </div>
          </div>

          <div>
            <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: 4 }}>Tuổi dữ liệu (BR11)</div>
            <DataAgeBadge
              lastReadingAt={sensor.lastReadingAt}
              initialStatus={data.dataAgeStatus}
              initialSeconds={data.dataAgeSeconds}
            />
          </div>
        </div>
      </div>

      {/* Hardware Metadata & FSM Visualizer Grid */}
      <div className="grid-2" style={{ marginBottom: 24 }}>
        {/* Hardware Meta Card */}
        <div className="card">
          <div className="card-title">
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <Cpu size={18} style={{ color: '#38bdf8' }} />
              <span>Thông Số Kỹ Thuật Phần Cứng</span>
            </div>
          </div>

          <div style={{ display: 'flex', flexDirection: 'column', gap: 12, fontSize: '0.875rem' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid var(--border-color)', paddingBottom: 8 }}>
              <span style={{ color: 'var(--text-muted)' }}>Loại cảm biến:</span>
              <span style={{ fontWeight: 600 }}>{sensor.sensorType}</span>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid var(--border-color)', paddingBottom: 8 }}>
              <span style={{ color: 'var(--text-muted)' }}>Chu kỳ gửi dữ liệu (BR03):</span>
              <span style={{ fontWeight: 600 }}>{sensor.expectedIntervalSec} giây ({sensor.expectedIntervalSec / 60} phút)</span>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid var(--border-color)', paddingBottom: 8 }}>
              <span style={{ color: 'var(--text-muted)' }}>Ngưỡng STALE (BR05):</span>
              <span style={{ fontWeight: 600, color: '#fbbf24' }}>{sensor.staleThresholdSec}s (2x interval)</span>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid var(--border-color)', paddingBottom: 8 }}>
              <span style={{ color: 'var(--text-muted)' }}>Ngưỡng DISCONNECTED (BR06):</span>
              <span style={{ fontWeight: 600, color: '#f87171' }}>{sensor.disconnectThresholdSec}s (5x interval)</span>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between', borderBottom: '1px solid var(--border-color)', paddingBottom: 8 }}>
              <span style={{ color: 'var(--text-muted)' }}>Mức Pin & Sóng RSSI:</span>
              <span style={{ fontWeight: 600 }}>
                {sensor.batteryLevel ?? '--'}% | {sensor.lastRssi ?? '--'} dBm
              </span>
            </div>

            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: 'var(--text-muted)' }}>Firmware:</span>
              <span style={{ fontWeight: 600 }}>{sensor.firmwareVersion}</span>
            </div>
          </div>
        </div>

        {/* BR04: Máy Trạng Thái FSM Visualizer */}
        <div className="card">
          <div className="card-title">
            <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
              <Activity size={18} style={{ color: '#10b981' }} />
              <span>Máy Trạng Thái Kết Nối FSM (BR04, BR05, BR06)</span>
            </div>
          </div>

          <p style={{ color: 'var(--text-muted)', fontSize: '0.8125rem', marginBottom: 20 }}>
            Tự động giám sát chu kỳ thu thập; chuyển sang STALE sau 2x và DISCONNECTED sau 5x interval.
          </p>

          <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', position: 'relative' }}>
            {fsmSteps.map((step, idx) => {
              const isActive = currentState === step;
              return (
                <React.Fragment key={step}>
                  <div style={{
                    display: 'flex',
                    flexDirection: 'column',
                    alignItems: 'center',
                    gap: 6,
                    zIndex: 2
                  }}>
                    <div style={{
                      width: 38,
                      height: 38,
                      borderRadius: '50%',
                      backgroundColor: isActive ? '#2563eb' : '#0f172a',
                      border: isActive ? '2px solid #38bdf8' : '2px solid var(--border-color)',
                      display: 'flex',
                      alignItems: 'center',
                      justifyContent: 'center',
                      fontWeight: 700,
                      fontSize: '0.8125rem',
                      color: isActive ? '#fff' : 'var(--text-dim)',
                      boxShadow: isActive ? '0 0 12px rgba(56, 189, 248, 0.5)' : 'none'
                    }}>
                      {idx + 1}
                    </div>
                    <span style={{
                      fontSize: '0.6875rem',
                      fontWeight: isActive ? 700 : 500,
                      color: isActive ? '#38bdf8' : 'var(--text-dim)',
                      textAlign: 'center'
                    }}>
                      {step}
                    </span>
                  </div>
                  {idx < fsmSteps.length - 1 && (
                    <div style={{
                      flex: 1,
                      height: 2,
                      backgroundColor: 'var(--border-color)',
                      margin: '0 8px',
                      position: 'relative',
                      top: -12
                    }} />
                  )}
                </React.Fragment>
              );
            })}
          </div>

          <div style={{
            marginTop: 24,
            padding: '12px 16px',
            backgroundColor: '#0f172a',
            borderRadius: 6,
            border: '1px solid var(--border-color)',
            fontSize: '0.8125rem'
          }}>
            Trạng thái hiện tại: <strong>{currentState}</strong>
            <br />
            Lần nhận reading cuối: <strong>{sensor.lastReadingAt ? new Date(sensor.lastReadingAt).toLocaleString('vi-VN') : 'Chưa có'}</strong>
          </div>
        </div>
      </div>

      {/* CUSUM Drift Detection Card (BR09, BR10) */}
      <div className="card" style={{ marginBottom: 24 }}>
        <div className="card-title">
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Activity size={18} style={{ color: '#f59e0b' }} />
            <span>Hồ Sơ Trôi Dạt Chỉ Số CUSUM (Cumulative Sum Drift Detection — BR09, BR10)</span>
          </div>
        </div>

        <div className="grid-2">
          {/* Temperature Drift */}
          <div style={{ padding: 16, background: '#0f172a', borderRadius: 8, border: '1px solid var(--border-color)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
              <strong style={{ color: '#38bdf8' }}>Chỉ số Nhiệt Độ</strong>
              <span className={`badge ${tempDrift?.driftSeverity === 'NONE' ? 'badge-fresh' : tempDrift?.driftSeverity === 'MODERATE' ? 'badge-stale' : 'badge-expired'}`}>
                {tempDrift?.driftSeverity || 'CHƯA ĐÁNH GIÁ'}
              </span>
            </div>
            <div style={{ fontSize: '0.8125rem', display: 'flex', flexDirection: 'column', gap: 6 }}>
              <div>Tốc độ trôi dạt: <strong>{tempDrift?.driftRatePerHour ?? 0} °C/h</strong> (Ngưỡng cảnh báo: 0.1°C/h)</div>
              <div>Tổng tích lũy CUSUM S+: <strong>{tempDrift?.cusumPositive ?? 0}</strong> | S-: <strong>{tempDrift?.cusumNegative ?? 0}</strong></div>
              <div>Yêu cầu hiệu chuẩn: <strong>{tempDrift?.requiresCalibration ? 'CẦN HIỆU CHUẨN GẤP' : 'Bình thường'}</strong></div>
            </div>
          </div>

          {/* Humidity Drift */}
          <div style={{ padding: 16, background: '#0f172a', borderRadius: 8, border: '1px solid var(--border-color)' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 12 }}>
              <strong style={{ color: '#06b6d4' }}>Chỉ số Độ Ẩm</strong>
              <span className={`badge ${humDrift?.driftSeverity === 'NONE' ? 'badge-fresh' : humDrift?.driftSeverity === 'MODERATE' ? 'badge-stale' : 'badge-expired'}`}>
                {humDrift?.driftSeverity || 'CHƯA ĐÁNH GIÁ'}
              </span>
            </div>
            <div style={{ fontSize: '0.8125rem', display: 'flex', flexDirection: 'column', gap: 6 }}>
              <div>Tốc độ trôi dạt: <strong>{humDrift?.driftRatePerHour ?? 0} %RH/h</strong></div>
              <div>Tổng tích lũy CUSUM S+: <strong>{humDrift?.cusumPositive ?? 0}</strong> | S-: <strong>{humDrift?.cusumNegative ?? 0}</strong></div>
              <div>Yêu cầu hiệu chuẩn: <strong>{humDrift?.requiresCalibration ? 'CẦN HIỆU CHUẨN GẤP' : 'Bình thường'}</strong></div>
            </div>
          </div>
        </div>
      </div>

      {/* DataGaps History Table (BR07) */}
      <div className="card" style={{ marginBottom: 24 }}>
        <div className="card-title">
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <History size={18} style={{ color: '#ef4444' }} />
            <span>Lịch Sử Khoảng Trống Dữ Liệu (DataGaps Tracking — BR07)</span>
          </div>
        </div>

        {dataGaps.length === 0 ? (
          <p style={{ color: 'var(--text-muted)', fontSize: '0.875rem' }}>
            Không có khoảng gián đoạn dữ liệu nào được ghi nhận. Dữ liệu liên tục 100%.
          </p>
        ) : (
          <div className="table-container">
            <table className="table">
              <thead>
                <tr>
                  <th>Thời điểm bắt đầu gap</th>
                  <th>Thời điểm kết thúc gap</th>
                  <th>Thời lượng gián đoạn</th>
                  <th>Nguyên nhân (Reason)</th>
                  <th>Trạng thái</th>
                </tr>
              </thead>
              <tbody>
                {dataGaps.map(g => (
                  <tr key={g.id}>
                    <td>{new Date(g.startTime).toLocaleString('vi-VN')}</td>
                    <td>{g.endTime ? new Date(g.endTime).toLocaleString('vi-VN') : 'ĐANG MẤT TÍN HIỆU (Mở)'}</td>
                    <td>{g.durationSec ? `${Math.round(g.durationSec / 60)} phút (${g.durationSec}s)` : 'Chưa xác định'}</td>
                    <td>
                      <span className="badge badge-expired">{g.reason}</span>
                    </td>
                    <td>
                      {g.endTime ? (
                        <span className="badge badge-fresh">Đã đóng</span>
                      ) : (
                        <span className="badge badge-expired">Đang mở</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Calibration History Table */}
      <div className="card">
        <div className="card-title">
          <div style={{ display: 'flex', alignItems: 'center', gap: 8 }}>
            <Wrench size={18} style={{ color: '#10b981' }} />
            <span>Lịch Sử Hiệu Chuẩn & Cân Chỉnh Thiết Bị (Calibration Records)</span>
          </div>
        </div>

        {calibrations.length === 0 ? (
          <p style={{ color: 'var(--text-muted)', fontSize: '0.875rem' }}>
            Chưa có biên bản hiệu chuẩn nào.
          </p>
        ) : (
          <div className="table-container">
            <table className="table">
              <thead>
                <tr>
                  <th>Ngày hiệu chuẩn</th>
                  <th>Kỹ thuật viên</th>
                  <th>Hệ số bù cũ</th>
                  <th>Hệ số bù mới</th>
                  <th>Thiết bị đo chuẩn sử dụng</th>
                  <th>Hạn hiệu chuẩn tiếp theo</th>
                </tr>
              </thead>
              <tbody>
                {calibrations.map(c => (
                  <tr key={c.id}>
                    <td>{new Date(c.calibrationDate).toLocaleDateString('vi-VN')}</td>
                    <td>{c.technicianName}</td>
                    <td>{c.oldOffset ?? 0} °C</td>
                    <td style={{ fontWeight: 600, color: '#34d399' }}>{c.newOffset} °C</td>
                    <td>{c.standardInstrumentInfo}</td>
                    <td>{new Date(c.nextDueDate).toLocaleDateString('vi-VN')}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
      </div>

      {/* Calibration Dialog Modal (UC09) */}
      {isCalibrating && (
        <div className="modal-overlay">
          <div className="modal-content">
            <h3 style={{ fontSize: '1.25rem', fontWeight: 600, marginBottom: 12 }}>
              Ghi Nhận Biên Bản Hiệu Chuẩn Cảm Biến (UC09)
            </h3>
            <p style={{ color: 'var(--text-muted)', fontSize: '0.8125rem', marginBottom: 20 }}>
              Sau khi lưu, hệ thống sẽ tự động <strong>reset hồ sơ CUSUM Drift về 0</strong> và đưa trạng thái cảm biến về <strong>ONLINE</strong> (BR09).
            </p>

            <form onSubmit={handleCalibrationSubmit}>
              <div style={{ marginBottom: 16 }}>
                <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6 }}>
                  Hệ số bù sai lệch mới (Offset °C)
                </label>
                <input
                  type="number"
                  step="0.001"
                  className="input"
                  value={calibForm.newOffset}
                  onChange={(e) => setCalibForm({ ...calibForm, newOffset: e.target.value })}
                  required
                />
              </div>

              <div style={{ marginBottom: 16 }}>
                <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6 }}>
                  Thông tin thiết bị đo chuẩn đối chứng
                </label>
                <input
                  type="text"
                  className="input"
                  value={calibForm.standardInstrumentInfo}
                  onChange={(e) => setCalibForm({ ...calibForm, standardInstrumentInfo: e.target.value })}
                  required
                />
              </div>

              <div style={{ marginBottom: 16 }}>
                <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6 }}>
                  Hạn hiệu chuẩn tiếp theo (Next Due Date)
                </label>
                <input
                  type="date"
                  className="input"
                  value={calibForm.nextDueDate}
                  onChange={(e) => setCalibForm({ ...calibForm, nextDueDate: e.target.value })}
                  required
                />
              </div>

              <div style={{ marginBottom: 24 }}>
                <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6 }}>
                  Ghi chú kỹ thuật
                </label>
                <textarea
                  className="input"
                  rows={3}
                  value={calibForm.notes}
                  onChange={(e) => setCalibForm({ ...calibForm, notes: e.target.value })}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setIsCalibrating(false)}
                  disabled={submitting}
                >
                  Hủy bỏ
                </button>
                <button type="submit" className="btn btn-primary" disabled={submitting}>
                  {submitting ? 'Đang lưu...' : 'Lưu kết quả & Reset CUSUM'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
