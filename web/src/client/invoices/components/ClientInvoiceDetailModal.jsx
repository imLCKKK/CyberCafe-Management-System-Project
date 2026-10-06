import React, { useState } from 'react';
import { X, WalletCards, Banknote } from 'lucide-react';
import { formatCurrency } from '../mockData';

export function ClientInvoiceDetailModal({
  isOpen,
  invoice,
  walletBalance,
  onClose,
  onPayWithWallet,
  onRequestCashPayment,
}) {
  const [feedbackMsg, setFeedbackMsg] = useState(null);

  if (!isOpen || !invoice) return null;

  const isPending =
    invoice.status === 'pending_payment' || invoice.status === 'preparing';
  const isAwaitingConfirmation = invoice.status === 'pending_confirmation';
  const isTopUp = invoice.amountType === 'topup';

  const handleWalletPay = () => {
    if (walletBalance < invoice.amount) {
      setFeedbackMsg({
        type: 'error',
        text: '⚠️ Số dư ví không đủ. Vui lòng nạp thêm tiền!',
      });
      return;
    }
    onPayWithWallet(invoice.id);
    setFeedbackMsg({
      type: 'success',
      text: '✅ Thanh toán thành công qua Ví hội viên!',
    });
  };

  const handleCashPay = () => {
    onRequestCashPayment(invoice.id);
    setFeedbackMsg({
      type: 'info',
      text: '🔔 Đã gửi yêu cầu: Nhân viên sẽ thu tiền mặt tại Máy 12!',
    });
  };

  return (
    <div
      style={{
        position: 'fixed',
        inset: 0,
        background: 'rgba(5, 7, 9, 0.8)',
        backdropFilter: 'blur(3px)',
        display: 'grid',
        placeItems: 'center',
        zIndex: 1000,
        padding: '16px',
      }}
      onClick={onClose}
    >
      <div
        className="client-card"
        style={{
          width: '100%',
          maxWidth: '460px',
          maxHeight: '90vh',
          display: 'flex',
          flexDirection: 'column',
          background: '#171d1f',
          borderColor: '#363d40',
        }}
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div style={{ padding: '14px 18px', borderBottom: '1px solid #283033', display: 'flex', justifyContent: 'space-between', alignItems: 'center', background: '#131719' }}>
          <div>
            <div style={{ fontSize: '10px', fontWeight: 800, color: '#baf34c', letterSpacing: '0.8px' }}>
              ⚡ TRẠM CYBER LOUNGE
            </div>
            <div style={{ fontSize: '14px', fontWeight: 800, color: '#e8ecee', marginTop: '2px' }}>
              CHI TIẾT HÓA ĐƠN ĐIỆN TỬ
            </div>
          </div>
          <button
            onClick={onClose}
            style={{ background: 'none', border: 'none', color: '#737c7f', cursor: 'pointer', padding: '4px' }}
          >
            <X size={16} />
          </button>
        </div>

        {/* Body */}
        <div style={{ padding: '16px 18px', overflowY: 'auto', flex: 1, fontSize: '11px' }}>
          {feedbackMsg && (
            <div
              style={{
                background: '#203518',
                border: '1px solid #395726',
                borderRadius: '6px',
                padding: '8px 12px',
                color: feedbackMsg.type === 'error' ? '#f87171' : feedbackMsg.type === 'info' ? '#fbbf24' : '#baf34c',
                marginBottom: '12px',
                textAlign: 'center',
                fontWeight: 600,
              }}
            >
              {feedbackMsg.text}
            </div>
          )}

          {/* Metadata */}
          <div style={{ background: '#1c2225', border: '1px solid #293033', borderRadius: '8px', padding: '10px 12px', marginBottom: '14px', display: 'flex', flexDirection: 'column', gap: '6px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: '#737c7f' }}>Mã hóa đơn:</span>
              <span style={{ color: '#baf34c', fontFamily: 'monospace', fontWeight: 700 }}>{invoice.id}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: '#737c7f' }}>Máy sử dụng:</span>
              <span style={{ color: '#e8ecee', fontWeight: 600 }}>{invoice.machineId}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: '#737c7f' }}>Khách hàng:</span>
              <span style={{ color: '#e8ecee', fontWeight: 600 }}>{invoice.customerName}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: '#737c7f' }}>Thời gian:</span>
              <span style={{ color: '#e8ecee' }}>{invoice.time}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: '#737c7f' }}>Phương thức:</span>
              <span style={{ color: '#e8ecee' }}>{invoice.paymentMethod}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span style={{ color: '#737c7f' }}>Trạng thái:</span>
              <span style={{ color: invoice.status === 'completed' ? '#baf34c' : invoice.status === 'pending_confirmation' ? '#f97316' : '#fbbf24', fontWeight: 700 }}>
                {invoice.statusLabel}
              </span>
            </div>
          </div>

          {/* Items breakdown */}
          <table style={{ width: '100%', borderCollapse: 'collapse', marginBottom: '14px' }}>
            <thead>
              <tr style={{ background: '#141719', color: '#737c7f', fontSize: '10px', textAlign: 'left' }}>
                <th style={{ padding: '6px 8px' }}>Tên món / Dịch vụ</th>
                <th style={{ padding: '6px 8px', textAlign: 'center', width: '36px' }}>SL</th>
                <th style={{ padding: '6px 8px', textAlign: 'right', width: '75px' }}>Đơn giá</th>
                <th style={{ padding: '6px 8px', textAlign: 'right', width: '75px' }}>T.Tiền</th>
              </tr>
            </thead>
            <tbody>
              {invoice.items.map((it) => (
                <tr key={it.id} style={{ borderBottom: '1px solid #242a2d', color: '#dfe5e5' }}>
                  <td style={{ padding: '7px 8px', fontWeight: 600 }}>{it.name}</td>
                  <td style={{ padding: '7px 8px', textAlign: 'center', color: '#8b9497' }}>{it.quantity}</td>
                  <td style={{ padding: '7px 8px', textAlign: 'right', color: '#8b9497' }}>{formatCurrency(it.unitPrice)}</td>
                  <td style={{ padding: '7px 8px', textAlign: 'right', fontWeight: 700, color: '#e8ecee' }}>{formatCurrency(it.subtotal)}</td>
                </tr>
              ))}
            </tbody>
          </table>

          {invoice.note && (
            <div style={{ fontSize: '10px', fontStyle: 'italic', color: '#8b9497', marginBottom: '12px' }}>
              * Ghi chú: {invoice.note}
            </div>
          )}

          {/* Total */}
          <div style={{ borderTop: '1px solid #2d3538', paddingTop: '10px', display: 'flex', flexDirection: 'column', gap: '6px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between', color: '#737c7f' }}>
              <span>Tạm tính:</span>
              <span style={{ color: '#e8ecee' }}>{formatCurrency(invoice.amount)}</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', color: '#737c7f' }}>
              <span>Giảm giá:</span>
              <span style={{ color: '#e8ecee' }}>0đ</span>
            </div>
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'baseline', paddingTop: '6px', borderTop: '1px solid #2d3538' }}>
              <span style={{ fontSize: '12px', fontWeight: 800, color: '#e8ecee' }}>TỔNG THANH TOÁN:</span>
              <span style={{ fontSize: '16px', fontWeight: 900, color: isTopUp ? '#baf34c' : '#e8ecee' }}>
                {isTopUp ? '+' : ''}{formatCurrency(invoice.amount)}
              </span>
            </div>
          </div>
        </div>

        {/* Footer */}
        <div style={{ padding: '12px 18px', background: '#131719', borderTop: '1px solid #283033', display: 'flex', flexDirection: 'column', gap: '6px' }}>
          {isPending ? (
            <>
              <button
                type="button"
                onClick={handleWalletPay}
                style={{
                  width: '100%',
                  padding: '9px',
                  borderRadius: '6px',
                  background: '#baf34c',
                  color: '#16200e',
                  border: 'none',
                  fontSize: '11px',
                  fontWeight: 800,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: '6px',
                }}
              >
                <WalletCards size={13} />
                Thanh toán bằng ví (Số dư: {formatCurrency(walletBalance)})
              </button>
              <button
                type="button"
                onClick={handleCashPay}
                style={{
                  width: '100%',
                  padding: '7px',
                  borderRadius: '6px',
                  background: '#252b2d',
                  color: '#e8ecee',
                  border: '1px solid #363d40',
                  fontSize: '11px',
                  fontWeight: 600,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: '6px',
                }}
              >
                <Banknote size={13} />
                Thanh toán tiền mặt tại máy
              </button>
            </>
          ) : isAwaitingConfirmation ? (
            <>
              <button
                type="button"
                onClick={handleWalletPay}
                style={{
                  width: '100%',
                  padding: '9px',
                  borderRadius: '6px',
                  background: '#baf34c',
                  color: '#16200e',
                  border: 'none',
                  fontSize: '11px',
                  fontWeight: 800,
                  cursor: 'pointer',
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  gap: '6px',
                }}
              >
                <WalletCards size={13} />
                Đổi sang thanh toán ví ({formatCurrency(walletBalance)})
              </button>
              <button
                type="button"
                onClick={onClose}
                style={{
                  width: '100%',
                  padding: '7px',
                  borderRadius: '6px',
                  background: '#252b2d',
                  color: '#a4abad',
                  border: '1px solid #363d40',
                  fontSize: '11px',
                  fontWeight: 600,
                  cursor: 'pointer',
                }}
              >
                Đóng (Chờ nhân viên thu tại Máy 12)
              </button>
            </>
          ) : (
            <button
              type="button"
              onClick={onClose}
              style={{
                width: '100%',
                padding: '7px',
                borderRadius: '6px',
                background: '#252b2d',
                color: '#a4abad',
                border: '1px solid #363d40',
                fontSize: '11px',
                fontWeight: 600,
                cursor: 'pointer',
              }}
            >
              Đóng biên lai
            </button>
          )}
        </div>
      </div>
    </div>
  );
}
