export const INITIAL_CLIENT_SESSION = {
  username: 'minhanh_gamer',
  fullName: 'Nguyễn Minh Anh',
  avatarText: 'MA',
  machineId: 'Máy 12',
  roomName: 'Phòng Thi đấu - Tầng 2 - Máy hiệu năng cao',
  walletBalance: 153000,
  currentPlayTime: '02:15:00',
  startTime: '14:00 hôm nay',
  unpaidOrderAmount: 45000,
  sessionTotalExpense: 75000,
};

export const INITIAL_CLIENT_INVOICES = [
  {
    id: '#GM1048',
    title: 'Đơn món #GM1048',
    type: 'order',
    typeLabel: 'Gọi món',
    time: '16:05 Hôm nay',
    amount: 43000,
    amountType: 'expense',
    paymentMethod: 'Ví hội viên',
    status: 'preparing',
    statusLabel: 'Đang chuẩn bị',
    machineId: 'Máy 12',
    customerName: 'Nguyễn Minh Anh',
    createdAt: '2026-10-06T16:05:00',
    items: [
      { id: 'it-1', name: 'Mì xào bò', quantity: 1, unitPrice: 28000, subtotal: 28000 },
      { id: 'it-2', name: 'Trà đào cam sả', quantity: 1, unitPrice: 15000, subtotal: 15000 },
    ],
    note: 'Không cay, nhiều đá',
  },
  {
    id: '#NAP502',
    title: 'Nạp tiền vào ví',
    type: 'topup',
    typeLabel: 'Nạp tiền ví',
    time: '13:55 Hôm nay',
    amount: 150000,
    amountType: 'topup',
    paymentMethod: 'Chuyển khoản QR',
    status: 'completed',
    statusLabel: 'Thành công',
    machineId: 'Máy 12',
    customerName: 'Nguyễn Minh Anh',
    createdAt: '2026-10-06T13:55:00',
    items: [
      { id: 'it-topup', name: 'Nạp số dư tài khoản hội viên qua Momo QR', quantity: 1, unitPrice: 150000, subtotal: 150000 },
    ],
    note: 'Cổng thanh toán tự động Momo',
  },
  {
    id: '#GM1020',
    title: 'Đơn món #GM1020',
    type: 'order',
    typeLabel: 'Gọi món',
    time: '15:20 Hôm nay',
    amount: 45000,
    amountType: 'expense',
    paymentMethod: 'Ví hội viên',
    status: 'pending_payment',
    statusLabel: 'Chờ thanh toán',
    machineId: 'Máy 12',
    customerName: 'Nguyễn Minh Anh',
    createdAt: '2026-10-06T15:20:00',
    items: [
      { id: 'it-snack1', name: 'Khoai tây chiên lắc phô mai', quantity: 1, unitPrice: 30000, subtotal: 30000 },
      { id: 'it-snack2', name: 'Cà phê đen đá', quantity: 1, unitPrice: 15000, subtotal: 15000 },
    ],
    note: 'Giao tận bàn Máy 12',
  },
  {
    id: '#HD1026',
    title: 'Hóa đơn #HD1026',
    type: 'playtime',
    typeLabel: 'Giờ chơi máy',
    time: '19:00 Hôm qua',
    amount: 52000,
    amountType: 'expense',
    paymentMethod: 'Ví hội viên',
    status: 'completed',
    statusLabel: 'Đã thanh toán',
    machineId: 'Máy 12',
    customerName: 'Nguyễn Minh Anh',
    createdAt: '2026-10-05T19:00:00',
    items: [
      { id: 'it-play', name: 'Giờ chơi máy VIP (3.25 giờ x 16.000đ/h)', quantity: 1, unitPrice: 52000, subtotal: 52000 },
    ],
    note: 'Tự động trừ tài khoản sau khi kết thúc phiên',
  },
  {
    id: '#HD1019',
    title: 'Hóa đơn #HD1019',
    type: 'combo',
    typeLabel: 'Combo',
    time: '23:00 Ngày 03/10',
    amount: 65000,
    amountType: 'expense',
    paymentMethod: 'Tiền mặt',
    status: 'completed',
    statusLabel: 'Đã thanh toán',
    machineId: 'Máy 12',
    customerName: 'Nguyễn Minh Anh',
    createdAt: '2026-10-03T23:00:00',
    items: [
      { id: 'it-cb1', name: 'Combo Đêm (6 giờ chơi + 1 Sting dâu + 1 Mì trứng)', quantity: 1, unitPrice: 65000, subtotal: 65000 },
    ],
    note: 'Áp dụng khung giờ 23:00 - 05:00',
  },
  {
    id: '#GM1033',
    title: 'Đơn món #GM1033',
    type: 'order',
    typeLabel: 'Gọi món',
    time: '12:15 Ngày 03/10',
    amount: 50000,
    amountType: 'expense',
    paymentMethod: 'Ví hội viên',
    status: 'completed',
    statusLabel: 'Đã thanh toán',
    machineId: 'Máy 12',
    customerName: 'Nguyễn Minh Anh',
    createdAt: '2026-10-03T12:15:00',
    items: [
      { id: 'it-cg', name: 'Cơm gà xối mỡ', quantity: 1, unitPrice: 35000, subtotal: 35000 },
      { id: 'it-cc', name: 'Coca Cola lon 330ml', quantity: 1, unitPrice: 15000, subtotal: 15000 },
    ],
  },
];

export const formatCurrency = (val) => {
  return `${Number(val || 0).toLocaleString('vi-VN')}đ`;
};

export const calculateClientStats = (invoices, session) => {
  let unpaidAmount = 0;
  let totalExpenseSession = 0;

  for (const inv of invoices) {
    if (inv.status === 'pending_payment' || inv.status === 'pending_confirmation') {
      unpaidAmount += inv.amount;
    }
    // Session expenses: orders, playtime, combo (excluding wallet top-up)
    if (inv.amountType === 'expense' && inv.time.includes('Hôm nay')) {
      totalExpenseSession += inv.amount;
    }
  }

  return {
    walletBalance: session.walletBalance,
    unpaidAmount: unpaidAmount || session.unpaidOrderAmount,
    totalExpenseSession: totalExpenseSession || session.sessionTotalExpense,
    currentPlayTime: session.currentPlayTime,
  };
};
