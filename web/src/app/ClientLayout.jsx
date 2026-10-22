import React, { useEffect, useState } from 'react';
import { NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Home, Utensils, WalletCards, Trophy, ReceiptText, UserRound, Bell, Monitor, Zap, LifeBuoy } from 'lucide-react';
import { ClientDemoProvider, useClientDemo } from '../client/shared/ClientDemoContext';
import { dateTime, money } from '../client/shared/demoModel';
import HomeDialog from '../client/home/HomeDialog';
import './client.css';
import '../client/home/home.css';
import '../client/shared/client-pages.css';

const menu = [
  ['/client/home', 'Trang chủ', Home],
  ['/client/ordering', 'Gọi món', Utensils],
  ['/client/wallet', 'Ví', WalletCards],
  ['/client/tournaments', 'Giải đấu', Trophy],
  ['/client/invoices', 'Hóa đơn', ReceiptText],
  ['/client/account', 'Tài khoản', UserRound],
];

export function ClientLayout() {
  return <ClientDemoProvider><ClientShell/></ClientDemoProvider>;
}

function ClientShell() {
  const { account, computer, db, customer } = useClientDemo();
  const { pathname } = useLocation();
  const navigate = useNavigate();
  const [now, setNow] = useState(() => new Date());
  const [dialog, setDialog] = useState(null);
  useEffect(() => { const timer = setInterval(() => setNow(new Date()), 60000); return () => clearInterval(timer); }, []);
  const pageTitle = menu.find(([path]) => path === pathname)?.[1] || 'Khu vực người chơi';
  const initials = account.display_name.split(' ').filter(Boolean).slice(-2).map((part) => part[0]).join('');
  const waiting = db.top_ups.filter((r) => r.customer_id === customer.id && ['Pending', 'CreditedUnpaid'].includes(r.status));
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
              title={label}
              aria-label={label}
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
          <button onClick={() => setDialog('support')}>Liên hệ tại quầy →</button>
        </div>

        <div className="client-user">
          <div className="client-avatar">{initials}</div>
          <div>
            <strong>{account.display_name}</strong>
            <span>{account.phone}</span>
          </div>
          <button className="client-profile-button" aria-label="Mở tài khoản" onClick={() => navigate('/client/account')}><UserRound size={16}/></button>
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
            <time className="client-time" dateTime={now.toISOString()}>{dateTime(now)}</time>
            <span className="client-machine"><Monitor size={16} /> {computer?.code || 'CHƯA CÓ PHIÊN'}</span>
            <button className="client-bell" aria-label="Thông báo" onClick={() => setDialog('notifications')}><Bell size={16}/>{waiting.length > 0 && <i/>}</button>
          </div>
        </header>
        <Outlet />
      </main>
      {dialog && <HomeDialog title={dialog === 'support' ? 'TRAM luôn sẵn sàng hỗ trợ' : 'Thông báo của bạn'} onClose={() => setDialog(null)}>{dialog === 'support' ? <div className="home-info-box"><LifeBuoy size={24}/><span>Bạn có thể liên hệ nhân viên tại quầy để được hỗ trợ về máy, nạp tiền hoặc đơn món. Cho nhân viên biết vị trí {computer?.code || 'máy của bạn'} để được phục vụ nhanh hơn.</span></div> : <>{waiting.length ? waiting.map((r) => <div className="home-detail-row" key={r.id}><span>Yêu cầu nạp #NT{r.id}<small>{r.status === 'Pending' ? 'Đang chờ nhân viên xác nhận' : 'Đã cộng ví · chờ thu tiền tại quầy'}</small></span><strong>{money(r.amount)}</strong></div>) : <p className="home-dialog-intro">Bạn đã xem hết thông báo. Chúc bạn có một buổi chơi vui vẻ!</p>}</>}</HomeDialog>}
    </div>
  );
}
