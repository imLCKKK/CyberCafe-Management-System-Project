import React, { useEffect, useId, useRef } from "react";
import {
  ChevronLeft,
  ChevronRight,
  Search,
  X,
  PackageOpen,
} from "lucide-react";
import { statuses } from "./model";
import { productVisuals } from "../../admin/products/mockData";

export function StatusBadge({ status }) {
  return (
    <span className={`cc-badge cc-status-${status}`}>
      <i />
      {statuses[status] || status}
    </span>
  );
}
export function FoodArt({ product, small = false }) {
  const emoji =
    productVisuals[product.ProductId]?.[0] ||
    { 1: "🍲", 2: "🍿", 3: "🥤" }[product.CategoryId] ||
    "🍽️";
  return (
    <div
      aria-hidden="true"
      className={`cc-food-art cc-food-${product.CategoryId} ${small ? "cc-food-small" : ""}`}
    >
      <span>{emoji}</span>
    </div>
  );
}
export function SearchBox({ value, onChange, placeholder }) {
  return (
    <div className="cc-search">
      <Search size={17} />
      <input
        aria-label={placeholder}
        placeholder={placeholder}
        value={value}
        onChange={(event) => onChange(event.target.value)}
      />
      {value && (
        <button
          className="cc-icon-button"
          aria-label="Xóa tìm kiếm"
          onClick={() => onChange("")}
        >
          <X size={15} />
        </button>
      )}
    </div>
  );
}
export function EmptyState({
  title = "Không tìm thấy kết quả",
  description = "Thử thay đổi từ khóa hoặc bộ lọc.",
  action,
}) {
  return (
    <div className="cc-empty">
      <PackageOpen size={36} strokeWidth={1.4} />
      <h3>{title}</h3>
      <p>{description}</p>
      {action}
    </div>
  );
}
export function Notice({ notice, onClose }) {
  if (!notice) return null;
  return (
    <div
      className={`cc-notice ${notice.error ? "cc-notice-error" : ""}`}
      role={notice.error ? "alert" : "status"}
    >
      <span>{notice.text}</span>
      <button
        aria-label="Đóng thông báo"
        className="cc-icon-button"
        onClick={onClose}
      >
        <X size={16} />
      </button>
    </div>
  );
}
export function Pagination({ page, count, pageSize, onChange }) {
  const totalPages = Math.max(1, Math.ceil(count / pageSize));
  return (
    <div className="cc-pagination">
      <span>
        {count
          ? `${(page - 1) * pageSize + 1}–${Math.min(page * pageSize, count)} trong ${count} kết quả`
          : "0 kết quả"}
      </span>
      <div>
        <button
          className="cc-icon-button"
          aria-label="Trang trước"
          disabled={page <= 1}
          onClick={() => onChange(page - 1)}
        >
          <ChevronLeft size={17} />
        </button>
        <span>
          Trang {page} / {totalPages}
        </span>
        <button
          className="cc-icon-button"
          aria-label="Trang sau"
          disabled={page >= totalPages}
          onClick={() => onChange(page + 1)}
        >
          <ChevronRight size={17} />
        </button>
      </div>
    </div>
  );
}
export function Modal({ title, onClose, children, wide = false }) {
  const ref = useRef(null);
  const id = useId();
  useEffect(() => {
    const previous = document.activeElement;
    const dialog = ref.current;
    dialog.showModal();
    return () => {
      dialog.close();
      previous?.focus();
    };
  }, []);
  return (
    <dialog
      ref={ref}
      className={`cc-modal ${wide ? "cc-modal-wide" : ""}`}
      aria-labelledby={id}
      onCancel={(event) => {
        event.preventDefault();
        onClose();
      }}
      onClick={(event) => {
        if (event.target === ref.current) {
          const box = ref.current.getBoundingClientRect();
          if (
            event.clientX < box.left ||
            event.clientX > box.right ||
            event.clientY < box.top ||
            event.clientY > box.bottom
          )
            onClose();
        }
      }}
    >
      <div className="cc-modal-heading">
        <h2 id={id}>{title}</h2>
        <button
          className="cc-icon-button"
          aria-label="Đóng hộp thoại"
          onClick={onClose}
        >
          <X size={20} />
        </button>
      </div>
      {children}
    </dialog>
  );
}
