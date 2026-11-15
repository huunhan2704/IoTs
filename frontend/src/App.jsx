import React, { useState, useEffect } from 'react';
import { BrowserRouter, Routes, Route, Navigate } from 'react-router-dom';
import { auth, startSignalR, apiFetch } from './api';

import Navbar from './components/Navbar';
import SimulatorModal from './components/SimulatorModal';

import WarehouseDashboard from './pages/WarehouseDashboard';
import ZoneDetail from './pages/ZoneDetail';
import SensorDetail from './pages/SensorDetail';
import AlertCenter from './pages/AlertCenter';
import InspectionManager from './pages/InspectionManager';
import SensorManagement from './pages/SensorManagement';
import ReportGenerator from './pages/ReportGenerator';
import Login from './pages/Login';

import { Bell, Sliders, Clock, CheckCircle2 } from 'lucide-react';

function ProtectedLayout({ children, activeAlertCount, onOpenSimulator }) {
  const [currentTime, setCurrentTime] = useState(new Date().toLocaleTimeString('vi-VN'));

  useEffect(() => {
    const timer = setInterval(() => setCurrentTime(new Date().toLocaleTimeString('vi-VN')), 1000);
    return () => clearInterval(timer);
  }, []);

  if (!auth.isLoggedIn()) {
    return <Navigate to="/login" replace />;
  }

  return (
    <div className="app-container">
      <Navbar activeAlertCount={activeAlertCount} onOpenSimulator={onOpenSimulator} />
      <div className="main-content">
        <header className="top-header">
          <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
            <span style={{ fontSize: '0.875rem', color: 'var(--text-muted)' }}>Hệ thống giám sát vi khí hậu kho</span>
            <span style={{ color: 'var(--border-color)' }}>|</span>
            <span className="badge badge-fresh" style={{ fontSize: '0.6875rem' }}>
              <CheckCircle2 size={12} /> WebSocket Connected
            </span>
          </div>

          <div style={{ display: 'flex', alignItems: 'center', gap: 16 }}>
            <div style={{ display: 'flex', alignItems: 'center', gap: 6, color: 'var(--text-muted)', fontSize: '0.8125rem' }}>
              <Clock size={14} />
              <span className="font-mono">{currentTime}</span>
            </div>

            <div style={{ display: 'flex', alignItems: 'center', gap: 6, color: '#38bdf8', fontSize: '0.8125rem', backgroundColor: 'rgba(56, 189, 248, 0.1)', padding: '4px 10px', borderRadius: 6, border: '1px solid rgba(56, 189, 248, 0.3)' }}>
              <span style={{ width: 8, height: 8, borderRadius: '50%', backgroundColor: '#22c55e', display: 'inline-block' }}></span>
              <span>Wokwi Hardware Mode (5 ESP32)</span>
            </div>
          </div>
        </header>

        <main className="page-body">
          {children}
        </main>
      </div>
    </div>
  );
}

export default function App() {
  const [activeAlertCount, setActiveAlertCount] = useState(0);
  const [isSimulatorOpen, setIsSimulatorOpen] = useState(false);
  const [user, setUser] = useState(auth.getUser());

  // Load initial active alerts count
  const loadAlertsCount = async () => {
    if (!auth.isLoggedIn()) return;
    try {
      const list = await apiFetch('/api/alerts?status=ACTIVE');
      setActiveAlertCount(list.length);
    } catch {
      // ignore
    }
  };

  useEffect(() => {
    if (auth.isLoggedIn()) {
      loadAlertsCount();
      // Start SignalR realtime connection
      startSignalR({
        alert: () => loadAlertsCount()
      });
    }

    const handleAuthChange = () => {
      setUser(auth.getUser());
    };
    window.addEventListener('auth-changed', handleAuthChange);
    return () => window.removeEventListener('auth-changed', handleAuthChange);
  }, []);

  return (
    <BrowserRouter>
      <Routes>
        <Route path="/login" element={<Login onLoginSuccess={(u) => { setUser(u); loadAlertsCount(); }} />} />

        <Route
          path="/"
          element={
            <ProtectedLayout activeAlertCount={activeAlertCount} onOpenSimulator={() => setIsSimulatorOpen(true)}>
              <WarehouseDashboard />
            </ProtectedLayout>
          }
        />

        <Route
          path="/zones/:id"
          element={
            <ProtectedLayout activeAlertCount={activeAlertCount} onOpenSimulator={() => setIsSimulatorOpen(true)}>
              <ZoneDetail />
            </ProtectedLayout>
          }
        />

        <Route
          path="/sensors"
          element={
            <ProtectedLayout activeAlertCount={activeAlertCount} onOpenSimulator={() => setIsSimulatorOpen(true)}>
              <SensorManagement />
            </ProtectedLayout>
          }
        />

        <Route
          path="/sensors/:id"
          element={
            <ProtectedLayout activeAlertCount={activeAlertCount} onOpenSimulator={() => setIsSimulatorOpen(true)}>
              <SensorDetail />
            </ProtectedLayout>
          }
        />

        <Route
          path="/alerts"
          element={
            <ProtectedLayout activeAlertCount={activeAlertCount} onOpenSimulator={() => setIsSimulatorOpen(true)}>
              <AlertCenter />
            </ProtectedLayout>
          }
        />

        <Route
          path="/inspections"
          element={
            <ProtectedLayout activeAlertCount={activeAlertCount} onOpenSimulator={() => setIsSimulatorOpen(true)}>
              <InspectionManager />
            </ProtectedLayout>
          }
        />

        <Route
          path="/reports"
          element={
            <ProtectedLayout activeAlertCount={activeAlertCount} onOpenSimulator={() => setIsSimulatorOpen(true)}>
              <ReportGenerator />
            </ProtectedLayout>
          }
        />

        <Route path="*" element={<Navigate to="/" replace />} />
      </Routes>

      <SimulatorModal isOpen={isSimulatorOpen} onClose={() => setIsSimulatorOpen(false)} />
    </BrowserRouter>
  );
}
