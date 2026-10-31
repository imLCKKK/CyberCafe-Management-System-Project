import { dateTime, labels, minutesLabel, money, transactionLabels } from '../shared/demoModel.js';

export const paymentLabels = { Paid: 'Đã thanh toán', Partial: 'Còn tiền mặt chờ thu', Unpaid: 'Chưa thanh toán' };
const normalize = (value) => String(value).normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D').toLowerCase().replace(/\s+/g, ' ').trim();
const dayFormatter = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' });

export function receiptDateKey(value) {
  const date = new Date(value);
  if (!Number.isFinite(date.getTime())) return '';
  const parts = Object.fromEntries(dayFormatter.formatToParts(date).map((part) => [part.type, part.value]));
  return `${parts.year}-${parts.month}-${parts.day}`;
}

export function filterReceipts(receipts, db, { session = 'all', payment = 'all', from = '', to = '', query = '' } = {}) {
  if (from && to && from > to) return [];
  const search = normalize(query).replace(/\b(pc|gm)\s+(\d+)/g, '$1$2');
  return receipts.filter((receipt) => {
    const day = receiptDateKey(receipt.session.started_at);
    if (session !== 'all' && receipt.session.status !== session) return false;
    if (payment === 'due' && receipt.cashDue <= 0) return false;
    if (payment === 'paid' && receipt.paymentState !== 'Paid') return false;
    if (payment === 'refund' && receipt.refunds <= 0 && receipt.refundPending <= 0) return false;
    if ((from && day < from) || (to && day > to)) return false;
    if (!search) return true;
    const terms = [`#PC${receipt.id}`, receipt.computer?.code || '', ...receipt.orders.map((order) => `#GM${order.id}`), ...receipt.items.map((item) => db.products.find((product) => product.id === item.product_id)?.name || '')];
    return normalize(terms.join(' ')).includes(search);
  });
}

export function orderPaymentText(order) {
  if (!order.cash) return order.refundPending > 0 ? 'Ví TRAM · Chờ hoàn tiền món hủy' : 'Ví TRAM · Đã thanh toán';
  if (order.cashDue > 0) return 'Tiền mặt · Chờ thu';
  return order.cashPaid > 0 ? 'Tiền mặt · Đã thu' : 'Tiền mặt · Không phải thu';
}

export function receiptText(receipt, db, account, now = new Date()) {
  const productName = (item) => db.products.find((product) => product.id === item.product_id)?.name || `Món #${item.product_id}`;
  return [
    'TRAM / BẢNG KÊ PHIÊN CHƠI', `Mã: PC${receipt.id}`, `Khách hàng: ${account.display_name}`, `Điện thoại: ${account.phone}`,
    `Máy: ${receipt.computer?.code || '—'}`, `Bắt đầu: ${dateTime(receipt.session.started_at)}`,
    `Kết thúc: ${receipt.session.ended_at ? dateTime(receipt.session.ended_at) : 'Phiên đang hoạt động'}`,
    `Thời gian chơi: ${minutesLabel(receipt.session.started_at, receipt.session.ended_at || now)}`,
    `Trạng thái phiên: ${receipt.session.status === 'Active' ? 'Đang chơi · Bảng kê tạm tính' : 'Đã kết thúc'}`,
    `Thanh toán: ${paymentLabels[receipt.paymentState]}`, `Cập nhật tới: ${dateTime(receipt.updatedAt)}`, '',
    'TIỀN MÁY', `Đơn giá lúc bắt đầu: ${money(receipt.session.hourly_rate_snapshot)} / giờ`,
    `Đã ghi nhận tới: ${dateTime(receipt.session.last_charged_at)}`, `Tiền máy: ${money(receipt.session.charged_amount)}`, '',
    'ĐƠN MÓN', ...receipt.orderDetails.flatMap((order) => [
      `Đơn #GM${order.id} · ${dateTime(order.placed_at)} · ${orderPaymentText(order)}`,
      ...order.items.map((item) => `  ${item.quantity} × ${productName(item)} @ ${money(item.unit_price_snapshot)} = ${money(item.quantity * item.unit_price_snapshot)} · ${labels[item.status] || item.status}`),
      `  Giá trị ban đầu: ${money(order.total_amount)}`,
      ...(order.refunds > 0 ? [`  Đã hoàn vào ví: -${money(order.refunds)}`] : []),
      ...(order.cash && order.cancelled > 0 ? [`  Món tiền mặt đã hủy, không thu: -${money(order.cancelled)}`] : []),
      ...(order.refundPending > 0 ? [`  Tiền món hủy chờ hoàn: ${money(order.refundPending)}`] : []),
      `  Còn ghi nhận: ${money(order.net)}`, '',
    ]), ...(receipt.orderDetails.length ? [] : ['Không có đơn món.', '']),
    'TỔNG HỢP', `Tiền máy: ${money(receipt.session.charged_amount)}`, `Tiền món ban đầu: ${money(receipt.foodGross)}`,
    `Đã hoàn vào ví: -${money(receipt.refunds)}`, `Món tiền mặt hủy không thu: -${money(receipt.cashCancelled)}`,
    `Tổng ghi nhận: ${money(receipt.total)}`, `Đã trả bằng ví (sau hoàn tiền): ${money(receipt.walletPaid)}`,
    `Đã thu tiền mặt: ${money(receipt.cashPaid)}`, `Tổng đã thanh toán: ${money(receipt.paidTotal)}`,
    `Tiền mặt chờ thu: ${money(receipt.cashDue)}`, ...(receipt.refundPending > 0 ? [`Tiền món hủy chờ hoàn: ${money(receipt.refundPending)} (chưa trừ khỏi tổng ghi nhận)`] : []), '',
    'GIAO DỊCH VÍ TRONG PHIÊN', ...receipt.transactions.map((row) => `#GD${row.id} · ${dateTime(row.recorded_at)} · ${transactionLabels[row.transaction_type] || row.transaction_type} · ${row.amount >= 0 ? '+' : '-'}${money(Math.abs(row.amount))} · Số dư sau giao dịch: ${money(row.balance_after)}`),
    ...(receipt.transactions.length ? [] : ['Chưa có giao dịch ví.']), '',
    receipt.cashDue > 0 ? 'Chỉ khoản tiền mặt chờ thu được thanh toán với nhân viên; không thu lại khoản đã trả bằng ví.' : 'Không còn tiền mặt phải thu theo bảng kê này.',
    'Phiên đang chơi tiếp tục cập nhật. Dữ liệu demo, đây là bảng kê sử dụng, không phải hóa đơn thuế.',
  ].join('\n');
}
