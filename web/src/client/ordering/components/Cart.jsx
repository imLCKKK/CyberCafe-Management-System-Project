import React from "react";
import {
  ShoppingBag,
  Monitor,
  Minus,
  Plus,
  Trash2,
  ArrowRight,
  ReceiptText,
} from "lucide-react";
import { FoodArt } from "../../../shared/commerce/components";
import { money } from "../../../shared/commerce/model";

export default function Cart({
  cart,
  products,
  machine,
  onQuantity,
  onClear,
  onOrder,
  busy,
}) {
  const count = cart.reduce((sum, line) => sum + line.Quantity, 0);
  const total = cart.reduce(
    (sum, line) =>
      sum +
      (products.find((item) => item.ProductId === line.ProductId)?.Price || 0) *
        line.Quantity,
    0,
  );
  const invalid = cart.some((line) => {
    const product = products.find((item) => item.ProductId === line.ProductId);
    return !product?.IsActive || product.Stock < line.Quantity;
  });
  return (
    <aside className="cc-cart">
      <div className="cc-cart-heading">
        <h2>
          <ShoppingBag size={19} />
          Giỏ hàng của bạn <span>{count}</span>
        </h2>
        {cart.length > 0 && (
          <button
            aria-label="Xóa toàn bộ giỏ hàng"
            title="Xóa giỏ hàng"
            className="cc-icon-button"
            onClick={onClear}
          >
            <Trash2 size={16} />
          </button>
        )}
      </div>
      <div className="cc-cart-machine">
        <Monitor size={18} />
        <div>
          <strong>Giao đến {machine}</strong>
          <small>Phục vụ ngay tại chỗ ngồi của bạn</small>
        </div>
        <span className="cc-dot" />
      </div>
      {cart.length ? (
        <div className="cc-cart-lines">
          {cart.map((line) => {
            const product = products.find(
              (item) => item.ProductId === line.ProductId,
            );
            if (!product) return null;
            return (
              <div className="cc-cart-line" key={line.ProductId}>
                <FoodArt product={product} small />
                <div className="cc-cart-line-body">
                  <strong>{product.ProductName}</strong>
                  <small>{money(product.Price)}</small>
                  <div className="cc-cart-line-bottom">
                    <div className="cc-quantity">
                      <button
                        aria-label={`Giảm ${product.ProductName}`}
                        onClick={() =>
                          onQuantity(product.ProductId, line.Quantity - 1)
                        }
                      >
                        <Minus size={13} />
                      </button>
                      <span>{line.Quantity}</span>
                      <button
                        aria-label={`Tăng ${product.ProductName}`}
                        disabled={
                          !product.IsActive || line.Quantity >= product.Stock
                        }
                        onClick={() =>
                          onQuantity(product.ProductId, line.Quantity + 1)
                        }
                      >
                        <Plus size={13} />
                      </button>
                    </div>
                    <button
                      className="cc-icon-button"
                      aria-label={`Xóa ${product.ProductName} khỏi giỏ`}
                      onClick={() => onQuantity(product.ProductId, 0)}
                    >
                      <Trash2 size={14} />
                    </button>
                  </div>
                  {(!product.IsActive || product.Stock < line.Quantity) && (
                    <small className="cc-form-error">
                      {!product.IsActive
                        ? "Món đã ngừng bán"
                        : `Chỉ còn ${product.Stock} phần`}
                    </small>
                  )}
                </div>
              </div>
            );
          })}
        </div>
      ) : (
        <div className="cc-cart-empty">
          <div>
            <ShoppingBag size={30} strokeWidth={1.3} />
          </div>
          <strong>Chọn món bạn thích</strong>
          <p>
            Thêm chút năng lượng
            <br />
            cho trận đấu tiếp theo.
          </p>
        </div>
      )}
      <div className="cc-cart-summary">
        <div>
          <span>Tạm tính ({count} món)</span>
          <strong>{money(total)}</strong>
        </div>
        <div>
          <span>Phí phục vụ tại máy</span>
          <span className="cc-lime-text">Miễn phí</span>
        </div>
        <div className="cc-cart-total">
          <span>Tổng cộng</span>
          <strong>{money(total)}</strong>
        </div>
        <button
          className="cc-button cc-lime cc-checkout"
          disabled={!cart.length || invalid || busy}
          onClick={onOrder}
        >
          {busy ? "Đang gửi đơn..." : "Đặt món ngay"}
          <ArrowRight size={17} />
        </button>
        <p>
          <ReceiptText size={14} />
          Thanh toán cùng hóa đơn phiên chơi.
        </p>
      </div>
    </aside>
  );
}
