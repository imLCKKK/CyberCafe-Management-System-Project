import React from 'react';
import { formatCurrency } from '../mockData';

export function ReportRevenueBreakdown({ typeSegments, methodSegments }) {
  return (
    <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(300px, 1fr))', gap: '16px', marginBottom: '20px' }}>
      {/* Box 1: By Service Type */}
      <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '10px', padding: '18px' }}>
        <h3 style={{ fontSize: '14px', fontWeight: 800, color: 'var(--text)', margin: '0 0 4px 0' }}>Cơ cấu nguồn thu</h3>
        <p style={{ fontSize: '11px', color: '#8993A4', margin: '0 0 14px 0' }}>Phân loại theo dịch vụ và giờ chơi</p>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
          {typeSegments.map((seg, idx) => (
            <div key={idx} style={{ display: 'flex', flexDirection: 'column', gap: '5px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <div style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: seg.color }} />
                  <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text)' }}>{seg.name}</span>
                </div>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: '6px' }}>
                  <span style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text)' }}>{formatCurrency(seg.amount)}</span>
                  <span style={{ fontSize: '10px', fontWeight: 700, color: '#8993A4', minWidth: '28px', textAlign: 'right' }}>{seg.percentage}%</span>
                </div>
              </div>

              <div style={{ height: '6px', background: '#F1F5F9', borderRadius: '3px', overflow: 'hidden' }}>
                <div style={{ height: '100%', width: `${Math.max(2, seg.percentage)}%`, backgroundColor: seg.color, borderRadius: '3px' }} />
              </div>
            </div>
          ))}
        </div>
      </div>

      {/* Box 2: By Payment Method */}
      <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '10px', padding: '18px' }}>
        <h3 style={{ fontSize: '14px', fontWeight: 800, color: 'var(--text)', margin: '0 0 4px 0' }}>Phương thức thanh toán</h3>
        <p style={{ fontSize: '11px', color: '#8993A4', margin: '0 0 14px 0' }}>Doanh thu thu về qua từng kênh</p>

        <div style={{ display: 'flex', flexDirection: 'column', gap: '12px' }}>
          {methodSegments.map((seg, idx) => (
            <div key={idx} style={{ display: 'flex', flexDirection: 'column', gap: '5px' }}>
              <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
                <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                  <div style={{ width: '8px', height: '8px', borderRadius: '50%', backgroundColor: seg.color }} />
                  <span style={{ fontSize: '12px', fontWeight: 600, color: 'var(--text)' }}>{seg.name}</span>
                </div>
                <div style={{ display: 'flex', alignItems: 'baseline', gap: '6px' }}>
                  <span style={{ fontSize: '12px', fontWeight: 700, color: 'var(--text)' }}>{formatCurrency(seg.amount)}</span>
                  <span style={{ fontSize: '10px', fontWeight: 700, color: '#8993A4', minWidth: '28px', textAlign: 'right' }}>{seg.percentage}%</span>
                </div>
              </div>

              <div style={{ height: '6px', background: '#F1F5F9', borderRadius: '3px', overflow: 'hidden' }}>
                <div style={{ height: '100%', width: `${Math.max(2, seg.percentage)}%`, backgroundColor: seg.color, borderRadius: '3px' }} />
              </div>
            </div>
          ))}
        </div>
      </div>
    </div>
  );
}
