import React, { useState } from "react";
import { Package, Save } from "lucide-react";
import { Modal } from "../../../shared/commerce/components";
import { commerce } from "../../../shared/commerce/store";

export default function ProductForm({ product, categories, onClose, onSaved }) {
  const [form, setForm] = useState(
    product || {
      ProductName: "",
      CategoryId: categories.find((item) => item.IsActive)?.CategoryId || "",
      Price: "",
      Stock: 0,
      IsActive: true,
    },
  );
  const [error, setError] = useState("");
  const update = (key, value) =>
    setForm((current) => ({ ...current, [key]: value }));
  function submit(event) {
    event.preventDefault();
    try {
      commerce.saveProduct(form);
      onSaved();
    } catch (error) {
      setError(error.message);
    }
  }
  return (
    <Modal
      title={product ? "Chỉnh sửa sản phẩm" : "Thêm sản phẩm mới"}
      onClose={onClose}
    >
      <form onSubmit={submit} className="cc-form">
        <div className="cc-form-intro">
          <Package size={24} />
          <p>Thông tin sản phẩm sẽ được cập nhật trên thực đơn gọi món.</p>
        </div>
        <label>
          Tên sản phẩm <span>*</span>
          <input
            autoFocus
            required
            maxLength={150}
            value={form.ProductName}
            placeholder="Ví dụ: Trà đào cam sả"
            onChange={(event) => update("ProductName", event.target.value)}
          />
        </label>
        <label>
          Danh mục <span>*</span>
          <select
            required
            value={form.CategoryId}
            onChange={(event) => update("CategoryId", event.target.value)}
          >
            {categories
              .filter((item) => item.IsActive)
              .map((item) => (
                <option key={item.CategoryId} value={item.CategoryId}>
                  {item.CategoryName}
                </option>
              ))}
          </select>
        </label>
        <div className="cc-form-grid">
          <label>
            Giá bán (VNĐ) <span>*</span>
            <input
              type="number"
              required
              min="1"
              max="9999999999"
              step="1"
              value={form.Price}
              placeholder="25000"
              onChange={(event) => update("Price", event.target.value)}
            />
          </label>
          <label>
            Tồn kho <span>*</span>
            <input
              type="number"
              required
              min="0"
              max="2147483647"
              step="1"
              value={form.Stock}
              onChange={(event) => update("Stock", event.target.value)}
            />
          </label>
        </div>
        <label className="cc-checkbox">
          <input
            type="checkbox"
            checked={form.IsActive}
            onChange={(event) => update("IsActive", event.target.checked)}
          />
          <div>
            Đang kinh doanh
            <small>Hiển thị sản phẩm trong thực đơn của khách.</small>
          </div>
        </label>
        {error && (
          <p className="cc-form-error" role="alert">
            {error}
          </p>
        )}
        <div className="cc-modal-actions">
          <button type="button" className="cc-button" onClick={onClose}>
            Hủy
          </button>
          <button type="submit" className="cc-button cc-primary">
            <Save size={16} />
            {product ? "Lưu thay đổi" : "Thêm sản phẩm"}
          </button>
        </div>
      </form>
    </Modal>
  );
}
