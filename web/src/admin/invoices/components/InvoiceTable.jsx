import React from 'react';
import { Eye, CheckCircle2, AlertCircle } from 'lucide-react';
import { formatCurrency, getInvoiceStatusInfo } from '../mockData';

export function InvoiceTable({ invoices, onOpenDetail, onQuickPayCash }) {
  if (invoices.length === 0) {
    return (
      <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '10px', padding: '48px 20px', textAlign: 'center', color: '#8993A4' }}>
        <AlertCircle size={30} style={{ marginBottom: '8px' }} />
        <h3 style={{ fontSize: '15px', fontWeight: 700, color: 'var(--text)', margin: '0 0 4px 0' }}>
          Không tìm thấy hóa đơn nào
        </h3>
        <p style={{ fontSize: '12px', margin: 0 }}>
          Thử thay đổi từ khóa tìm kiếm hoặc chọn lại bộ lọc trạng thái và phương thức.
        </p>
      </div>
    );
  }

  const getStatusStyle = (status) => {
    switch (status) {
      case 'paid':
        return { bg: '#ECFDF5', color: '#047857', border: '#A7F3D0' };
      case 'pending':
        return { bg: '#FFFBEB', color: '#B45309', border: '#FDE68A' };
      case 'cancelled':
        return { bg: '#FEF2F2', color: '#B91C1C', border: '#FECACA' };
      default:
        return { bg: '#F1F5F9', color: '#64748B', border: '#E2E8F0' };
    }
  };

  return (
    <div style={{ background: '#FFFFFF', border: '1px solid var(--border)', borderRadius: '10px', overflow: 'hidden' }}>
      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '12px' }}>
          <thead>
            <tr style={{ background: '#F8FAFC', borderBottom: '1px solid var(--border)', color: '#8993A4', fontSize: '11px', textTransform: 'uppercase', letterSpacing: '0.6px' }}>
              <th style={{ padding: '10px 14px' }}>Mã HĐ</th>
              <th style={{ padding: '10px 14px' }}>Thời gian</th>
              <th style={{ padding: '10px 14px' }}>Khách hàng</th>
              <th style={{ padding: '10px 14px' }}>Phân loại</th>
              <th style={{ padding: '10px 14px' }}>Phương thức</th>
              <th style={{ padding: '10px 14px' }}>Tổng tiền</th>
              <th style={{ padding: '10px 14px' }}>Trạng thái</th>
              <th style={{ padding: '10px 14px', textAlign: 'right' }}>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {invoices.map((inv) => {
              const statusInfo = getInvoiceStatusInfo(inv.status);
              const statusSt = getStatusStyle(inv.status);
              return (
                <tr key={inv.id} style={{ borderBottom: '1px solid var(--border)' }}>
                  {/* Mã HĐ */}
                  <td style={{ padding: '11px 14px' }}>
                    <div style={{ fontFamily: 'monospace', fontWeight: 700, color: 'var(--text)' }}>{inv.id}</div>
                    <div style={{ fontSize: '10px', color: '#8993A4', marginTop: '2px' }}>{inv.orderCode}</div>
                  </td>

                  {/* Thời gian */}
                  <td style={{ padding: '11px 14px', color: '#8993A4' }}>{inv.createdAt}</td>

                  {/* Khách hàng */}
                  <td style={{ padding: '11px 14px' }}>
                    <div style={{ fontWeight: 600, color: 'var(--text)' }}>{inv.customerName}</div>
                    <span
                      style={{
                        display: 'inline-block',
                        fontSize: '10px',
                        fontWeight: 600,
                        padding: '1px 6px',
                        borderRadius: '4px',
                        marginTop: '2px',
                        background: inv.customerType === 'Thành viên' ? '#EFF6FF' : '#F1F5F9',
                        color: inv.customerType === 'Thành viên' ? '#1D4ED8' : '#64748B',
                      }}
                    >
                      {inv.customerType}
                    </span>
                  </td>

                  {/* Phân loại */}
                  <td style={{ padding: '11px 14px', color: '#475569' }}>{inv.type}</td>

                  {/* Phương thức */}
                  <td style={{ padding: '11px 14px' }}>
                    <span style={{ padding: '3px 7px', borderRadius: '4px', background: '#F8FAFC', border: '1px solid var(--border)', fontSize: '11px', color: '#475569' }}>
                      {inv.paymentMethod === 'Tiền mặt'
                        ? '💵 Tiền mặt'
                        : inv.paymentMethod === 'Chuyển khoản'
                        ? '🏦 Chuyển khoản'
                        : '💳 Ví hội viên'}
                    </span>
                  </td>

                  {/* Tổng tiền */}
                  <td style={{ padding: '11px 14px', fontWeight: 800, color: 'var(--text)' }}>
                    {formatCurrency(inv.totalAmount)}
                  </td>

                  {/* Trạng thái */}
                  <td style={{ padding: '11px 14px' }}>
                    <span
                      style={{
                        display: 'inline-block',
                        padding: '3px 8px',
                        borderRadius: '12px',
                        fontSize: '10px',
                        fontWeight: 700,
                        background: statusSt.bg,
                        color: statusSt.color,
                        border: `1px solid ${statusSt.border}`,
                      }}
                    >
                      {statusInfo.label}
                    </span>
                  </td>

                  {/* Thao tác */}
                  <td style={{ padding: '11px 14px', textAlign: 'right' }}>
                    <div style={{ display: 'inline-flex', gap: '5px' }}>
                      {inv.status === 'pending' ? (
                        <>
                          <button
                            type="button"
                            onClick={() => onQuickPayCash(inv)}
                            style={{ padding: '4px 8px', borderRadius: '4px', background: '#ECFDF5', color: '#047857', border: '1px solid #A7F3D0', fontSize: '11px', fontWeight: 700, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: '3px' }}
                          >
                            <CheckCircle2 size={11} /> Thu tiền
                          </button>
                          <button
                            type="button"
                            onClick={() => onOpenDetail(inv)}
                            style={{ padding: '4px 7px', borderRadius: '4px', background: '#FFFFFF', color: '#475569', border: '1px solid var(--border)', fontSize: '11px', fontWeight: 600, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: '3px' }}
                          >
                            <Eye size={11} /> Xem
                          </button>
                        </>
                      ) : (
                        <button
                          type="button"
                          onClick={() => onOpenDetail(inv)}
                          style={{ padding: '4px 8px', borderRadius: '4px', background: '#FFFFFF', color: '#475569', border: '1px solid var(--border)', fontSize: '11px', fontWeight: 600, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: '3px' }}
                        >
                          <Eye size={11} /> Xem & In
                        </button>
                      )}
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
