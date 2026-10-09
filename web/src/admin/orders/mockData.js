export const customers = [
  { CustomerId: 1, FullName: "Nguyễn Minh Anh", Username: "minhanh" },
  { CustomerId: 2, FullName: "Trần Hoàng Nam", Username: "hoangnam" },
  { CustomerId: 3, FullName: "Lê Gia Huy", Username: "giahuy" },
  { CustomerId: 4, FullName: "Phạm Tuấn Kiệt", Username: "tuankiet" },
];
export const computers = [12, 8, 4, 19].map((number, index) => ({
  ComputerId: index + 1,
  ComputerName: `Máy ${String(number).padStart(2, "0")}`,
}));
export const gamingSessions = customers.map((customer, index) => ({
  SessionId: index + 1,
  CustomerId: customer.CustomerId,
  ComputerId: index + 1,
  Status: "Active",
}));
export function createOrderSeed(now = Date.now()) {
  const rows = [
    [
      1048,
      1,
      "Pending",
      [
        [1, 1, 28000],
        [3, 1, 15000],
      ],
      3,
    ],
    [
      1047,
      2,
      "Pending",
      [
        [2, 1, 35000],
        [7, 2, 12000],
      ],
      6,
    ],
    [
      1046,
      3,
      "Preparing",
      [
        [4, 2, 20000],
        [6, 1, 18000],
      ],
      10,
    ],
    [
      1045,
      4,
      "Ready",
      [
        [5, 1, 25000],
        [12, 1, 15000],
      ],
      14,
    ],
    [
      1044,
      2,
      "Delivered",
      [
        [1, 1, 28000],
        [9, 1, 8000],
      ],
      28,
    ],
    [1043, 3, "Delivered", [[6, 2, 18000]], 42],
    [1042, 1, "Cancelled", [[10, 1, 38000]], 55],
  ];
  let detailId = 1;
  const orders = [],
    orderDetails = [],
    orderStatusHistory = [];
  for (const [id, customer, status, items, minutes] of rows) {
    const time = new Date(now - minutes * 60000).toISOString();
    orders.push({
      OrderId: id,
      CustomerId: customer,
      SessionId: customer,
      EmployeeId: status === "Pending" ? null : 1,
      InvoiceId: null,
      OrderTime: time,
      Status: status,
      TotalAmount: items.reduce((sum, [, qty, price]) => sum + qty * price, 0),
    });
    items.forEach(([product, qty, price]) =>
      orderDetails.push({
        OrderDetailId: detailId++,
        OrderId: id,
        ProductId: product,
        Quantity: qty,
        UnitPrice: price,
      }),
    );
    const flow =
      status === "Cancelled"
        ? ["Pending", "Cancelled"]
        : ["Pending", "Preparing", "Ready", "Delivered"].slice(
            0,
            ["Pending", "Preparing", "Ready", "Delivered"].indexOf(status) + 1,
          );
    flow.forEach((step, index) =>
      orderStatusHistory.push({
        HistoryId: orderStatusHistory.length + 1,
        OrderId: id,
        OldStatus: index ? flow[index - 1] : null,
        NewStatus: step,
        ChangedBy: index ? 1 : null,
        ChangedAt: new Date(
          new Date(time).getTime() + index * 60000,
        ).toISOString(),
      }),
    );
  }
  return { orders, orderDetails, orderStatusHistory };
}
export const mockData = createOrderSeed().orders;
