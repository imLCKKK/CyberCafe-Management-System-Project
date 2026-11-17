import React from 'react';
import { ExternalLink, RefreshCw } from 'lucide-react';
import { customerDate, walletMoney } from '../../shared/customerWalletApi.js';
import { paymentCanBePaid, paymentIsApplied, paymentNeedsReview } from '../../shared/zaloPayPaymentApi.js';
import { createZaloPayPaymentQr } from './zaloPayPaymentQr.js';

const displayDate = value => Number.isFinite(Date.parse(value)) ? customerDate(value) : '—';

export function ZaloPayPaymentQr({ payment, enabled, now = Date.now() }) {
  const qr = createZaloPayPaymentQr(payment, enabled, now);
  if (!qr) return null;
  return <figure className="zalopay-payment-qr">
    <svg role="img" aria-label={`Mã QR ZaloPay Sandbox cho giao dịch #${payment.id}`}
      viewBox={`0 0 ${qr.size} ${qr.size}`} width={qr.size * 4} height={qr.size * 4}
      shapeRendering="crispEdges" xmlns="http://www.w3.org/2000/svg">
      <rect width={qr.size} height={qr.size} fill="#fff"/><path d={qr.path} fill="#000"/>
    </svg>
    <figcaption>Quét bằng app ZaloPay Sandbox và xác nhận thanh toán.<br/>Chỉ dùng số dư thử Sandbox.</figcaption>
  </figure>;
}

export function ZaloPayPaymentCard({ payment, enabled, syncing = false, syncError, onSync, now = Date.now() }) {
  const applied = paymentIsApplied(payment);
  const review = paymentNeedsReview(payment);
  const canPay = paymentCanBePaid(payment, enabled, now);
  const elapsed = Date.parse(payment.expiresAtUtc) <= now;
  const isTopUp = payment.purpose === 'TopUp';
  const status = review ? 'Cần kiểm tra thanh toán'
    : applied ? isTopUp ? 'Đã cộng vào ví' : 'Đã ghi nhận thanh toán đơn'
      : payment.status === 'Succeeded' ? 'Đã thanh toán · đang ghi nhận'
        : payment.status === 'Failed' ? 'Thanh toán chưa thành công'
          : payment.status === 'Expired' ? 'Giao dịch hết hạn'
            : elapsed ? 'Hết thời gian quét · chưa xác nhận kết quả' : 'Chờ thanh toán';
  const detail = review ? 'Giao dịch cần nhân viên kiểm tra. Vui lòng giữ mã giao dịch và không thanh toán lại.'
    : applied ? isTopUp ? 'Tiền đã được ghi vào lịch sử ví. Số dư được tải lại từ máy chủ.' : 'Khoản thanh toán đã được ghi nhận cho đơn này.'
      : payment.status === 'Succeeded' ? isTopUp ? 'Máy chủ đã xác minh thanh toán. Chờ ghi có vào ví; không cần quét hoặc nạp lại.' : 'Máy chủ đã xác minh thanh toán. Chờ xác nhận cho đơn; không cần trả thêm.'
        : payment.status === 'Failed' ? 'Máy chủ chưa xác nhận thanh toán thành công. Nếu app đã trừ số dư thử, hãy kiểm tra lại trạng thái trước khi thanh toán tiếp.'
          : payment.status === 'Expired' || elapsed ? 'Không quét mã đã hết hạn. Nếu đã thanh toán, hãy kiểm tra trạng thái hoặc liên hệ nhân viên.'
            : 'Ví và đơn chỉ thay đổi sau khi máy chủ xác minh kết quả. Mở cổng thanh toán hoặc quay lại trang không xác nhận đã trả tiền.';
  return <article className={`zalopay-payment-card${applied ? ' is-applied' : ''}${review ? ' needs-review' : ''}`} aria-label={`Giao dịch ZaloPay #${payment.id}`}>
    <div className="zalopay-payment-heading"><strong>{isTopUp ? 'Nạp ví' : `Thanh toán đơn #${payment.sourceId}`} · {walletMoney(payment.expectedAmountVnd)}</strong><span>#{payment.id}</span></div>
    <p className="zalopay-payment-status" role="status">{status}</p><p>{detail}</p>
    <dl><div><dt>Mã giao dịch</dt><dd>{payment.merchantTransactionId}</dd></div>
      <div><dt>Tạo lúc</dt><dd>{displayDate(payment.createdAtUtc)}</dd></div>
      <div><dt>Hạn thanh toán</dt><dd>{displayDate(payment.expiresAtUtc)}</dd></div>
      {applied && <div><dt>{isTopUp ? 'Giao dịch ví' : 'Thanh toán đơn'}</dt><dd>#{isTopUp ? payment.walletTransactionId : payment.paymentId} · {displayDate(payment.appliedAtUtc)}</dd></div>}
    </dl>
    <ZaloPayPaymentQr payment={payment} enabled={enabled} now={now}/>
    <div className="zalopay-payment-actions">
      {canPay && <a className="home-button primary" href={payment.paymentUrl} target="_blank" rel="noopener noreferrer"><ExternalLink size={16}/>Mở ZaloPay Sandbox</a>}
      {!applied && <button type="button" className="home-button" disabled={syncing} onClick={() => onSync?.(payment)}><RefreshCw size={16}/>{syncing ? 'Đang kiểm tra…' : 'Kiểm tra trạng thái'}</button>}
    </div>
    {!applied && syncError && <p className="home-alert" role="alert">{syncError}</p>}
  </article>;
}
