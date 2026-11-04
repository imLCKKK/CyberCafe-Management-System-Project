import React from 'react';
import { Check, Mail, Phone, ShieldCheck, UserRound, WalletCards } from 'lucide-react';
import { PageHeading } from '../shared/ClientUI';
import { dateTime, money } from '../shared/demoModel';
import { useClientProfile } from './useClientProfile';

// Hồ sơ khách đọc từ API/SQL theo tài khoản đang đăng nhập (S1-M-1).
// Đổi tên, đổi mật khẩu và lịch sử phiên chơi chưa có API nên tạm không hiển thị, không dùng dữ liệu mẫu thay thế.
export function AccountPage() {
  const { status, profile, error, reload } = useClientProfile();

  const heading = (chip) => <PageHeading eyebrow="YOUR SPACE, YOUR IDENTITY" title={<>Tài khoản của <span>bạn.</span></>} description="Thông tin người chơi tại TRAM.">{chip}</PageHeading>;
  if (status === 'loading') {
    return <div className="cafe-home client-module account-page">{heading()}<p className="home-muted" role="status">Đang tải hồ sơ...</p></div>;
  }
  if (status === 'error') {
    return <div className="cafe-home client-module account-page">{heading()}
      <div className="home-alert" role="alert">{error}</div>
      <button className="home-button primary" onClick={reload}>Thử lại</button>
    </div>;
  }

  const active = profile.status === 'Active';
  const initials = profile.fullName.split(' ').filter(Boolean).slice(-2).map((part) => part[0]).join('');
  return <div className="cafe-home client-module account-page">
    {heading(<span className="client-heading-chip"><ShieldCheck size={16}/>{active ? 'Tài khoản đang hoạt động' : 'Tài khoản đã khóa'}</span>)}
    <div className="account-layout">
      <aside>
        <section className="home-panel account-profile">
          <div className="account-avatar">{initials}<span><Check size={12}/></span></div>
          <h2>{profile.fullName}</h2>
          <p>Người chơi TRAM</p>
          <div className="account-id">ID #{String(profile.customerId).padStart(6, '0')}</div>
          <div className="account-summary-line"><Phone size={15}/><span>{profile.phone}</span></div>
          {profile.email && <div className="account-summary-line"><Mail size={15}/><span>{profile.email}</span></div>}
          <div className="account-summary-line"><ShieldCheck size={15}/><span>{active ? 'Đang hoạt động' : 'Đã khóa'}</span><span className="client-live-dot"/></div>
          <div className="account-member-since">Đồng hành cùng TRAM từ<br/><strong>{dateTime(profile.createdAt).split(' ')[1] || dateTime(profile.createdAt)}</strong></div>
        </section>
      </aside>
      <div className="account-main">
        <div className="client-stat-grid three">
          <div className="client-stat"><span className="client-icon-tile"><WalletCards size={19}/></span><div><span>Số dư ví</span><strong>{money(profile.walletBalance)}</strong></div></div>
        </div>
        <section className="home-panel">
          <div className="client-panel-heading"><h2><UserRound size={18}/> Thông tin cá nhân</h2><span className="client-muted">Hồ sơ người chơi</span></div>
          <div className="client-panel-body">
            <div className="home-detail-row"><span>Họ tên</span><strong>{profile.fullName}</strong></div>
            <div className="home-detail-row"><span>Số điện thoại / Tên đăng nhập</span><strong>{profile.phone}</strong></div>
            <div className="home-detail-row"><span>Email</span><strong>{profile.email || '—'}</strong></div>
            <div className="home-detail-row"><span>Phiên chơi</span><strong>{profile.hasActiveSession ? 'Đang có phiên chơi' : 'Chưa có phiên'}</strong></div>
            <p className="home-fine-print">Liên hệ nhân viên nếu cần điều chỉnh thông tin cá nhân.</p>
          </div>
        </section>
      </div>
    </div>
  </div>;
}
