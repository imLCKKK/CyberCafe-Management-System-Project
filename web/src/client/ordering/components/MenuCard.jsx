import React from "react";
import { Plus, Check } from "lucide-react";
import { FoodArt } from "../../../shared/commerce/components";
import { money } from "../../../shared/commerce/model";
import { productVisuals } from "../../../admin/products/mockData";

export default function MenuCard({ product, category, quantity, onAdd }) {
  const soldOut = product.Stock === 0;
  return (
    <article className={`cc-menu-card ${soldOut ? "is-sold-out" : ""}`}>
      <div className="cc-menu-visual">
        <FoodArt product={product} />
        {soldOut ? (
          <span className="cc-food-label">Tạm hết món</span>
        ) : quantity > 0 ? (
          <span className="cc-food-label cc-in-cart">
            <Check size={12} />
            {quantity} trong giỏ
          </span>
        ) : product.Stock <= 5 ? (
          <span className="cc-food-label">Chỉ còn {product.Stock} phần</span>
        ) : null}
      </div>
      <div className="cc-menu-body">
        <span className="cc-menu-category">{category}</span>
        <h3>{product.ProductName}</h3>
        <p>
          {productVisuals[product.ProductId]?.[1] ||
            "Được chuẩn bị và phục vụ ngay tại máy."}
        </p>
        <div className="cc-menu-bottom">
          <strong>{money(product.Price)}</strong>
          <button
            disabled={soldOut || quantity >= product.Stock}
            aria-label={`Thêm ${product.ProductName}`}
            className="cc-add-food"
            onClick={onAdd}
          >
            <Plus size={18} />
          </button>
        </div>
      </div>
    </article>
  );
}
