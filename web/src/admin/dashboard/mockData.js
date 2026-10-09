export const recentOrders = [
  {
    OrderId: 1,
    ComputerId: 12,
    ProductName: "Burger & khoai tây",
    MinutesAgo: 2,
    Status: "Pending",
  },
  {
    OrderId: 2,
    ComputerId: 5,
    ProductName: "Cà phê sữa đá",
    MinutesAgo: 5,
    Status: "Pending",
  },
  {
    OrderId: 3,
    ComputerId: 31,
    ProductName: "Snack phô mai",
    MinutesAgo: 8,
    Status: "Delivered",
  },
  {
    OrderId: 4,
    ComputerId: 19,
    ProductName: "Pizza hải sản",
    MinutesAgo: 11,
    Status: "Pending",
  },
  {
    OrderId: 5,
    ComputerId: 27,
    ProductName: "Bánh mì xúc xích",
    MinutesAgo: 14,
    Status: "Delivered",
  },
  {
    OrderId: 6,
    ComputerId: 26,
    ProductName: "Sinh tố xoài",
    MinutesAgo: 18,
    Status: "Pending",
  },
  {
    OrderId: 7,
    ComputerId: 43,
    ProductName: "Cánh gà chiên",
    MinutesAgo: 22,
    Status: "Delivered",
  },
  {
    OrderId: 8,
    ComputerId: 34,
    ProductName: "Cà phê đen đá",
    MinutesAgo: 27,
    Status: "Pending",
  },
  {
    OrderId: 9,
    ComputerId: 45,
    ProductName: "Mì xào bò",
    MinutesAgo: 30,
    Status: "Pending",
  },
  {
    OrderId: 10,
    ComputerId: 12,
    ProductName: "Nước suối",
    MinutesAgo: 32,
    Status: "Pending",
  },
];
export const revenue = { Today: 2480000, Yesterday: 2214286 };
export const foodAlertIds = new Set(
  recentOrders
    .filter((order) => order.Status === "Pending")
    .map((order) => order.ComputerId),
);
