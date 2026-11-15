import React from 'react';
import { NavLink, useNavigate } from 'react-router-dom';
import {
  LayoutDashboard,
  Cpu,
  Bell,
  ClipboardCheck,
  FileText,
  Sliders,
  LogOut,
  User,
  ShieldAlert
} from 'lucide-react';
import { auth } from '../api';

export default function Navbar({ activeAlertCount = 0, onOpenSimulator }) {
  const navigate = useNavigate();
  const user = auth.getUser();

  const handleLogout = () => {
    auth.clearSession();
    navigate('/login');
  };

  return (
    <aside className="sidebar">
      <div style={{ padding: '20px 20px 16px', borderBottom: '1px solid var(--border-color)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
          <div style={{
            width: 34, height: 34, borderRadius: 8,
            backgroundColor: '#2563eb', display: 'flex', alignItems: 'center', justifyContent: 'center',
            color: '#fff', fontWeight: 700, fontSize: '0.9375rem'
          }}>
            IOT
          </div>
          <div>
            <div style={{ fontWeight: 700, fontSize: '1rem', letterSpacing: '0.025em', color: '#f8fafc' }}>
              IOT-04 MONITOR
            </div>
            <div style={{ fontSize: '0.6875rem', color: '#94a3b8' }}>
              Honest Data & Microservices
            </div>
          </div>
        </div>
      </div>

      <nav style={{ padding: '16px 12px', flex: 1, display: 'flex', flexDirection: 'column', gap: 4 }}>
        <NavLink
          to="/"
          end
          className={({ isActive }) => `btn btn-outline ${isActive ? 'btn-primary' : ''}`}
          style={{ justifyContent: 'flex-start', border: 'none', padding: '10px 12px' }}
        >
          <LayoutDashboard size={18} />
          <span>Tổng quan kho</span>
        </NavLink>

        <NavLink
          to="/sensors"
          className={({ isActive }) => `btn btn-outline ${isActive ? 'btn-primary' : ''}`}
          style={{ justifyContent: 'flex-start', border: 'none', padding: '10px 12px' }}
        >
          <Cpu size={18} />
          <span>Quản lý cảm biến</span>
        </NavLink>

        <NavLink
          to="/alerts"
          className={({ isActive }) => `btn btn-outline ${isActive ? 'btn-primary' : ''}`}
          style={{ justifyContent: 'flex-start', border: 'none', padding: '10px 12px', position: 'relative' }}
        >
          <Bell size={18} />
          <span>Trung tâm cảnh báo</span>
          {activeAlertCount > 0 && (
            <span style={{
              marginLeft: 'auto',
              backgroundColor: '#ef4444',
              color: '#fff',
              fontSize: '0.75rem',
              fontWeight: 700,
              padding: '2px 8px',
              borderRadius: 9999
            }}>
              {activeAlertCount}
            </span>
          )}
        </NavLink>

        <NavLink
          to="/inspections"
          className={({ isActive }) => `btn btn-outline ${isActive ? 'btn-primary' : ''}`}
          style={{ justifyContent: 'flex-start', border: 'none', padding: '10px 12px' }}
        >
          <ClipboardCheck size={18} />
          <span>Kiểm tra & Hiệu chuẩn</span>
        </NavLink>

        <NavLink
          to="/reports"
          className={({ isActive }) => `btn btn-outline ${isActive ? 'btn-primary' : ''}`}
          style={{ justifyContent: 'flex-start', border: 'none', padding: '10px 12px' }}
        >
          <FileText size={18} />
          <span>Báo cáo điều kiện kho</span>
        </NavLink>
      </nav>

      {/* User Info & Logout */}
      <div style={{ padding: '16px 20px', borderTop: '1px solid var(--border-color)', backgroundColor: 'rgba(0,0,0,0.2)' }}>
        <div style={{ display: 'flex', alignItems: 'center', gap: 10, marginBottom: 12 }}>
          <div style={{
            width: 32, height: 32, borderRadius: '50%', backgroundColor: '#334155',
            display: 'flex', alignItems: 'center', justifyContent: 'center'
          }}>
            <User size={16} />
          </div>
          <div style={{ overflow: 'hidden' }}>
            <div style={{ fontWeight: 600, fontSize: '0.8125rem', whiteSpace: 'nowrap', textOverflow: 'ellipsis', overflow: 'hidden' }}>
              {user?.fullName || 'Người dùng'}
            </div>
            <div style={{ fontSize: '0.6875rem', color: '#94a3b8' }}>
              {user?.role || 'MONITORING_OPERATOR'}
            </div>
          </div>
        </div>
        <button
          onClick={handleLogout}
          className="btn btn-outline btn-sm"
          style={{ width: '100%', justifyContent: 'center', borderColor: '#334155', color: '#94a3b8' }}
        >
          <LogOut size={14} /> Đăng xuất
        </button>
      </div>
    </aside>
  );
}
