import React from 'react';
import { WalletCards, Clock, AlertCircle, Receipt } from 'lucide-react';
import { formatCurrency } from '../mockData';

export function ClientInvoiceSummary({ session, stats, onSelectFilter }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(210px, 1fr))', gap: '12px', marginBottom: '16px' }}>
      {/* 1. Wallet Balance */}
      <div className="client-card">
        <div className="client-card-inner">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span className="client-balance-label">Số dư ví hiện tại</span>
            <WalletCards size={14} color="#baf34c" />
          </div>
          <div className="client-balance" style={{ margin: '8px 0 10px', fontSize: '22px' }}>
            {formatCurrency(stats.walletBalance)}
          </div>
          <button
            type="button"
            className="client-deposit"
            onClick={() => onSelectFilter('topup')}
          >
            <WalletCards size={11} /> + &nbsp;Nạp tiền
          </button>
        </div>
      </div>

      {/* 2. Unpaid Orders */}
      <div className="client-card">
        <div className="client-card-inner">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span className="client-balance-label">Gọi món chưa thanh toán</span>
            <AlertCircle size={14} color="#fbbf24" />
          </div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: '#fbbf24', margin: '8px 0 6px' }}>
            {formatCurrency(stats.unpaidAmount)}
          </div>
          <div style={{ fontSize: '11px', color: '#737c7f', lineHeight: 1.4 }}>
            {stats.unpaidAmount > 0 ? 'Các đơn món đang chờ thanh toán' : 'Đã thanh toán hết các đơn món'}
          </div>
          {stats.unpaidAmount > 0 && (
            <button
              type="button"
              style={{ background: 'none', border: 'none', color: '#fbbf24', fontSize: '11px', fontWeight: 700, padding: 0, marginTop: '8px', cursor: 'pointer' }}
              onClick={() => onSelectFilter('pending')}
            >
              Xem đơn chờ →
            </button>
          )}
        </div>
      </div>

      {/* 3. Session Total Expense */}
      <div className="client-card">
        <div className="client-card-inner">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span className="client-balance-label">Tổng chi tiêu phiên này</span>
            <Receipt size={14} color="#38bdf8" />
          </div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: '#e8ecee', margin: '8px 0 6px' }}>
            {formatCurrency(stats.totalExpenseSession)}
          </div>
          <div style={{ fontSize: '11px', color: '#737c7f', lineHeight: 1.4 }}>
            Gồm giờ chơi máy, đồ ăn & combo
          </div>
        </div>
      </div>

      {/* 4. Session Play Time */}
      <div className="client-card">
        <div className="client-card-inner">
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span className="client-balance-label">{session.machineId} · Thời gian chơi</span>
            <Clock size={14} color="#baf34c" />
          </div>
          <div style={{ fontSize: '22px', fontWeight: 800, color: '#baf34c', margin: '8px 0 6px' }}>
            {stats.currentPlayTime}
          </div>
          <div style={{ fontSize: '11px', color: '#737c7f', lineHeight: 1.4 }}>
            Bắt đầu lúc {session.startTime}
          </div>
        </div>
      </div>
    </div>
  );
}
