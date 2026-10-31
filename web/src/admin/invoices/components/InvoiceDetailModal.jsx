import React from 'react';
import { X, Printer, Banknote, CreditCard } from 'lucide-react';
import { formatCurrency, getInvoiceStatusInfo } from '../mockData';

export function InvoiceDetailModal({
  isOpen,
  invoice,
  onClose,
  onMarkAsPaid,
}) {
  if (!isOpen || !invoice) return null;

  const statusInfo = getInvoiceStatusInfo(invoice.status);

  const handlePrint = () => {
    window.print();
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(17, 28, 48, 0.65)',
        backdropFilter: 'blur(2px)',
        display: 'grid',
        placeItems: 'center',
        zIndex: 1000,
        padding: '16px',
      }}
      onClick={onClose}
    >
      <div
        style={{
          background: '#FFFFFF',
          borderRadius: '12px',
          width: '100%',
          maxWidth: '460px',
          maxHeight: '90vh',
          display: 'flex',
          flexDirection: 'column',
          overflow: 'hidden',
          boxShadow: '0 20px 30px rgba(0, 0, 0, 0.15)',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div style={{ padding: '16px 18px', borderBottom: '1px solid var(--border)', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: '8px' }}>
            <div style={{ width: '28px', height: '28px', background: '#111C30', color: '#FFF', borderRadius: '6px', fontWeight: 800, fontSize: '14px', display: 'grid', placeItems: 'center' }}>
              N
            </div>
            <div>
              <div style={{ fontSize: '13px', fontWeight: 800, color: '#111C30' }}>NET CAFE</div>
              <div style={{ fontSize: '10px', color: '#8993A4' }}>Chi nhánh mẫu · Cyber Hub</div>
            </div>
          </div>
          <div style={{ textAlign: 'right' }}>
            <div style={{ fontSize: '13px', fontWeight: 800, color: 'var(--text)' }}>BIÊN LAI HÓA ĐƠN</div>
            <div style={{ fontSize: '10px', color: '#8993A4', fontFamily: 'monospace' }}>{invoice.id} · {invoice.orderCode}</div>
          </div>
        </div>

        {/* Body */}
        <div style={{ padding: '16px 18px', overflowY: 'auto', flex: 1, fontSize: '12px' }}>
          {/* Metadata */}
          <div style={{ background: '#F8FAFC', border: '1px solid var(--border)', borderRadius: '8px', padding: '10px 12px', marginBottom: '14px', display: 'flex', flexDirection: 'column', gap: '6px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: '#8993A4' }}>Khách hàng:</span>
              <span style={{ fontWeight: 600, color: 'var(--text)' }}>{invoice.customerName}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: '#8993A4' }}>Thời gian:</span>
              <span style={{ color: 'var(--text)' }}>{invoice.createdAt}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: '#8993A4' }}>Thu ngân:</span>
              <span style={{ color: 'var(--text)' }}>{invoice.employeeName}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: '#8993A4' }}>Phương thức:</span>
              <span style={{ fontWeight: 600, color: 'var(--text)' }}>{invoice.paymentMethod}</span>
            </div>
          </div>

          {/* Items breakdown */}
          <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: '14px' }}>
            <thead>
              <tr style={{ background: '#F1F5F9', color: '#64748B', fontSize: '11px', textAlign: 'left' }}>
                <th style={{ padding: '7px 8px' }}>Mặt hàng / Dịch vụ</th>
                <th style={{ padding: '7px 8px', textAlign: 'center', width: '36px' }}>SL</th>
                <th style={{ padding: '7px 8px', textAlign: 'right', width: '75px' }}>Đơn giá</th>
                <th style={{ padding: '7px 8px', textAlign: 'right', width: '75px' }}>Thành tiền</th>
              </tr>
            </thead>
            <tbody>
              {invoice.items.map((item, idx) => (
                <tr key={idx} style={{ borderBottom: '1px solid #F1F5F9' }}>
                  <td style={{ padding: '7px 8px', fontWeight: 600 }}>{item.name}</td>
                  <td style={{ padding: '7px 8px', textAlign: 'center', color: '#8993A4' }}>{item.quantity}</td>
                  <td style={{ padding: '7px 8px', textAlign: 'right', color: '#8993A4' }}>{formatCurrency(item.price)}</td>
                  <td style={{ padding: '7px 8px', textAlign: 'right', fontWeight: 700, color: 'var(--text)' }}>{formatCurrency(item.subtotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {/* Total & Status */}
          <div style={{ borderTop: '2px solid #111C30', paddingTop: '10px', display: 'flex', flexDirection: 'column', gap: '6px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline' }}>
              <span style={{ fontSize: '13px', fontWeight: 800, color: 'var(--text)' }}>TỔNG CỘNG:</span>
              <span style={{ fontSize: '18px', fontWeight: 900, color: 'var(--text)' }}>{formatCurrency(invoice.totalAmount)}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
              <span style={{ color: '#8993A4' }}>Trạng thái:</span>
              <span style={{ fontWeight: 700 }}>{statusInfo.label}</span>
            </div>
            {invoice.note && (
              <div style={{ fontSize: '11px', fontStyle: 'italic', color: '#8993A4', marginTop: '4px' }}>
                * Ghi chú: {invoice.note}
              </div>
            )}
          </div>

          {/* Cashier Confirmations if Pending */}
          {invoice.status === 'pending' && (
            <div style={{ marginTop: '14px', background: '#FFFBEB', border: '1px solid #FDE68A', borderRadius: '6px', padding: '10px' }}>
              <p style={{ margin: '0 0 8px 0', fontSize: '11px', fontWeight: 700, color: '#92400E' }}>Xác nhận thu tiền tại quầy:</p>
              <div style={{ display: 'flex', gap: '8px' }}>
                <button
                  type="button"
                  onClick={() => onMarkAsPaid(invoice.id, 'Tiền mặt')}
                  style={{ flex: 1, padding: '7px', background: '#059669', color: '#FFF', border: 'none', borderRadius: '4px', fontSize: '11px', fontWeight: 700, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: '4px' }}
                >
                  <Banknote size={12} /> Thu tiền mặt
                </button>
                <button
                  type="button"
                  onClick={() => onMarkAsPaid(invoice.id, 'Chuyển khoản')}
                  style={{ flex: 1, padding: '7px', background: '#2563EB', color: '#FFF', border: 'none', borderRadius: '4px', fontSize: '11px', fontWeight: 700, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', justifyContent: 'center', gap: '4px' }}
                >
                  <CreditCard size={12} /> Đã nhận chuyển khoản
                </button>
              </div>
            </div>
          )}
        </div>

        {/* Footer */}
        <div style={{ padding: '12px 18px', background: '#F8FAFC', borderTop: '1px solid var(--border)', display: 'flex', justifyContent: 'flex-end', gap: '8px' }}>
          <button
            type="button"
            onClick={onClose}
            style={{ padding: '7px 14px', borderRadius: '6px', border: '1px solid var(--border)', background: '#FFFFFF', fontSize: '12px', fontWeight: 600, cursor: 'pointer' }}
          >
            Đóng
          </button>
          <button
            type="button"
            onClick={handlePrint}
            style={{ padding: '7px 16px', borderRadius: '6px', border: 'none', background: '#111C30', color: '#FFFFFF', fontSize: '12px', fontWeight: 700, cursor: 'pointer', display: 'inline-flex', alignItems: 'center', gap: '5px' }}
          >
            <Printer size={13} /> In biên lai
          </button>
        </div>
      </div>
    </div>
  );
}
