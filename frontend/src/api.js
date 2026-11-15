import * as signalR from '@microsoft/signalr';

const TOKEN_KEY = 'iot04_token';
const USER_KEY = 'iot04_user';

export const auth = {
  getToken: () => localStorage.getItem(TOKEN_KEY),
  getUser: () => {
    const raw = localStorage.getItem(USER_KEY);
    try { return raw ? JSON.parse(raw) : null; } catch { return null; }
  },
  setSession: (token, user) => {
    localStorage.setItem(TOKEN_KEY, token);
    localStorage.setItem(USER_KEY, JSON.stringify(user));
  },
  clearSession: () => {
    localStorage.removeItem(TOKEN_KEY);
    localStorage.removeItem(USER_KEY);
  },
  isLoggedIn: () => !!localStorage.getItem(TOKEN_KEY)
};

export async function apiFetch(endpoint, options = {}) {
  const token = auth.getToken();
  const headers = {
    'Content-Type': 'application/json',
    ...(token ? { Authorization: `Bearer ${token}` } : {}),
    ...options.headers
  };

  const response = await fetch(endpoint, { ...options, headers });
  if (response.status === 401) {
    auth.clearSession();
    window.dispatchEvent(new Event('auth-changed'));
    throw new Error('Hết phiên làm việc. Vui lòng đăng nhập lại.');
  }

  const contentType = response.headers.get('content-type');
  if (contentType && contentType.includes('application/json')) {
    const data = await response.json();
    if (!response.ok) throw new Error(data.error || 'Yêu cầu thất bại');
    return data;
  }

  if (!response.ok) {
    const text = await response.text();
    throw new Error(text || 'Yêu cầu thất bại');
  }

  return response;
}

// SignalR Realtime Client
let connection = null;

export function getSignalRConnection() {
  if (!connection) {
    connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/monitor', {
        accessTokenFactory: () => auth.getToken() || ''
      })
      .withAutomaticReconnect([0, 2000, 5000, 10000])
      .configureLogging(signalR.LogLevel.Warning)
      .build();
  }
  return connection;
}

export async function startSignalR(onMessage = {}) {
  const conn = getSignalRConnection();
  if (conn.state === signalR.HubConnectionState.Disconnected) {
    try {
      await conn.start();
      console.log('SignalR connected to warehouse telemetry hub');
    } catch (err) {
      console.error('SignalR connection error:', err);
    }
  }

  if (onMessage.reading) conn.on('reading', onMessage.reading);
  if (onMessage.sensorState) conn.on('sensorState', onMessage.sensorState);
  if (onMessage.alert) conn.on('alert', onMessage.alert);
  if (onMessage.drift) conn.on('drift', onMessage.drift);

  return conn;
}
