import React, { useState, useEffect } from 'react';

export default function DataAgeBadge({ lastReadingAt, initialStatus, initialSeconds }) {
  const [seconds, setSeconds] = useState(initialSeconds ?? 0);

  useEffect(() => {
    if (!lastReadingAt) return;
    const calc = () => {
      const now = Date.now();
      const readingTime = new Date(lastReadingAt).getTime();
      return Math.max(0, Math.floor((now - readingTime) / 1000));
    };

    setSeconds(calc());
    const timer = setInterval(() => setSeconds(calc()), 1000);
    return () => clearInterval(timer);
  }, [lastReadingAt]);

  if (!lastReadingAt) {
    return <span className="badge badge-expired">CHƯA CÓ DATA</span>;
  }

  let status = 'FRESH';
  let badgeClass = 'badge-fresh';

  if (seconds < 180) { // < 3m
    status = 'FRESH';
    badgeClass = 'badge-fresh';
  } else if (seconds < 600) { // 3-10m
    status = 'AGING';
    badgeClass = 'badge-aging';
  } else if (seconds < 1800) { // 10-30m
    status = 'STALE';
    badgeClass = 'badge-stale';
  } else {
    status = 'EXPIRED';
    badgeClass = 'badge-expired';
  }

  const formatDuration = (sec) => {
    if (sec < 60) return `${sec}s`;
    const m = Math.floor(sec / 60);
    const s = sec % 60;
    if (m < 60) return `${m}m ${s}s`;
    const h = Math.floor(m / 60);
    return `${h}h ${m % 60}m`;
  };

  return (
    <span className={`badge ${badgeClass}`} title={`Tuổi dữ liệu: ${seconds} giây`}>
      {formatDuration(seconds)} ({status})
    </span>
  );
}
