import React from 'react';
import { Monitor, Plus, ArrowUpRight, Utensils, WalletCards, ReceiptText, ChevronRight } from 'lucide-react';

const foods = [
  ['Mì xào bò', '28.000đ'],
  ['Cơm gà xối mỡ', '35.000đ'],
  ['Trà đào cam sả', '15.000đ'],
];

export default function HomePage(){
  return <div className="client-page">
    <div className="client-page-heading">
      <div><h1>Chào Minh Anh, sẵn sàng vào trận?</h1><p>Mọi thứ bạn cần cho buổi chơi hôm nay, ngay tại đây.</p></div>
      <button className="client-action"><Utensils size={12}/> Gọi món tại máy</button>
    </div>

    <div className="client-grid">
      <div>
        <section className="client-card client-machine-card">
          <div className="client-card-inner">
            <div className="client-machine-name"><Monitor size={14}/> Máy 12 <span className="client-machine-status"><i/> Đang chơi</span></div>
            <div className="client-machine-time">02:15:00</div>
            <div className="client-machine-meta">Bắt đầu lúc 14:00 hôm nay</div>
            <div className="client-machine-meta">Phòng Thi đấu · Tầng 2 · Máy hiệu năng cao</div>
            <div className="client-machine-meta">Intel Core i5-12400F · RAM 16 GB · RTX 3060</div>
          </div>
          <div className="client-machine-image" />
        </section>

        <section className="client-section">
          <div className="client-section-title"><div><h2>Nạp năng lượng, tiếp trận</h2></div><a>Xem thực đơn ↗</a></div>
          <div className="client-order-grid">
            {foods.map(([name,price])=><div className="client-food" key={name}><div className="client-food-img client-skeleton"/><div className="client-food-body"><div className="client-food-type">Món ăn</div><div className="client-food-name">{name}</div><div className="client-food-bottom"><span className="client-food-price">{price}</span><button className="client-add"><Plus size={11}/></button></div></div></div>)}
          </div>
        </section>
      </div>

      <div>
        <section className="client-card client-side-card"><div className="client-card-inner"><div className="client-balance-label">Số dư ví hiện tại</div><div className="client-balance">153.000đ</div><button className="client-deposit"><WalletCards size={11}/> + &nbsp;Nạp tiền</button><div className="client-balance-row"><span>Gọi món · chưa thanh toán</span><strong>43.000đ</strong></div><div className="client-balance-row"><span>Tổng chi tiêu phiên này</span><strong>70.000đ</strong></div></div></section>
        <section className="client-section client-card client-tournament-card"><div className="client-tournament-content"><span className="client-tag">GIẢI ĐẤU SẮP TỚI</span><h3>Đấu trường cuối tuần</h3><p>VALORANT · Tối 5 người</p><p>14:00 · Thứ Bảy, 10/10/2026</p><button className="client-tournament-button">Xem chi tiết & đăng ký →</button></div></section>
      </div>
    </div>

    <section className="client-section"><div className="client-section-title"><h2>Hoạt động gần đây</h2><a>Xem báo cáo ↗</a></div><div className="client-activity"><div className="client-activity-head"><div>Giao dịch / đơn hàng</div><div>Thời gian</div><div>Số tiền</div><div>Trạng thái</div><div/></div>{[['Đơn món #GM1048','16:05','43.000đ','Đang chuẩn bị',Utensils],['Nạp tiền vào ví','13:55','+150.000đ','Thành công',WalletCards],['Hóa đơn #HD1026','19:00','52.000đ','Đã thanh toán',ReceiptText]].map(([title,time,amount,status,Icon])=><div className="client-activity-row" key={title}><div className="client-activity-main"><div className="activity-icon"><Icon size={11}/></div><div><span>{title}</span><small>Chi tiết giao dịch / đơn hàng</small></div></div><div>{time}</div><div>{amount}</div><div className={`client-status ${status==='Đang chuẩn bị'?'pending':''}`}>{status}</div><ChevronRight size={12}/></div>)}</div></section>
  </div>
}
