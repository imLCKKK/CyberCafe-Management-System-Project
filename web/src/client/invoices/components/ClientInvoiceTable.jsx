import React from 'react';
import { ChevronRight, Utensils, WalletCards, Clock, Sparkles, FileText, AlertCircle } from 'lucide-react';
import { formatCurrency } from '../mockData';

export function ClientInvoiceTable({
  invoices,
  onOpenDetail,
  onPayWithWallet,
}) {
  const getTypeIcon = (type) => {
    switch (type) {
      case 'order':
        return <Utensils size={13} color="#38bdf8" />;
      case 'topup':
        return <WalletCards size={13} color="#baf34c" />;
      case 'playtime':
        return <Clock size={13} color="#fbbf24" />;
      case 'combo':
        return <Sparkles size={13} color="#a855f7" />;
      default:
        return <FileText size={13} color="#94a3b8" />;
    }
  };

  const getStatusStyle = (status) => {
    switch (status) {
      case 'completed':
        return { bg: '#22371c', color: '#baf34c' };
      case 'preparing':
        return { bg: '#172b38', color: '#38bdf8' };
      case 'pending_payment':
        return { bg: '#362a14', color: '#fbbf24' };
      case 'pending_confirmation':
        return { bg: '#362615', color: '#f97316' };
      default:
        return { bg: '#202628', color: '#a4abad' };
    }
  };

  if (invoices.length === 0) {
    return (
      <div className="client-card" style={{ padding: '48px 20px', textAlign: 'center', color: '#737c7f' }}>
        <AlertCircle size={28} style={{ marginBottom: '8px' }} />
        <h3 style={{ fontSize: '14px', fontWeight: 600, color: '#e8ecee', margin: '0 0 4px 0' }}>
          Không tìm thấy giao dịch nào
        </h3>
        <p style={{ fontSize: '11px', margin: 0 }}>
          Vui lòng thử chọn bộ lọc khác hoặc kiểm tra lại từ khóa tìm kiếm.
        </p>
      </div>
    );
  }

  return (
    <div className="client-card">
      <div style={{ overflowX: 'auto' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', textAlign: 'left', fontSize: '11px' }}>
          <thead>
            <tr style={{ background: '#141819', borderBottom: '1px solid #293034', color: '#737c7f', textTransform: 'uppercase', letterSpacing: '0.6px', fontSize: '10px' }}>
              <th style={{ padding: '10px 14px' }}>Mã & Loại</th>
              <th style={{ padding: '10px 14px' }}>Chi tiết món / Dịch vụ</th>
              <th style={{ padding: '10px 14px' }}>Thời gian</th>
              <th style={{ padding: '10px 14px' }}>Hình thức</th>
              <th style={{ padding: '10px 14px' }}>Số tiền</th>
              <th style={{ padding: '10px 14px' }}>Trạng thái</th>
              <th style={{ padding: '10px 14px', textAlign: 'right' }}>Thao tác</th>
            </tr>
          </thead>
          <tbody>
            {invoices.map((inv) => {
              const isTopUp = inv.amountType === 'topup';
              const isPending =
                inv.status === 'pending_payment' ||
                inv.status === 'pending_confirmation';
              const statusSt = getStatusStyle(inv.status);

              return (
                <tr
                  key={inv.id}
                  style={{ borderBottom: '1px solid #252b2d', color: '#dfe5e5' }}
                >
                  {/* Mã & Loại */}
                  <td style={{ padding: '10px 14px' }}>
                    <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
                      {getTypeIcon(inv.type)}
                      <div>
                        <div style={{ fontWeight: 700, fontFamily: 'monospace', color: '#e8ecee' }}>{inv.id}</div>
                        <div style={{ fontSize: '9px', color: '#737c7f' }}>{inv.typeLabel}</div>
                      </div>
                    </div>
                  </td>

                  {/* Chi tiết */}
                  <td style={{ padding: '10px 14px' }}>
                    <div style={{ fontWeight: 600, color: '#e8ecee' }}>{inv.title}</div>
                    <div style={{ fontSize: '10px', color: '#8b9497' }}>
                      {inv.items.map((i) => `${i.quantity}x ${i.name}`).join(', ')}
                    </div>
                    {inv.note && <div style={{ fontSize: '9px', color: '#636b6d', fontStyle: 'italic' }}>Ghi chú: {inv.note}</div>}
                  </td>

                  {/* Thời gian */}
                  <td style={{ padding: '10px 14px', color: '#8b9497' }}>{inv.time}</td>

                  {/* Hình thức */}
                  <td style={{ padding: '10px 14px' }}>
                    <span style={{ padding: '2px 6px', background: '#22292b', borderRadius: '4px', fontSize: '10px', color: '#a4abad' }}>
                      {inv.paymentMethod}
                    </span>
                  </td>

                  {/* Số tiền */}
                  <td style={{ padding: '10px 14px', fontWeight: 800 }}>
                    <span style={{ color: isTopUp ? '#baf34c' : isPending ? '#fbbf24' : '#e8ecee' }}>
                      {isTopUp ? '+' : ''}
                      {formatCurrency(inv.amount)}
                    </span>
                  </td>

                  {/* Trạng thái */}
                  <td style={{ padding: '10px 14px' }}>
                    <span
                      style={{
                        display: 'inline-block',
                        padding: '3px 8px',
                        borderRadius: '12px',
                        fontSize: '10px',
                        fontWeight: 700,
                        background: statusSt.bg,
                        color: statusSt.color,
                      }}
                    >
                      {inv.statusLabel}
                    </span>
                  </td>

                  {/* Thao tác */}
                  <td style={{ padding: '10px 14px', textAlign: 'right' }}>
                    <div style={{ display: 'inline-flex', gap: '6px', alignItems: 'center' }}>
                      {isPending && (
                        <button
                          type="button"
                          onClick={() => onPayWithWallet(inv)}
                          style={{
                            padding: '3px 8px',
                            borderRadius: '4px',
                            background: '#baf34c',
                            color: '#16200e',
                            border: 'none',
                            fontSize: '10px',
                            fontWeight: 800,
                            cursor: 'pointer',
                          }}
                        >
                          Dùng ví
                        </button>
                      )}
                      <button
                        type="button"
                        onClick={() => onOpenDetail(inv)}
                        style={{
                          padding: '3px 7px',
                          borderRadius: '4px',
                          background: '#252b2d',
                          color: '#a4abad',
                          border: '1px solid #363d40',
                          fontSize: '10px',
                          fontWeight: 600,
                          cursor: 'pointer',
                          display: 'inline-flex',
                          alignItems: 'center',
                          gap: '2px',
                        }}
                      >
                        Chi tiết <ChevronRight size={10} />
                      </button>
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
