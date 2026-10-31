export const REPORT_INVOICES_DATA = [
  {
    id: 'HD001',
    orderCode: 'ORD-1001',
    customerName: 'Khách vãng lai (Máy 03)',
    type: 'Dịch vụ & ăn uống',
    paymentMethod: 'Tiền mặt',
    status: 'paid',
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
    type: 'Nạp giờ chơi',
    paymentMethod: 'Ví hội viên',
    status: 'paid',
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
    type: 'Dịch vụ & ăn uống',
    paymentMethod: 'Chuyển khoản',
    status: 'paid',
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
    type: 'Dịch vụ & ăn uống',
    paymentMethod: 'Ví hội viên',
    status: 'paid',
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
    type: 'Nạp giờ chơi',
    paymentMethod: 'Tiền mặt',
    status: 'paid',
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
    type: 'Dịch vụ & ăn uống',
    paymentMethod: 'Chuyển khoản',
    status: 'pending',
    totalAmount: 66000,
    createdAt: '06/10/2026 09:40',
    items: [
      { name: 'Nước Tăng Lực Redbull', quantity: 1, price: 16000, subtotal: 16000 },
      { name: 'Thẻ Garena 50.000 đ', quantity: 1, price: 50000, subtotal: 50000 },
    ],
  },
  {
    id: 'HD007',
    orderCode: 'ORD-1007',
    customerName: 'Phạm Minh Đạt (HV003)',
    type: 'Combo',
    paymentMethod: 'Ví hội viên',
    status: 'paid',
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
    type: 'Dịch vụ & ăn uống',
    paymentMethod: 'Tiền mặt',
    status: 'paid',
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
    type: 'Dịch vụ & ăn uống',
    paymentMethod: 'Ví hội viên',
    status: 'paid',
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
    type: 'Dịch vụ & ăn uống',
    paymentMethod: 'Chuyển khoản',
    status: 'cancelled',
    totalAmount: 22000,
    createdAt: '06/10/2026 10:45',
    items: [
      { name: 'Trà Xanh Không Độ', quantity: 1, price: 10000, subtotal: 10000 },
      { name: 'Xúc Xích Tiệt Trùng CP', quantity: 1, price: 12000, subtotal: 12000 },
    ],
  },
];

export const formatCurrency = (val) => {
  return `${Number(val || 0).toLocaleString('vi-VN')}đ`;
};

export const calculateReportData = (invoices, period = 'all') => {
  // Only calculate for paid invoices
  const paidInvoices = invoices.filter((inv) => inv.status === 'paid');

  let totalRevenue = 0;
  let totalItemsSold = 0;

  let playAmount = 0;
  let playCount = 0;
  let serviceAmount = 0;
  let serviceCount = 0;
  let comboAmount = 0;
  let comboCount = 0;

  let cashAmount = 0;
  let cashCount = 0;
  let transferAmount = 0;
  let transferCount = 0;
  let walletAmount = 0;
  let walletCount = 0;

  const itemMap = {};

  for (const inv of paidInvoices) {
    totalRevenue += inv.totalAmount;

    // By service type
    if (inv.type === 'Nạp giờ chơi') {
      playAmount += inv.totalAmount;
      playCount++;
    } else if (inv.type === 'Dịch vụ & ăn uống') {
      serviceAmount += inv.totalAmount;
      serviceCount++;
    } else if (inv.type === 'Combo') {
      comboAmount += inv.totalAmount;
      comboCount++;
    }

    // By payment method
    if (inv.paymentMethod === 'Tiền mặt') {
      cashAmount += inv.totalAmount;
      cashCount++;
    } else if (inv.paymentMethod === 'Chuyển khoản') {
      transferAmount += inv.totalAmount;
      transferCount++;
    } else if (inv.paymentMethod === 'Ví hội viên') {
      walletAmount += inv.totalAmount;
      walletCount++;
    }

    // Calculate items sold and top selling items
    // (Bỏ qua nạp giờ chơi riêng lẻ, không tính vào số lượng sản phẩm bán ra)
    if (inv.type !== 'Nạp giờ chơi') {
      for (const it of inv.items) {
        if (it.name.toLowerCase().includes('nạp giờ chơi') && inv.type !== 'Combo') {
          continue;
        }

        totalItemsSold += it.quantity;

        if (!itemMap[it.name]) {
          let cat = 'Đồ ăn';
          let unit = 'Phần';

          if (inv.type === 'Combo' || it.name.toLowerCase().includes('combo')) {
            cat = 'Combo';
            unit = 'Combo';
          } else if (it.name.toLowerCase().includes('thẻ')) {
            cat = 'Thẻ nạp';
            unit = 'Thẻ';
          } else if (
            it.name.toLowerCase().includes('mì') ||
            it.name.toLowerCase().includes('bánh') ||
            it.name.toLowerCase().includes('xúc xích') ||
            it.name.toLowerCase().includes('snack')
          ) {
            cat = 'Đồ ăn';
            unit = 'Món';
          } else {
            cat = 'Nước uống';
            unit = 'Lon';
          }

          itemMap[it.name] = {
            name: it.name,
            category: cat,
            quantitySold: 0,
            revenue: 0,
            unit,
          };
        }

        itemMap[it.name].quantitySold += it.quantity;
        itemMap[it.name].revenue += it.subtotal;
      }
    }
  }

  const totalOrders = paidInvoices.length;
  const avgOrderValue = totalOrders > 0 ? Math.round(totalRevenue / totalOrders) : 0;

  const total = totalRevenue || 1;

  const typeSegments = [
    {
      name: 'Nạp giờ chơi máy trạm',
      amount: playAmount,
      percentage: Math.round((playAmount / total) * 100),
      count: playCount,
      color: '#2563EB',
    },
    {
      name: 'Dịch vụ đồ ăn & nước uống',
      amount: serviceAmount,
      percentage: Math.round((serviceAmount / total) * 100),
      count: serviceCount,
      color: '#10B981',
    },
    {
      name: 'Gói Combo giờ & món',
      amount: comboAmount,
      percentage: Math.round((comboAmount / total) * 100),
      count: comboCount,
      color: '#8B5CF6',
    },
  ];

  const methodSegments = [
    {
      name: '💵 Tiền mặt tại quầy',
      amount: cashAmount,
      percentage: Math.round((cashAmount / total) * 100),
      count: cashCount,
      color: '#059669',
    },
    {
      name: '🏦 Chuyển khoản ngân hàng',
      amount: transferAmount,
      percentage: Math.round((transferAmount / total) * 100),
      count: transferCount,
      color: '#3B82F6',
    },
    {
      name: '💳 Ví hội viên (Trừ tài khoản)',
      amount: walletAmount,
      percentage: Math.round((walletAmount / total) * 100),
      count: walletCount,
      color: '#6366F1',
    },
  ];

  // Top 5 items sorted by quantity sold descending
  const topList = Object.values(itemMap);
  topList.sort((a, b) => b.quantitySold - a.quantitySold || b.revenue - a.revenue);
  const topItems = topList.slice(0, 5).map((item, index) => ({
    rank: index + 1,
    ...item,
  }));

  return {
    kpis: {
      totalRevenue,
      totalOrders,
      avgOrderValue,
      totalItemsSold,
    },
    typeSegments,
    methodSegments,
    topItems,
  };
};
