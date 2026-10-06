import React from 'react';
import { formatCurrency } from '../mockData';

export function InvoiceStats({ stats, statusFilter, setStatusFilter }) {
  const cards = [
    {
      id: 'all',
      label: 'Tổng hóa đơn',
      value: stats.total,
      subValue: 'Toàn bộ giao dịch',
      color: '#2563EB',
      bg: '#EFF6FF',
      icon: '🧾',
      isClickable: true,
    },
    {
      id: 'revenue',
      label: 'Doanh thu thực nhận',
      value: formatCurrency(stats.totalRevenue),
      subValue: `${stats.paidCount} hóa đơn đã thu`,
      color: '#059669',
      bg: '#ECFDF5',
      icon: '💰',
      isClickable: false,
    },
    {
      id: 'paid',
      label: 'Đã thanh toán',
      value: stats.paidCount,
      subValue: 'Giao dịch thành công',
      color: '#059669',
      bg: '#ECFDF5',
      icon: '✅',
      isClickable: true,
    },
    {
      id: 'pending',
      label: 'Chờ xử lý',
      value: stats.pendingCount,
      subValue: 'Cần xác nhận thu tiền',
      color: '#D97706',
      bg: '#FFFBEB',
      icon: '⏳',
      isClickable: true,
    },
  ];

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '14px', marginBottom: '20px' }}>
      {cards.map((card) => {
        const isActive = statusFilter === card.id;
        return (
          <div
            key={card.id}
            onClick={() => {
              if (card.isClickable) {
                setStatusFilter(card.id);
              }
            }}
            style={{
              background: '#FFFFFF',
              border: isActive ? '2px solid #111C30' : '1px solid var(--border)',
              borderRadius: '10px',
              padding: '16px',
              cursor: card.isClickable ? 'pointer' : 'default',
              transition: 'all 0.15s ease',
            }}
          >
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '8px' }}>
              <span style={{ fontSize: '12px', fontWeight: 600, color: '#8993A4' }}>{card.label}</span>
              <div
                style={{
                  width: '28px',
                  height: '28px',
                  borderRadius: '6px',
                  display: 'grid',
                  placeItems: 'center',
                  background: card.bg,
                  color: card.color,
                  fontSize: '13px',
                }}
              >
                {card.icon}
              </div>
            </div>
            <div style={{ fontSize: '22px', fontWeight: 800, color: card.color, marginBottom: '2px' }}>
              {card.value}
            </div>
            <div style={{ fontSize: '11px', color: '#8993A4', fontWeight: 500 }}>{card.subValue}</div>
          </div>
        );
      })}
    </div>
  );
}
