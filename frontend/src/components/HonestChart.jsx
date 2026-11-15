import React from 'react';
import {
  ResponsiveContainer,
  LineChart,
  Line,
  XAxis,
  YAxis,
  Tooltip,
  ReferenceLine,
  ReferenceArea,
  CartesianGrid
} from 'recharts';

// Custom Dot to display Honest Data quality (BR10)
const QualityDot = (props) => {
  const { cx, cy, payload } = props;
  if (!cx || !cy || payload.value === null || payload.value === undefined) return null;

  let fill = '#38bdf8'; // Default GOOD
  let stroke = '#0284c7';

  if (payload.quality === 'SUSPECT_DRIFT') {
    fill = '#fbbf24'; // Warning yellow
    stroke = '#d97706';
  } else if (payload.quality === 'ANOMALOUS_REJECT') {
    fill = '#f87171'; // Error red
    stroke = '#dc2626';
  }

  return (
    <circle
      cx={cx}
      cy={cy}
      r={3.5}
      fill={fill}
      stroke={stroke}
      strokeWidth={1.5}
    />
  );
};

export default function HonestChart({
  data = [],
  gaps = [],
  dataKey = 'value',
  name = 'Giá trị',
  unit = '°C',
  minThreshold,
  maxThreshold,
  color = '#38bdf8'
}) {
  const formatTime = (ts) => {
    if (!ts) return '';
    const d = new Date(ts);
    return `${d.getHours().toString().padStart(2, '0')}:${d.getMinutes().toString().padStart(2, '0')}`;
  };

  const CustomTooltip = ({ active, payload, label }) => {
    if (!active || !payload || !payload.length) return null;
    const pt = payload[0].payload;
    return (
      <div style={{
        backgroundColor: '#0f172a',
        border: '1px solid #334155',
        borderRadius: '6px',
        padding: '10px 14px',
        fontSize: '0.8125rem'
      }}>
        <div style={{ color: '#94a3b8', marginBottom: '4px' }}>
          Thời gian: <strong>{new Date(pt.time).toLocaleString('vi-VN')}</strong>
        </div>
        <div style={{ color: '#f8fafc', fontWeight: 600, fontSize: '0.9375rem' }}>
          {name}: {pt[dataKey] !== null && pt[dataKey] !== undefined ? `${pt[dataKey]} ${unit}` : 'Mất tín hiệu (DataGap)'}
        </div>
        {pt.hardwareId && (
          <div style={{ color: '#64748b', fontSize: '0.75rem', marginTop: '2px' }}>
            Sensor: {pt.hardwareId}
          </div>
        )}
        {pt.quality && (
          <div style={{ marginTop: '6px' }}>
            <span className={`badge ${pt.quality === 'GOOD' ? 'badge-fresh' : pt.quality === 'SUSPECT_DRIFT' ? 'badge-stale' : 'badge-expired'}`}>
              Quality: {pt.quality}
            </span>
          </div>
        )}
      </div>
    );
  };

  return (
    <div style={{ width: '100%', height: 320, position: 'relative' }}>
      <ResponsiveContainer width="100%" height="100%">
        <LineChart data={data} margin={{ top: 20, right: 30, left: 10, bottom: 20 }}>
          <CartesianGrid strokeDasharray="3 3" stroke="#1e293b" />
          <XAxis
            dataKey="time"
            tickFormatter={formatTime}
            stroke="#64748b"
            fontSize={12}
            dy={8}
          />
          <YAxis
            stroke="#64748b"
            fontSize={12}
            unit={` ${unit}`}
            domain={['auto', 'auto']}
          />
          <Tooltip content={<CustomTooltip />} />

          {/* Reference Lines for Safe Range (BR02) */}
          {minThreshold !== undefined && (
            <ReferenceLine
              y={minThreshold}
              stroke="#3b82f6"
              strokeDasharray="4 4"
              strokeWidth={1.5}
              label={{ value: `Ngưỡng Min: ${minThreshold}${unit}`, fill: '#3b82f6', fontSize: 11, position: 'insideBottomLeft' }}
            />
          )}
          {maxThreshold !== undefined && (
            <ReferenceLine
              y={maxThreshold}
              stroke="#ef4444"
              strokeDasharray="4 4"
              strokeWidth={1.5}
              label={{ value: `Ngưỡng Max: ${maxThreshold}${unit}`, fill: '#ef4444', fontSize: 11, position: 'insideTopLeft' }}
            />
          )}

          {/* BR08: GapOverlay — Vùng bóng gián đoạn dữ liệu DataGap */}
          {gaps.map((g, idx) => (
            <ReferenceArea
              key={`gap-${idx}`}
              x1={g.startTime}
              x2={g.endTime}
              fill="#ef4444"
              fillOpacity={0.12}
              stroke="#ef4444"
              strokeDasharray="3 3"
              strokeWidth={1}
              label={{
                value: `VÙNG GIÁN ĐOẠN (${g.reason || 'DATAGAP'})`,
                fill: '#f87171',
                fontSize: 10,
                position: 'insideTop'
              }}
            />
          ))}

          {/* BR08: Honest Data Principle: connectNulls={false} strictly set! */}
          <Line
            type="monotone"
            dataKey={dataKey}
            stroke={color}
            strokeWidth={2}
            connectNulls={false}
            dot={<QualityDot />}
            activeDot={{ r: 6, stroke: '#fff', strokeWidth: 2 }}
            isAnimationActive={false}
          />
        </LineChart>
      </ResponsiveContainer>
    </div>
  );
}
