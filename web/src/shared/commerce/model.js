import { categories, products } from "../../admin/products/mockData.js";
import {
  createOrderSeed,
  customers,
  computers,
  gamingSessions,
} from "../../admin/orders/mockData.js";

export const statuses = {
  Pending: "Chờ xác nhận",
  Preparing: "Đang chuẩn bị",
  Ready: "Sẵn sàng giao",
  Delivered: "Đã giao",
  Cancelled: "Đã hủy",
};
export const nextStatus = {
  Pending: "Preparing",
  Preparing: "Ready",
  Ready: "Delivered",
};
export const nextAction = {
  Pending: "Nhận đơn",
  Preparing: "Hoàn tất chuẩn bị",
  Ready: "Xác nhận đã giao",
};
export const money = (value) =>
  new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(value);
export const fold = (value) =>
  String(value)
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase();
export const nextId = (rows, key) =>
  Math.max(0, ...rows.map((row) => row[key])) + 1;
export function createSeed() {
  return structuredClone({
    version: 1,
    categories,
    products,
    customers,
    computers,
    gamingSessions,
    ...createOrderSeed(),
    inventoryTransactions: [],
  });
}
export function getOrderContext(data, order) {
  const session = data.gamingSessions.find(
    (item) => item.SessionId === order.SessionId,
  );
  return {
    customer:
      data.customers.find((item) => item.CustomerId === order.CustomerId)
        ?.FullName || "Khách hàng",
    computer:
      data.computers.find((item) => item.ComputerId === session?.ComputerId)
        ?.ComputerName || "Không có máy",
  };
}
export function saveProduct(data, input) {
  const name = input.ProductName.trim(),
    price = Number(input.Price),
    stock = Number(input.Stock);
  if (!name || name.length > 150)
    throw new Error("Tên sản phẩm cần từ 1 đến 150 ký tự.");
  if (!Number.isSafeInteger(price) || price <= 0 || price > 9999999999)
    throw new Error("Giá bán phải là số nguyên dương, tối đa 9.999.999.999đ.");
  if (
    input.Stock === "" ||
    !Number.isSafeInteger(stock) ||
    stock < 0 ||
    stock > 2147483647
  )
    throw new Error("Tồn kho phải là số nguyên từ 0 đến 2.147.483.647.");
  if (
    !data.categories.some(
      (item) => item.CategoryId === Number(input.CategoryId) && item.IsActive,
    )
  )
    throw new Error("Vui lòng chọn danh mục đang hoạt động.");
  const existing = data.products.find(
    (item) => item.ProductId === input.ProductId,
  );
  if (input.ProductId && !existing) throw new Error("Không tìm thấy sản phẩm.");
  const product = {
    ProductId: existing?.ProductId || nextId(data.products, "ProductId"),
    CategoryId: Number(input.CategoryId),
    ProductName: name,
    Price: price,
    Stock: stock,
    IsActive: Boolean(input.IsActive),
  };
  const result = structuredClone(data);
  result.products = existing
    ? result.products.map((item) =>
        item.ProductId === product.ProductId ? product : item,
      )
    : [...result.products, product];
  const difference = stock - (existing?.Stock || 0);
  if (difference)
    result.inventoryTransactions.push({
      InvTransId: nextId(result.inventoryTransactions, "InvTransId"),
      ProductId: product.ProductId,
      EmployeeId: 1,
      Type: "Adjust",
      Quantity: difference,
      ReferenceId: null,
      CreatedAt: new Date().toISOString(),
    });
  return result;
}
export function placeOrder(data, cart, customerId, sessionId) {
  if (
    !data.gamingSessions.some(
      (item) =>
        item.SessionId === sessionId &&
        item.CustomerId === customerId &&
        item.Status === "Active",
    )
  )
    throw new Error("Bạn cần có phiên chơi đang hoạt động để gọi món.");
  if (!cart.length) throw new Error("Hãy thêm món vào giỏ trước khi đặt.");
  if (new Set(cart.map((item) => item.ProductId)).size !== cart.length)
    throw new Error("Giỏ hàng có món bị trùng.");
  const result = structuredClone(data),
    id = nextId(data.orders, "OrderId"),
    time = new Date().toISOString();
  let total = 0;
  for (const line of cart) {
    const product = result.products.find(
      (item) => item.ProductId === line.ProductId,
    );
    if (
      !product?.IsActive ||
      !result.categories.some(
        (item) => item.CategoryId === product.CategoryId && item.IsActive,
      )
    )
      throw new Error("Một món trong giỏ đã ngừng bán. Vui lòng kiểm tra lại.");
    if (
      !Number.isSafeInteger(line.Quantity) ||
      line.Quantity < 1 ||
      line.Quantity > product.Stock
    )
      throw new Error(
        `${product.ProductName} chỉ còn ${product.Stock} phần. Vui lòng cập nhật giỏ hàng.`,
      );
    product.Stock -= line.Quantity;
    total += product.Price * line.Quantity;
    result.orderDetails.push({
      OrderDetailId: nextId(result.orderDetails, "OrderDetailId"),
      OrderId: id,
      ProductId: product.ProductId,
      Quantity: line.Quantity,
      UnitPrice: product.Price,
    });
    result.inventoryTransactions.push({
      InvTransId: nextId(result.inventoryTransactions, "InvTransId"),
      ProductId: product.ProductId,
      EmployeeId: null,
      Type: "Sale",
      Quantity: -line.Quantity,
      ReferenceId: id,
      CreatedAt: time,
    });
  }
  result.orders.unshift({
    OrderId: id,
    CustomerId: customerId,
    SessionId: sessionId,
    EmployeeId: null,
    InvoiceId: null,
    OrderTime: time,
    Status: "Pending",
    TotalAmount: total,
  });
  result.orderStatusHistory.push({
    HistoryId: nextId(result.orderStatusHistory, "HistoryId"),
    OrderId: id,
    OldStatus: null,
    NewStatus: "Pending",
    ChangedBy: null,
    ChangedAt: time,
  });
  return result;
}
export function changeOrderStatus(data, orderId, status) {
  const existing = data.orders.find((item) => item.OrderId === orderId);
  if (
    !existing ||
    !Object.hasOwn(statuses, status) ||
    !(
      nextStatus[existing.Status] === status ||
      (status === "Cancelled" &&
        ["Pending", "Preparing"].includes(existing.Status))
    )
  )
    throw new Error(
      "Trạng thái đơn đã thay đổi hoặc không thể chuyển bước này.",
    );
  const result = structuredClone(data),
    order = result.orders.find((item) => item.OrderId === orderId),
    time = new Date().toISOString();
  result.orderStatusHistory.push({
    HistoryId: nextId(result.orderStatusHistory, "HistoryId"),
    OrderId: orderId,
    OldStatus: order.Status,
    NewStatus: status,
    ChangedBy: 1,
    ChangedAt: time,
  });
  order.Status = status;
  order.EmployeeId = 1;
  if (status === "Cancelled")
    result.orderDetails
      .filter((item) => item.OrderId === orderId)
      .forEach((line) => {
        result.products.find(
          (item) => item.ProductId === line.ProductId,
        ).Stock += line.Quantity;
        result.inventoryTransactions.push({
          InvTransId: nextId(result.inventoryTransactions, "InvTransId"),
          ProductId: line.ProductId,
          EmployeeId: 1,
          Type: "Adjust",
          Quantity: line.Quantity,
          ReferenceId: orderId,
          CreatedAt: time,
        });
      });
  return result;
}
