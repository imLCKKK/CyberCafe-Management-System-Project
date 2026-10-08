export const initialBalance = 153000;

export const topUpPromoCodes = [
	{ code: 'TRAM10', name: 'Nạp ví - chơi thả ga', discountType: 'Percentage', discountValue: 10, startDate: '2026-10-01T00:00:00', endDate: '2026-10-31T23:59:59', isActive: true },
	{ code: 'NAP20K', name: 'Ưu đãi nạp ví', discountType: 'FixedAmount', discountValue: 20000, startDate: '2026-10-01T00:00:00', endDate: '2026-10-31T23:59:59', isActive: true },
];

export const mockData = [
	{ id: 5508, amount: -43000, balanceAfter: 153000, type: 'OrderPayment', description: 'Đơn món #GM1048 · Mì xào bò', createdAt: '2026-10-08T16:05:00' },
	{ id: 5507, amount: 150000, balanceAfter: 196000, type: 'TopUp', description: 'Nạp tiền qua ngân hàng', createdAt: '2026-10-08T13:55:00' },
	{ id: 5506, amount: -53000, balanceAfter: 46000, type: 'GamingSession', description: 'Thanh toán phiên chơi · Máy 12', createdAt: '2026-10-07T21:12:00' },
	{ id: 5505, amount: 79000, balanceAfter: 99000, type: 'TopUp', description: 'Nạp tiền tại quầy', createdAt: '2026-10-07T19:50:00' },
	{ id: 5504, amount: 20000, balanceAfter: 20000, type: 'Refund', description: 'Hoàn tiền đơn món #GM1039', createdAt: '2026-10-06T18:32:00' },
];
