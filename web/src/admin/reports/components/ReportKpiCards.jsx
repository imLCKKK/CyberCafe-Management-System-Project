import React from 'react';
import { formatCurrency } from '../mockData';

export function ReportKpiCards({ kpis }) {
  const cards = [
    {
      id: 'revenue',
      label: 'Tổng doanh thu',
      value: formatCurrency(kpis.totalRevenue),
      subValue: 'Doanh thu thực thu',
      color: '#059669',
      bg: '#ECFDF5',
      icon: '💰',
    },
    {
      id: 'orders',
      label: 'Hóa đơn hoàn tất',
      value: `${kpis.totalOrders} đơn`,
      subValue: 'Giao dịch thành công',
      color: '#2563EB',
      bg: '#EFF6FF',
      icon: '🧾',
    },
    {
      id: 'avg',
      label: 'Giá trị trung bình / đơn',
      value: formatCurrency(kpis.avgOrderValue),
      subValue: 'AOV phòng net',
      color: '#D97706',
      bg: '#FFFBEB',
      icon: '📊',
    },
    {
      id: 'items',
      label: 'Số lượng sản phẩm bán ra',
      value: `${kpis.totalItemsSold} món`,
      subValue: 'Đồ ăn, nước, combo & thẻ',
      color: '#7C3AED',
      bg: '#F5F3FF',
      icon: '📦',
    },
  ];

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '14px', marginBottom: '20px' }}>
      {cards.map((card) => (
        <div
          key={card.id}
          style={{
            background: '#FFFFFF',
            border: '1px solid var(--border)',
            borderRadius: '10px',
            padding: '16px',
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
      ))}
    </div>
  );
}
