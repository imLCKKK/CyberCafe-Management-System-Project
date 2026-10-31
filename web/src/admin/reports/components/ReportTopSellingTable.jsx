import React from 'react';
import { formatCurrency } from '../mockData';

export function ReportTopSellingTable({ topItems }) {
  if (topItems.length === 0) {
    return (
      <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '10px', padding: '30px', textAlign: 'center' }}>
        <h3 style={{ fontSize: '14px', fontWeight: 800, color: 'var(--text)', margin: '0 0 6px 0' }}>Top 5 sản phẩm & dịch vụ bán chạy</h3>
        <p style={{ fontSize: '12px', color: '#8993A4', margin: 0 }}>Chưa có dữ liệu bán hàng trong khoảng thời gian này.</p>
      </div>
    );
  }

  const maxQuantity = topItems[0]?.quantitySold || 1;

  return (
    <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '10px', padding: '18px' }}>
      <div style={{ marginBottom: '14px' }}>
        <h3 style={{ fontSize: '14px', fontWeight: 800, color: 'var(--text)', margin: '0 0 4px 0' }}>Top 5 sản phẩm & dịch vụ bán chạy</h3>
        <p style={{ fontSize: '11px', color: '#8993A4', margin: 0 }}>
          Xếp hạng theo số lượng bán ra của các món ăn, nước uống, combo và thẻ game
        </p>
      </div>

      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '12px' }}>
          <thead>
            <tr style={{ background: '#F8FAFC', borderBottom: '1px solid var(--border)', color: '#8993A4', fontSize: '11px', textTransform: 'uppercase', letterSpacing: '0.6px' }}>
              <th style={{ padding: '8px 12px', width: '50px' }}>Hạng</th>
              <th style={{ padding: '8px 12px' }}>Tên mặt hàng / Dịch vụ</th>
              <th style={{ padding: '8px 12px', width: '110px' }}>Danh mục</th>
              <th style={{ padding: '8px 12px', width: '90px' }}>Số lượng</th>
              <th style={{ padding: '8px 12px', width: '110px' }}>Doanh thu</th>
              <th style={{ padding: '8px 12px', width: '130px' }}>Tỷ trọng top</th>
            </tr>
          </thead>
          <tbody>
            {topItems.map((item) => {
              const rankIcon =
                item.rank === 1
                  ? '🥇'
                  : item.rank === 2
                  ? '🥈'
                  : item.rank === 3
                  ? '🥉'
                  : `#${item.rank}`;
              const ratio = Math.round((item.quantitySold / maxQuantity) * 100);

              return (
                <tr key={item.name} style={{ borderBottom: '1px solid var(--border)' }}>
                  {/* Rank */}
                  <td style={{ padding: '10px 12px', fontWeight: 800 }}>
                    {rankIcon}
                  </td>

                  {/* Name */}
                  <td style={{ padding: '10px 12px', fontWeight: 600, color: 'var(--text)' }}>{item.name}</td>

                  {/* Category */}
                  <td style={{ padding: '10px 12px' }}>
                    <span style={{ padding: '2px 7px', background: '#F1F5F9', borderRadius: '4px', fontSize: '11px', color: '#475569' }}>
                      {item.category}
                    </span>
                  </td>

                  {/* Quantity */}
                  <td style={{ padding: '10px 12px' }}>
                    <strong style={{ color: 'var(--text)' }}>{item.quantitySold}</strong>{' '}
                    <span style={{ fontSize: '11px', color: '#8993A4' }}>{item.unit}</span>
                  </td>

                  {/* Revenue */}
                  <td style={{ padding: '10px 12px', fontWeight: 700, color: 'var(--text)' }}>
                    {formatCurrency(item.revenue)}
                  </td>

                  {/* Ratio bar */}
                  <td style={{ padding: '10px 12px' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '6px' }}>
                      <div style={{ flex: 1, height: '6px', background: '#F1F5F9', borderRadius: '3px', overflow: 'hidden' }}>
                        <div
                          style={{
                            height: '100%',
                            width: `${ratio}%`,
                            backgroundColor:
                              item.rank === 1
                                ? '#EAB308'
                                : item.rank === 2
                                ? '#94A3B8'
                                : item.rank === 3
                                ? '#D97706'
                                : '#3B82F6',
                          }}
                        />
                      </div>
                      <span style={{ fontSize: '10px', fontWeight: 700, color: '#8993A4', minWidth: '28px', textAlign: 'right' }}>
                        {ratio}%
                      </span>
                    </div>
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
    </div>
  );
}
