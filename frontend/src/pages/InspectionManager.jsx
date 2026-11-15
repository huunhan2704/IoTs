import React, { useState, useEffect } from 'react';
import { apiFetch } from '../api';
import {
  ClipboardCheck,
  Plus,
  RefreshCw,
  CheckCircle,
  XCircle,
  AlertCircle,
  Calendar
} from 'lucide-react';

export default function InspectionManager() {
  const [inspections, setInspections] = useState([]);
  const [zones, setZones] = useState([]);
  const [loading, setLoading] = useState(true);

  // New Inspection Modal
  const [isCreating, setIsCreating] = useState(false);
  const [selectedZoneId, setSelectedZoneId] = useState('');
  const [sensorsInZone, setSensorsInZone] = useState([]);
  const [itemsForm, setItemsForm] = useState([]);
  const [inspectionNotes, setInspectionNotes] = useState('');
  const [submitting, setSubmitting] = useState(false);

  const loadData = async () => {
    try {
      setLoading(true);
      const [inspList, whList] = await Promise.all([
        apiFetch('/api/inspections'),
        apiFetch('/api/warehouses')
      ]);
      setInspections(inspList);

      const allZones = whList.flatMap(w => w.zones || []);
      setZones(allZones);
      if (allZones.length > 0 && !selectedZoneId) {
        setSelectedZoneId(allZones[0].id);
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

  // When zone changes in form, load sensors
  useEffect(() => {
    if (!selectedZoneId || !isCreating) return;
    const fetchZoneSensors = async () => {
      try {
        const res = await apiFetch(`/api/zones/${selectedZoneId}`);
        const sList = res.sensors || [];
        setSensorsInZone(sList);
        setItemsForm(sList.map(s => ({
          sensorId: s.id,
          hardwareId: s.hardwareId,
          checklistItem: 'Kiểm tra đối chứng nhiệt kế chuẩn & vỏ bọc',
          referenceValue: s.latestTemp !== null && s.latestTemp !== undefined ? s.latestTemp : 4.0,
          sensorReadingValue: s.latestTemp !== null && s.latestTemp !== undefined ? s.latestTemp : 4.0,
          result: 'PASS',
          notes: ''
        })));
      } catch (err) {
        console.error(err);
      }
    };
    fetchZoneSensors();
  }, [selectedZoneId, isCreating]);

  const handleItemChange = (idx, field, val) => {
    const updated = [...itemsForm];
    updated[idx][field] = val;

    // Auto evaluate PASS / FAIL based on deviation (BR14: +/- 0.5°C)
    if (field === 'referenceValue' || field === 'sensorReadingValue') {
      const ref = parseFloat(updated[idx].referenceValue);
      const reading = parseFloat(updated[idx].sensorReadingValue);
      if (!isNaN(ref) && !isNaN(reading)) {
        const dev = Math.abs(reading - ref);
        updated[idx].result = dev <= 0.5 ? 'PASS' : 'FAIL';
      }
    }
    setItemsForm(updated);
  };

  const handleCreateSubmit = async (e) => {
    e.preventDefault();
    if (itemsForm.length === 0) {
      alert('Vùng được chọn không có cảm biến nào.');
      return;
    }

    try {
      setSubmitting(true);
      await apiFetch('/api/inspections', {
        method: 'POST',
        body: JSON.stringify({
          zoneId: selectedZoneId,
          inspectionDate: new Date().toISOString().split('T')[0],
          notes: inspectionNotes,
          items: itemsForm.map(it => ({
            sensorId: it.sensorId,
            checklistItem: it.checklistItem,
            referenceValue: parseFloat(it.referenceValue),
            sensorReadingValue: parseFloat(it.sensorReadingValue),
            result: it.result,
            notes: it.notes
          }))
        })
      });

      setIsCreating(false);
      setInspectionNotes('');
      await loadData();
      alert('Đã lưu biên bản kiểm tra thực địa thành công! Thiết bị FAIL (nếu có) đã tự động chuyển sang trạng thái CALIBRATING (BR14).');
    } catch (err) {
      alert(err.message);
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: 20 }}>
        <div>
          <h1 style={{ fontSize: '1.5rem', fontWeight: 700 }}>Kiểm Tra Hiện Trường & Đối Chứng Chuẩn (Inspection — UC08)</h1>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.875rem' }}>
            Quy trình đối chứng nhiệt kế chuẩn (sai số &plusmn;0.5°C) và checklist vật lý tuân thủ chuẩn GSP/GDP (BR14)
          </p>
        </div>

        <div style={{ display: 'flex', gap: 10 }}>
          <button className="btn btn-primary" onClick={() => setIsCreating(true)}>
            <Plus size={16} /> Tạo phiên kiểm tra mới
          </button>
          <button className="btn btn-outline" onClick={loadData}>
            <RefreshCw size={16} />
          </button>
        </div>
      </div>

      {/* Inspection History Table */}
      <div className="card">
        <div className="card-title">
          <span>Lịch Sử Các Phiên Kiểm Tra Hiện Trường ({inspections.length} phiên)</span>
        </div>

        <div className="table-container">
          <table className="table">
            <thead>
              <tr>
                <th>Mã phiên</th>
                <th>Ngày thực hiện</th>
                <th>Phân vùng kiểm tra</th>
                <th>Kỹ thuật viên thực hiện</th>
                <th>Số hạng mục</th>
                <th>Kết quả chung (BR14)</th>
                <th>Ghi chú</th>
              </tr>
            </thead>
            <tbody>
              {inspections.length === 0 ? (
                <tr>
                  <td colSpan={7} style={{ textAlign: 'center', padding: '32px 0', color: 'var(--text-muted)' }}>
                    Chưa có phiên kiểm tra nào được ghi nhận.
                  </td>
                </tr>
              ) : (
                inspections.map(insp => (
                  <tr key={insp.id}>
                    <td style={{ fontFamily: 'monospace', fontWeight: 600 }}>{insp.id.substring(0, 8)}...</td>
                    <td>{new Date(insp.inspectionDate).toLocaleDateString('vi-VN')}</td>
                    <td>{insp.zoneName}</td>
                    <td>{insp.inspectorName}</td>
                    <td>
                      {insp.itemCount} hạng mục ({insp.failCount > 0 ? `${insp.failCount} FAIL` : 'Đạt 100%'})
                    </td>
                    <td>
                      <span className={`badge ${insp.overallResult === 'PASS' ? 'badge-fresh' : insp.overallResult === 'FAIL' ? 'badge-expired' : 'badge-stale'}`}>
                        {insp.overallResult || 'PASS'}
                      </span>
                    </td>
                    <td style={{ maxWidth: 260 }}>{insp.notes || '--'}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>

      {/* Create Inspection Modal */}
      {isCreating && (
        <div className="modal-overlay">
          <div className="modal-content" style={{ maxWidth: 820 }}>
            <h3 style={{ fontSize: '1.25rem', fontWeight: 600, marginBottom: 12 }}>
              Tạo Phiên Kiểm Tra & Đối Chứng Chuẩn Mới (UC08)
            </h3>
            <p style={{ color: 'var(--text-muted)', fontSize: '0.8125rem', marginBottom: 20 }}>
              Nhập giá trị đo từ nhiệt kế chuẩn đối chứng. Nếu sai lệch &gt; 0.5°C, hệ thống sẽ tự động chuyển cảm biến sang trạng thái <strong>CALIBRATING</strong> (BR14).
            </p>

            <form onSubmit={handleCreateSubmit}>
              <div style={{ marginBottom: 16 }}>
                <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                  Chọn Phân Vùng Cần Kiểm Tra:
                </label>
                <select
                  className="select"
                  value={selectedZoneId}
                  onChange={(e) => setSelectedZoneId(e.target.value)}
                >
                  {zones.map(z => (
                    <option key={z.id} value={z.id}>{z.name} ({z.code})</option>
                  ))}
                </select>
              </div>

              {/* Checklist table */}
              <div style={{ marginBottom: 20 }}>
                <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                  Checklist Cảm Biến Trong Zone:
                </label>

                {itemsForm.length === 0 ? (
                  <p style={{ color: 'var(--text-muted)', fontSize: '0.8125rem', fontStyle: 'italic' }}>
                    Không có cảm biến nào trong phân vùng này.
                  </p>
                ) : (
                  <div className="table-container">
                    <table className="table" style={{ fontSize: '0.8125rem' }}>
                      <thead>
                        <tr>
                          <th>Cảm biến</th>
                          <th>Giá trị Sensor (°C)</th>
                          <th>Chuẩn tham chiếu (°C)</th>
                          <th>Độ lệch (&Delta;)</th>
                          <th>Kết quả</th>
                        </tr>
                      </thead>
                      <tbody>
                        {itemsForm.map((item, idx) => {
                          const dev = (item.sensorReadingValue - item.referenceValue).toFixed(2);
                          return (
                            <tr key={item.sensorId}>
                              <td style={{ fontWeight: 600, fontFamily: 'monospace' }}>
                                {item.hardwareId}
                              </td>
                              <td style={{ width: 140 }}>
                                <input
                                  type="number"
                                  step="0.01"
                                  className="input"
                                  style={{ padding: '4px 8px' }}
                                  value={item.sensorReadingValue}
                                  onChange={(e) => handleItemChange(idx, 'sensorReadingValue', e.target.value)}
                                  required
                                />
                              </td>
                              <td style={{ width: 140 }}>
                                <input
                                  type="number"
                                  step="0.01"
                                  className="input"
                                  style={{ padding: '4px 8px' }}
                                  value={item.referenceValue}
                                  onChange={(e) => handleItemChange(idx, 'referenceValue', e.target.value)}
                                  required
                                />
                              </td>
                              <td>
                                <span style={{ fontWeight: 600, color: Math.abs(dev) > 0.5 ? '#f87171' : '#34d399' }}>
                                  {dev > 0 ? `+${dev}` : dev}°C
                                </span>
                              </td>
                              <td style={{ width: 120 }}>
                                <select
                                  className="select"
                                  style={{ padding: '4px 8px' }}
                                  value={item.result}
                                  onChange={(e) => handleItemChange(idx, 'result', e.target.value)}
                                >
                                  <option value="PASS">PASS</option>
                                  <option value="FAIL">FAIL</option>
                                  <option value="PARTIAL">PARTIAL</option>
                                </select>
                              </td>
                            </tr>
                          );
                        })}
                      </tbody>
                    </table>
                  </div>
                )}
              </div>

              <div style={{ marginBottom: 24 }}>
                <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
                  Ghi chú chung phiên kiểm tra
                </label>
                <textarea
                  className="input"
                  rows={2}
                  placeholder="Ghi nhận tình trạng vỏ hộp, pin, tín hiệu sóng và hiện trường..."
                  value={inspectionNotes}
                  onChange={(e) => setInspectionNotes(e.target.value)}
                />
              </div>

              <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 10 }}>
                <button
                  type="button"
                  className="btn btn-secondary"
                  onClick={() => setIsCreating(false)}
                  disabled={submitting}
                >
                  Hủy bỏ
                </button>
                <button type="submit" className="btn btn-primary" disabled={submitting || itemsForm.length === 0}>
                  {submitting ? 'Đang lưu...' : 'Lưu Biên Bản Kiểm Tra'}
                </button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
