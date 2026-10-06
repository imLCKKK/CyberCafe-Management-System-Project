export const INITIAL_INVOICES = [
  {
    id: 'HD001',
    orderCode: 'ORD-1001',
    customerName: 'Khách vãng lai (Máy 03)',
    customerType: 'Khách vãng lai',
    employeeName: 'Admin (Ca 1)',
    paymentMethod: 'Tiền mặt',
    status: 'paid',
    type: 'Dịch vụ & ăn uống',
    totalAmount: 44000,
    createdAt: '06/10/2026 08:15',
    items: [
      { name: 'Sting Dâu 330ml', quantity: 2, price: 12000, subtotal: 24000 },
      { name: 'Mì Tôm Hảo Hảo Trứng', quantity: 1, price: 20000, subtotal: 20000 },
    ],
  },
  {
    id: 'HD002',
    orderCode: 'ORD-1002',
    customerName: 'Trần Hoàng Nam (HV002)',
    customerType: 'Thành viên',
    employeeName: 'Admin (Ca 1)',
    paymentMethod: 'Ví hội viên',
    status: 'paid',
    type: 'Nạp giờ chơi',
    totalAmount: 50000,
    createdAt: '06/10/2026 08:30',
    items: [
      { name: 'Nạp giờ chơi (Máy 12 - 5 giờ)', quantity: 1, price: 50000, subtotal: 50000 },
    ],
  },
  {
    id: 'HD003',
    orderCode: 'ORD-1003',
    customerName: 'Khách vãng lai (Máy 08)',
    customerType: 'Khách vãng lai',
    employeeName: 'Admin (Ca 1)',
    paymentMethod: 'Chuyển khoản',
    status: 'paid',
    type: 'Dịch vụ & ăn uống',
    totalAmount: 22000,
    createdAt: '06/10/2026 08:45',
    items: [
      { name: 'Coca Cola 320ml', quantity: 1, price: 12000, subtotal: 12000 },
      { name: 'Snack Khoai Tây Oishi', quantity: 1, price: 10000, subtotal: 10000 },
    ],
  },
  {
    id: 'HD004',
    orderCode: 'ORD-1004',
    customerName: 'Lê Quốc Tuấn (HV005)',
    customerType: 'Thành viên',
    employeeName: 'Admin (Ca 1)',
    paymentMethod: 'Ví hội viên',
    status: 'paid',
    type: 'Dịch vụ & ăn uống',
    totalAmount: 42000,
    createdAt: '06/10/2026 09:10',
    items: [
      { name: 'Mì Xào Bò Rau Cải', quantity: 1, price: 30000, subtotal: 30000 },
      { name: 'Pepsi Vị Chanh Muối', quantity: 1, price: 12000, subtotal: 12000 },
    ],
  },
  {
    id: 'HD005',
    orderCode: 'ORD-1005',
    customerName: 'Khách vãng lai (Máy 01)',
    customerType: 'Khách vãng lai',
    employeeName: 'Admin (Ca 1)',
    paymentMethod: 'Tiền mặt',
    status: 'paid',
    type: 'Nạp giờ chơi',
    totalAmount: 100000,
    createdAt: '06/10/2026 09:25',
    items: [
      { name: 'Nạp giờ chơi (Máy 01 - 10 giờ)', quantity: 1, price: 100000, subtotal: 100000 },
    ],
  },
  {
    id: 'HD006',
    orderCode: 'ORD-1006',
    customerName: 'Khách vãng lai (Máy 15)',
    customerType: 'Khách vãng lai',
    employeeName: 'Admin (Ca 1)',
    paymentMethod: 'Chuyển khoản',
    status: 'pending',
    type: 'Dịch vụ & ăn uống',
    totalAmount: 66000,
    createdAt: '06/10/2026 09:40',
    items: [
      { name: 'Nước Tăng Lực Redbull', quantity: 1, price: 16000, subtotal: 16000 },
      { name: 'Thẻ Garena 50.000 đ', quantity: 1, price: 50000, subtotal: 50000 },
    ],
    note: 'Chờ khách quét mã QR ngân hàng',
  },
  {
    id: 'HD007',
    orderCode: 'ORD-1007',
    customerName: 'Phạm Minh Đạt (HV003)',
    customerType: 'Thành viên',
    employeeName: 'Admin (Ca 1)',
    paymentMethod: 'Ví hội viên',
    status: 'paid',
    type: 'Combo',
    totalAmount: 70000,
    createdAt: '06/10/2026 10:00',
    items: [
      { name: 'Combo Đêm (6 giờ chơi + Sting + Mì trứng)', quantity: 1, price: 70000, subtotal: 70000 },
    ],
  },
  {
    id: 'HD008',
    orderCode: 'ORD-1008',
    customerName: 'Khách vãng lai (Máy 06)',
    customerType: 'Khách vãng lai',
    employeeName: 'Admin (Ca 1)',
    paymentMethod: 'Tiền mặt',
    status: 'paid',
    type: 'Dịch vụ & ăn uống',
    totalAmount: 14000,
    createdAt: '06/10/2026 10:15',
    items: [
      { name: 'Nước Khoáng Aquafina 500ml', quantity: 2, price: 7000, subtotal: 14000 },
    ],
  },
  {
    id: 'HD009',
    orderCode: 'ORD-1009',
    customerName: 'Nguyễn Văn An (HV001)',
    customerType: 'Thành viên',
    employeeName: 'Admin (Ca 1)',
    paymentMethod: 'Ví hội viên',
    status: 'paid',
    type: 'Dịch vụ & ăn uống',
    totalAmount: 20000,
    createdAt: '06/10/2026 10:30',
    items: [
      { name: 'Thẻ Garena 20.000 đ', quantity: 1, price: 20000, subtotal: 20000 },
    ],
  },
  {
    id: 'HD010',
    orderCode: 'ORD-1010',
    customerName: 'Khách vãng lai (Máy 20)',
    customerType: 'Khách vãng lai',
    employeeName: 'Admin (Ca 1)',
    paymentMethod: 'Chuyển khoản',
    status: 'cancelled',
    type: 'Dịch vụ & ăn uống',
    totalAmount: 22000,
    createdAt: '06/10/2026 10:45',
    items: [
      { name: 'Trà Xanh Không Độ', quantity: 1, price: 10000, subtotal: 10000 },
      { name: 'Xúc Xích Tiệt Trùng CP', quantity: 1, price: 12000, subtotal: 12000 },
    ],
    note: 'Khách hủy đơn do bận về đột xuất',
  },
];

export const formatCurrency = (val) => {
  return `${Number(val || 0).toLocaleString('vi-VN')}đ`;
};

export const getInvoiceStatusInfo = (status) => {
  switch (status) {
    case 'paid':
      return {
        label: 'Đã thanh toán',
        className: 'inv-status-paid',
      };
    case 'pending':
      return {
        label: 'Chờ xử lý',
        className: 'inv-status-pending',
      };
    case 'cancelled':
      return {
        label: 'Đã hủy',
        className: 'inv-status-cancelled',
      };
    default:
      return {
        label: status,
        className: '',
      };
  }
};

export const calculateInvoiceStats = (invoices) => {
  let totalRevenue = 0;
  let paidCount = 0;
  let pendingCount = 0;
  let cancelledCount = 0;

  for (const inv of invoices) {
    if (inv.status === 'paid') {
      totalRevenue += inv.totalAmount;
      paidCount++;
    } else if (inv.status === 'pending') {
      pendingCount++;
    } else if (inv.status === 'cancelled') {
      cancelledCount++;
    }
  }

  return {
    total: invoices.length,
    totalRevenue,
    paidCount,
    pendingCount,
    cancelledCount,
  };
};
