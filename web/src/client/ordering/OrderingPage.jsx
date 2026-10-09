import React from 'react';
import { useLocation } from 'react-router-dom';

export function OrderingPage() {
  const { state } = useLocation();
  // Handoff from Home. The future cart should use ProductId to fetch current
  // prices/stock and consume these initial items once, not on every render.
  const openCart = state?.openCart === true;
  const initialCartItems = Array.isArray(state?.initialCartItems)
    ? state.initialCartItems.filter((item) => item && Number.isInteger(item.ProductId) && item.ProductId > 0 && Number.isInteger(item.Quantity) && item.Quantity > 0 && typeof item.ProductName === 'string')
    : [];

  return <div className="client-page client-page-placeholder">
    <div className="client-empty">
      <h2>Gọi món</h2>
      <p>Menu đồ ăn, giỏ hàng và theo dõi order.</p>
      {openCart && initialCartItems.length > 0 && <p role="status">
        Đã chọn: {initialCartItems.map((item) => `${item.Quantity} × ${item.ProductName}`).join(', ')}.
        {' '}Giỏ hàng sẽ mở tại đây khi giao diện được hoàn thiện.
      </p>}
      <span>KHUNG CHƯA TRIỂN KHAI</span>
    </div>
  </div>;
}
