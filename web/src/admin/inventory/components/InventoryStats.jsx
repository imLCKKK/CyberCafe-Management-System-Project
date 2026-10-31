import React from 'react';

export function InventoryStats({ stats, statusFilter, setStatusFilter }) {
  const cards = [
    {
      id: 'all',
      label: 'Tổng mặt hàng',
      value: stats.total,
      subValue: `Tổng tồn: ${stats.totalStockQuantity} đơn vị`,
      color: '#2563EB',
      bg: '#EFF6FF',
      icon: '📦',
    },
    {
      id: 'in_stock',
      label: 'Còn hàng an toàn',
      value: stats.inStock,
      subValue: 'Tồn kho > 5',
      color: '#059669',
      bg: '#ECFDF5',
      icon: '✅',
    },
    {
      id: 'low_stock',
      label: 'Cảnh báo sắp hết',
      value: stats.lowStock,
      subValue: 'Tồn kho ≤ 5',
      color: '#D97706',
      bg: '#FFFBEB',
      icon: '⚠️',
    },
    {
      id: 'out_of_stock',
      label: 'Đã hết hàng',
      value: stats.outOfStock,
      subValue: 'Cần nhập gấp',
      color: '#DC2626',
      bg: '#FEF2F2',
      icon: '❌',
    },
  ];

  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(200px, 1fr))', gap: '14px', marginBottom: '20px' }}>
      {cards.map((card) => {
        const isActive = statusFilter === card.id;
        return (
          <div
            key={card.id}
            onClick={() => setStatusFilter(card.id)}
            style={{
              background: '#FFFFFF',
              border: isActive ? '2px solid #111C30' : '1px solid var(--border)',
              borderRadius: '10px',
              padding: '16px',
              cursor: 'pointer',
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
