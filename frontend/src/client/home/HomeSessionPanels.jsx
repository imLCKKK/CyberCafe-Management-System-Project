import React from 'react';
import { Clock3, Cpu, Gamepad2, Monitor, Plus, WalletCards } from 'lucide-react';
import { Badge, Empty } from '../shared/ClientUI';
import { dateTime, minutesLabel, money } from '../shared/demoModel';

export function HomeSessionPanels({ home, now, navigate }) {
  if (home.loading) return <section className="home-panel" role="status">Đang tải máy, phiên và ví…</section>;
  if (home.error) return <section className="home-panel home-alert" role="alert">
    {home.error}<button className="home-button" onClick={home.reload}>Thử lại phiên</button>
  </section>;
  const session = home.session;
  const remaining = session ? home.walletBalance / session.pricePerHourSnapshot : 0;
  return <>
    <section className="home-panel home-session" aria-labelledby="session-title">
      <div className="home-session-content">
        <div className="home-card-top"><h2 id="session-title"><Monitor size={19}/>{session?.computerName || 'Phiên chơi của bạn'}</h2>
          {session && <Badge status={session.status}/>}</div>
        {session ? <>
          <div className="home-eyebrow home-timer-label">THỜI GIAN ĐÃ CHƠI</div>
          <div className="home-timer home-timer-minutes" aria-label="Thời gian đã chơi">{minutesLabel(session.startTimeUtc, now)}</div>
          <p className="home-start"><Clock3 size={13}/> Bắt đầu {dateTime(session.startTimeUtc)}</p>
          <div className="home-machine-meta"><span>Khu máy {session.typeName}</span>
            <span><Cpu size={13}/>{[session.cpu, session.ram, session.gpu].filter(Boolean).join(' · ') || 'Chưa có thông tin cấu hình'}</span></div>
        </> : <Empty title="Chưa có phiên chơi">Liên hệ nhân viên tại quầy để bắt đầu.</Empty>}
      </div>
      <div className="home-rig" aria-hidden="true"><div className="home-rig-orbit"/><div className="home-rig-screen">
        <Gamepad2 size={60} strokeWidth={1}/><span>READY TO PLAY</span><div className="home-rig-line"/></div><div className="home-rig-stand"/><div className="home-rig-keyboard"/></div>
      {session && <div className="home-session-footer">
        <span>Đơn giá phiên <strong>{money(session.pricePerHourSnapshot)}<small> / giờ</small></strong></span>
        <span>Tiền máy đã trừ ví <strong>{money(session.totalCost)}</strong></span>
        <button onClick={() => navigate('/client/invoices')}>Hóa đơn tiền giờ</button>
      </div>}
    </section>
    <section className="home-panel home-wallet" aria-labelledby="wallet-title">
      <div className="home-card-top"><h2 id="wallet-title">Ví của bạn</h2><WalletCards size={20}/></div>
      <span className="home-muted">Số dư hiện tại</span><div className="home-balance">{money(home.walletBalance)}</div>
      <button className="home-button primary" onClick={() => navigate('/client/wallet')}><Plus size={16}/> Nạp tiền vào ví</button>
      {session && <><div className="home-balance-row"><span>Tiền giờ đã thu</span><strong>{money(session.totalCost)}</strong></div>
        <div className={`home-remaining ${remaining < 1 ? 'low' : ''}`}><Clock3 size={13}/>
          Ước tính theo số dư: {Math.floor(remaining)} giờ {Math.floor(remaining * 60) % 60} phút
        </div></>}
    </section>
  </>;
}
