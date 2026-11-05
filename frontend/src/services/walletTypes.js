// Nhãn tiếng Việt cho WalletTransactions.Type (xem Backend/docs/002-wallet-ledger.md).
export const walletTypeLabels = {
  TopUp: 'Nạp tiền vào ví',
  GamingCharge: 'Phí phiên chơi',
  OrderPayment: 'Thanh toán đơn món',
  OrderRefund: 'Hoàn tiền món',
  TournamentFee: 'Lệ phí giải đấu',
  TournamentRefund: 'Hoàn phí giải đấu',
  Adjustment: 'Điều chỉnh ví',
};
export const walletTypeLabel = (type) => walletTypeLabels[type] || type;

// Giao dịch ví: API camelCase (transactionId, type, createdAt...) -> dạng các trang đang dùng.
export const toWalletTransaction = (r) => ({
  id: r.transactionId,
  transaction_type: r.type,
  amount: Number(r.amount),
  balance_after: Number(r.balanceAfter),
  description: r.description,
  recorded_at: r.createdAt,
});
