import React from 'react';
import { NavLink, Outlet, useLocation } from 'react-router-dom';
import { Home, Utensils, WalletCards, Trophy, ReceiptText, UserRound, Bell, Monitor, LogOut, Zap, LifeBuoy } from 'lucide-react';
import './client.css';

const menu = [
  ['/client/home', 'Trang chủ', Home],
  ['/client/ordering', 'Gọi món', Utensils],
  ['/client/wallet', 'Ví', WalletCards],
  ['/client/tournaments', 'Giải đấu', Trophy],
  ['/client/invoices', 'Hóa đơn', ReceiptText],
  ['/client/account', 'Tài khoản', UserRound],
];

export function ClientLayout() {
  const { pathname } = useLocation();
  const pageTitle = menu.find(([path]) => path === pathname)?.[1] || 'Khu vực người chơi';
  return (
    <div className="client-shell">
      <aside className="client-sidebar">
        <div className="client-brand">
          <div className="client-brand-mark"><Zap size={18} strokeWidth={2.5} /></div>
          <div>
            <strong>TRAM</strong>
            <span>KHÔNG GIAN CHƠI</span>
          </div>
        </div>

        <div className="client-nav-title">DÀNH CHO NGƯỜI CHƠI</div>
        <nav className="client-nav">
          {menu.map(([to, label, Icon]) => (
            <NavLink
              key={to}
              to={to}
              className={({ isActive }) => `client-nav-item ${isActive ? 'active' : ''}`}
            >
              <Icon size={16} />
              <span>{label}</span>
            </NavLink>
          ))}
        </nav>

        <div className="client-support">
          <div className="support-icon"><LifeBuoy size={17} /></div>
          <strong>Cần hỗ trợ?</strong>
          <p>Nhân viên luôn sẵn sàng hỗ trợ bạn tại máy.</p>
          <button>Gọi nhân viên →</button>
        </div>

        <div className="client-user">
          <div className="client-avatar">MA</div>
          <div>
            <strong>Nguyễn Minh Anh</strong>
            <span>@minhanh</span>
          </div>
          <LogOut size={15} />
        </div>
      </aside>

      <main className="client-main">
        <header className="client-topbar">
          <div className="client-breadcrumb">
            <span>Khu vực người chơi</span>
            <b>/</b>
            <strong>{pageTitle}</strong>
          </div>
          <div className="client-top-actions">
            <span className="client-time">Thứ Hai, 05/10/2026 · 16:15</span>
            <span className="client-machine"><Monitor size={14} /> MÁY 12</span>
            <button className="client-bell"><Bell size={16} /></button>
          </div>
        </header>
        <Outlet />
      </main>
    </div>
  );
}
