import React from 'react';

const PERIODS = [
  { id: 'today', label: 'Hôm nay' },
  { id: '7days', label: '7 ngày qua' },
  { id: 'this_month', label: 'Tháng này' },
  { id: 'all', label: 'Toàn thời gian' },
];

export function ReportPeriodFilter({ selectedPeriod, onSelectPeriod }) {
  return (
    <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '20px', flexWrap: 'wrap', gap: '12px' }}>
      <div>
        <h2 style={{ fontSize: '18px', fontWeight: 800, color: 'var(--text)', margin: '0 0 4px 0' }}>
          Báo cáo doanh thu & hoạt động
        </h2>
        <p style={{ fontSize: '12px', color: '#8993A4', margin: 0 }}>
          Dữ liệu thống kê tự động đồng bộ từ module Hóa đơn & Kho hàng
        </p>
      </div>

      <div style={{ display: 'flex', gap: '6px', background: '#F1F5F9', padding: '3px', borderRadius: '20px' }}>
        {PERIODS.map((p) => {
          const isActive = selectedPeriod === p.id;
          return (
            <button
              key={p.id}
              type="button"
              onClick={() => onSelectPeriod(p.id)}
              style={{
                padding: '6px 14px',
                borderRadius: '16px',
                border: 'none',
                background: isActive ? '#111C30' : 'transparent',
                color: isActive ? '#FFFFFF' : '#64748B',
                fontSize: '11px',
                fontWeight: 600,
                cursor: 'pointer',
                transition: 'all 0.15s ease',
              }}
            >
              {p.label}
            </button>
          );
        })}
      </div>
    </div>
  );
}
