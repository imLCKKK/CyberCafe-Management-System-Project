export const INVENTORY_CATEGORIES = ['Tất cả', 'Nước uống', 'Đồ ăn', 'Thẻ nạp'];

export const INITIAL_INVENTORY_ITEMS = [
  {
    id: 'SP001',
    name: 'Sting dâu lon 330ml',
    category: 'Nước uống',
    costPrice: 8000,
    salePrice: 15000,
    stock: 48,
    unit: 'Lon',
    status: 'in_stock',
  },
  {
    id: 'SP002',
    name: 'Coca Cola lon 330ml',
    category: 'Nước uống',
    costPrice: 9000,
    salePrice: 15000,
    stock: 35,
    unit: 'Lon',
    status: 'in_stock',
  },
  {
    id: 'SP003',
    name: 'Redbull Thái lon 250ml',
    category: 'Nước uống',
    costPrice: 12000,
    salePrice: 20000,
    stock: 4,
    unit: 'Lon',
    status: 'low_stock',
  },
  {
    id: 'SP004',
    name: 'Mì xào bò đặc biệt',
    category: 'Đồ ăn',
    costPrice: 15000,
    salePrice: 35000,
    stock: 18,
    unit: 'Phần',
    status: 'in_stock',
  },
  {
    id: 'SP005',
    name: 'Cơm gà xối mỡ giòn',
    category: 'Đồ ăn',
    costPrice: 22000,
    salePrice: 45000,
    stock: 12,
    unit: 'Dĩa',
    status: 'in_stock',
  },
  {
    id: 'SP006',
    name: 'Xúc xích Đức nướng phô mai',
    category: 'Đồ ăn',
    costPrice: 7000,
    salePrice: 15000,
    stock: 0,
    unit: 'Cây',
    status: 'out_of_stock',
  },
  {
    id: 'SP007',
    name: 'Thẻ Garena 50.000đ',
    category: 'Thẻ nạp',
    costPrice: 47000,
    salePrice: 50000,
    stock: 20,
    unit: 'Thẻ',
    status: 'in_stock',
  },
  {
    id: 'SP008',
    name: 'Thẻ Zing 100.000đ',
    category: 'Thẻ nạp',
    costPrice: 94000,
    salePrice: 100000,
    stock: 6,
    unit: 'Thẻ',
    status: 'in_stock',
  },
];

export const formatCurrency = (val) => {
  return `${Number(val || 0).toLocaleString('vi-VN')}đ`;
};

export const calculateInventoryStats = (items) => {
  let totalStockQuantity = 0;
  let inStock = 0;
  let lowStock = 0;
  let outOfStock = 0;

  for (const item of items) {
    totalStockQuantity += item.stock;
    if (item.stock === 0) {
      outOfStock++;
    } else if (item.stock <= 5) {
      lowStock++;
    } else {
      inStock++;
    }
  }

  return {
    total: items.length,
    totalStockQuantity,
    inStock,
    lowStock,
    outOfStock,
  };
};
