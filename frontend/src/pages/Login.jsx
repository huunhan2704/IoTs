import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { apiFetch, auth } from '../api';
import { LogIn, Key, User, Shield } from 'lucide-react';

export default function Login({ onLoginSuccess }) {
  const navigate = useNavigate();
  const [username, setUsername] = useState('operator1');
  const [password, setPassword] = useState('123456');
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    if (e) e.preventDefault();
    try {
      setLoading(true);
      setError('');
      const res = await apiFetch('/api/auth/login', {
        method: 'POST',
        body: JSON.stringify({ username, password })
      });

      auth.setSession(res.token, res.user);
      if (onLoginSuccess) onLoginSuccess(res.user);
      navigate('/');
    } catch (err) {
      setError(err.message || 'Đăng nhập không thành công');
    } finally {
      setLoading(false);
    }
  };

  const quickLogin = (u) => {
    setUsername(u);
    setPassword('123456');
  };

  return (
    <div style={{
      minHeight: '100vh',
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      padding: 16,
      background: 'linear-gradient(135deg, #090e1a 0%, #0f172a 100%)'
    }}>
      <div className="card" style={{ maxWidth: 440, width: '100%', padding: 32 }}>
        <div style={{ textAlign: 'center', marginBottom: 28 }}>
          <div style={{
            width: 48,
            height: 48,
            borderRadius: 12,
            backgroundColor: '#2563eb',
            display: 'inline-flex',
            alignItems: 'center',
            justifyContent: 'center',
            marginBottom: 12
          }}>
            <Shield size={24} color="#fff" />
          </div>
          <h1 style={{ fontSize: '1.375rem', fontWeight: 700, color: '#f8fafc' }}>
            IOT-04 WAREHOUSE MONITOR
          </h1>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.8125rem', marginTop: 4 }}>
            Hệ Thống Theo Dõi Điều Kiện Kho Bằng Cảm Biến (Honest Data)
          </p>
        </div>

        {error && (
          <div style={{
            padding: '10px 14px',
            backgroundColor: 'rgba(239, 68, 68, 0.1)',
            border: '1px solid rgba(239, 68, 68, 0.3)',
            borderRadius: 6,
            color: '#f87171',
            fontSize: '0.8125rem',
            marginBottom: 16
          }}>
            {error}
          </div>
        )}

        <form onSubmit={handleSubmit}>
          <div style={{ marginBottom: 16 }}>
            <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
              Tên đăng nhập:
            </label>
            <div style={{ position: 'relative' }}>
              <User size={16} style={{ position: 'absolute', left: 10, top: 10, color: 'var(--text-muted)' }} />
              <input
                type="text"
                className="input"
                style={{ paddingLeft: 34 }}
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                required
              />
            </div>
          </div>

          <div style={{ marginBottom: 24 }}>
            <label style={{ display: 'block', fontSize: '0.8125rem', marginBottom: 6, fontWeight: 500 }}>
              Mật khẩu:
            </label>
            <div style={{ position: 'relative' }}>
              <Key size={16} style={{ position: 'absolute', left: 10, top: 10, color: 'var(--text-muted)' }} />
              <input
                type="password"
                className="input"
                style={{ paddingLeft: 34 }}
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                required
              />
            </div>
          </div>

          <button type="submit" className="btn btn-primary" style={{ width: '100%', justifyContent: 'center', padding: '10px 0' }} disabled={loading}>
            <LogIn size={16} /> {loading ? 'Đang xác thực...' : 'Đăng nhập hệ thống'}
          </button>
        </form>

        {/* Quick Login Role Pills for Teacher / Evaluator Testing */}
        <div style={{ marginTop: 24, paddingTop: 16, borderTop: '1px solid var(--border-color)' }}>
          <div style={{ fontSize: '0.75rem', color: 'var(--text-muted)', marginBottom: 8, textAlign: 'center' }}>
            Tài khoản mẫu có sẵn (Mật khẩu: <code>123456</code>):
          </div>
          <div style={{ display: 'flex', gap: 6, flexWrap: 'wrap', justifyContent: 'center' }}>
            <button className="btn btn-outline btn-sm" onClick={() => quickLogin('manager1')}>
              Quản lý kho
            </button>
            <button className="btn btn-outline btn-sm" onClick={() => quickLogin('operator1')}>
              Giám sát ca
            </button>
            <button className="btn btn-outline btn-sm" onClick={() => quickLogin('tech1')}>
              Kỹ thuật viên
            </button>
            <button className="btn btn-outline btn-sm" onClick={() => quickLogin('admin')}>
              Admin
            </button>
          </div>
        </div>
      </div>
    </div>
  );
}
