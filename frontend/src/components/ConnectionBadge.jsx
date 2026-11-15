import React from 'react';

export default function ConnectionBadge({ state }) {
  const map = {
    CONNECTED: { label: 'CONNECTED', className: 'badge-connected' },
    STALE: { label: 'STALE', className: 'badge-stale' },
    DISCONNECTED: { label: 'DISCONNECTED', className: 'badge-disconnected' },
    NEVER_CONNECTED: { label: 'NEVER_CONNECTED', className: 'badge-expired' }
  };

  const item = map[state] || { label: state || 'UNKNOWN', className: 'badge-stale' };

  return (
    <span className={`badge ${item.className}`}>
      {item.label}
    </span>
  );
}
