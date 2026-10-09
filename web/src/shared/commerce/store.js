import { useSyncExternalStore } from "react";
import {
  createSeed,
  saveProduct,
  placeOrder,
  changeOrderStatus,
} from "./model.js";

const key = "cybercafe.commerce.v1";
const listeners = new Set();
let state = createSeed();
const valid = (value) =>
  value?.version === 1 &&
  [
    "products",
    "categories",
    "orders",
    "orderDetails",
    "orderStatusHistory",
    "customers",
    "computers",
    "gamingSessions",
    "inventoryTransactions",
  ].every((name) => Array.isArray(value[name]));
function read() {
  try {
    const stored = JSON.parse(localStorage.getItem(key));
    if (valid(stored)) return stored;
  } catch {
    /* Keep the demo readable when storage is unavailable. */
  }
  return state;
}
state = read();
const publish = () => listeners.forEach((listener) => listener());
window.addEventListener("storage", (event) => {
  if (event.key === key || event.key === null) {
    state = read();
    publish();
  }
});
function commit(transform) {
  const updated = transform(read());
  try {
    localStorage.setItem(key, JSON.stringify(updated));
  } catch {
    throw new Error(
      "Không thể lưu dữ liệu trên trình duyệt. Hãy kiểm tra quyền lưu trữ hoặc dung lượng.",
    );
  }
  state = updated;
  publish();
  return updated;
}
const subscribe = (listener) => {
  listeners.add(listener);
  return () => listeners.delete(listener);
};
export const useCommerce = () => useSyncExternalStore(subscribe, () => state);
export const commerce = {
  saveProduct: (product) => commit((data) => saveProduct(data, product)),
  toggleProduct: (id) =>
    commit((data) => {
      const product = data.products.find((item) => item.ProductId === id);
      if (!product) throw new Error("Không tìm thấy sản phẩm.");
      return saveProduct(data, { ...product, IsActive: !product.IsActive });
    }),
  placeOrder: (cart, customerId, sessionId) =>
    commit((data) => placeOrder(data, cart, customerId, sessionId)),
  changeOrderStatus: (id, status) =>
    commit((data) => changeOrderStatus(data, id, status)),
};
