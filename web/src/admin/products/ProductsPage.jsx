import React, { useState } from "react";
import {
  Plus,
  Package,
  CircleCheck,
  TriangleAlert,
  CirclePause,
  Pencil,
  SlidersHorizontal,
} from "lucide-react";
import { useCommerce, commerce } from "../../shared/commerce/store";
import { fold, money } from "../../shared/commerce/model";
import {
  SearchBox,
  FoodArt,
  EmptyState,
  Notice,
  Pagination,
} from "../../shared/commerce/components";
import ProductForm from "./components/ProductForm";
import "../../shared/commerce/commerce.css";

export function ProductsPage() {
  const data = useCommerce();
  const [query, setQuery] = useState(""),
    [category, setCategory] = useState("all"),
    [status, setStatus] = useState("all"),
    [sort, setSort] = useState("id");
  const [page, setPage] = useState(1),
    [editing, setEditing] = useState(null),
    [notice, setNotice] = useState(null);
  const filtered = data.products.filter(
    (item) =>
      fold(
        `${item.ProductName} SP${String(item.ProductId).padStart(3, "0")}`,
      ).includes(fold(query.trim())) &&
      (category === "all" || item.CategoryId === Number(category)) &&
      (status === "all" ||
        (status === "active" && item.IsActive && item.Stock > 0) ||
        (status === "inactive" && !item.IsActive) ||
        (status === "low" && item.IsActive && item.Stock <= 5)),
  );
  filtered.sort((a, b) =>
    sort === "price-asc"
      ? a.Price - b.Price
      : sort === "price-desc"
        ? b.Price - a.Price
        : sort === "stock"
          ? a.Stock - b.Stock
          : a.ProductId - b.ProductId,
  );
  const currentPage = Math.min(
    page,
    Math.max(1, Math.ceil(filtered.length / 8)),
  );
  function filter(setter, value) {
    setter(value);
    setPage(1);
  }
  function toggle(product) {
    try {
      commerce.toggleProduct(product.ProductId);
      setNotice({
        text: `${product.IsActive ? "Đã ngừng bán" : "Đã mở bán"} ${product.ProductName}.`,
      });
    } catch (error) {
      setNotice({ text: error.message, error: true });
    }
  }
  return (
    <section className="cc-page cc-admin">
      <div className="cc-heading">
        <div>
          <div className="cc-eyebrow">THỰC ĐƠN & DỊCH VỤ</div>
          <h1>Quản lý sản phẩm</h1>
          <p>Chăm chút thực đơn, tiếp năng lượng cho mọi cuộc chơi.</p>
        </div>
        <button
          className="cc-button cc-primary"
          onClick={() => setEditing("new")}
        >
          <Plus size={18} />
          Thêm sản phẩm
        </button>
      </div>
      <Notice notice={notice} onClose={() => setNotice(null)} />
      <div className="cc-stats">
        {[
          [
            Package,
            "Tổng sản phẩm",
            data.products.length,
            "Trong thực đơn",
            "blue",
          ],
          [
            CircleCheck,
            "Đang kinh doanh",
            data.products.filter((item) => item.IsActive).length,
            "Được hiển thị cho khách",
            "green",
          ],
          [
            TriangleAlert,
            "Sắp hết / hết hàng",
            data.products.filter((item) => item.IsActive && item.Stock <= 5)
              .length,
            "Tồn kho từ 5 phần trở xuống",
            "orange",
          ],
          [
            CirclePause,
            "Ngừng kinh doanh",
            data.products.filter((item) => !item.IsActive).length,
            "Tạm ẩn khỏi thực đơn",
            "gray",
          ],
        ].map(([Icon, label, value, hint, color]) => (
          <div className="cc-stat" key={label}>
            <div>
              <span>{label}</span>
              <Icon size={19} className={`cc-color-${color}`} />
            </div>
            <strong>{value.toString().padStart(2, "0")}</strong>
            <small>{hint}</small>
          </div>
        ))}
      </div>
      <div className="cc-panel">
        <div className="cc-panel-title">
          <div>
            <h2>
              Danh sách sản phẩm{" "}
              <span className="cc-count">{data.products.length}</span>
            </h2>
            <p>Quản lý giá bán, tồn kho và trạng thái kinh doanh.</p>
          </div>
          <SlidersHorizontal size={19} />
        </div>
        <div className="cc-toolbar">
          <SearchBox
            value={query}
            onChange={(value) => filter(setQuery, value)}
            placeholder="Tìm tên hoặc mã sản phẩm..."
          />
          <select
            aria-label="Lọc danh mục"
            value={category}
            onChange={(event) => filter(setCategory, event.target.value)}
          >
            <option value="all">Tất cả danh mục</option>
            {data.categories.map((item) => (
              <option key={item.CategoryId} value={item.CategoryId}>
                {item.CategoryName}
              </option>
            ))}
          </select>
          <select
            aria-label="Lọc trạng thái sản phẩm"
            value={status}
            onChange={(event) => filter(setStatus, event.target.value)}
          >
            <option value="all">Tất cả trạng thái</option>
            <option value="active">Còn hàng, đang bán</option>
            <option value="low">Sắp hết / hết hàng</option>
            <option value="inactive">Ngừng kinh doanh</option>
          </select>
          <select
            aria-label="Sắp xếp sản phẩm"
            value={sort}
            onChange={(event) => filter(setSort, event.target.value)}
          >
            <option value="id">Mã sản phẩm</option>
            <option value="price-asc">Giá tăng dần</option>
            <option value="price-desc">Giá giảm dần</option>
            <option value="stock">Tồn kho tăng dần</option>
          </select>
        </div>
        {filtered.length ? (
          <div className="cc-table-wrap">
            <table className="cc-table">
              <thead>
                <tr>
                  <th>Sản phẩm</th>
                  <th>Danh mục</th>
                  <th>Giá bán</th>
                  <th>Tồn kho</th>
                  <th>Trạng thái</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {filtered
                  .slice((currentPage - 1) * 8, currentPage * 8)
                  .map((product) => (
                    <tr key={product.ProductId}>
                      <td>
                        <div className="cc-product-cell">
                          <FoodArt product={product} small />
                          <div>
                            <strong>{product.ProductName}</strong>
                            <small>
                              SP{String(product.ProductId).padStart(3, "0")}
                            </small>
                          </div>
                        </div>
                      </td>
                      <td>
                        {
                          data.categories.find(
                            (item) => item.CategoryId === product.CategoryId,
                          )?.CategoryName
                        }
                      </td>
                      <td className="cc-nowrap">
                        <strong>{money(product.Price)}</strong>
                      </td>
                      <td>
                        <span
                          className={product.Stock <= 5 ? "cc-stock-low" : ""}
                        >
                          {product.Stock === 0
                            ? "Hết hàng"
                            : `${product.Stock} phần`}
                          {product.Stock > 0 && product.Stock <= 5 && (
                            <TriangleAlert size={13} />
                          )}
                        </span>
                      </td>
                      <td>
                        <span
                          className={`cc-badge ${product.IsActive ? "cc-status-Delivered" : "cc-status-Cancelled"}`}
                        >
                          <i />
                          {product.IsActive ? "Đang bán" : "Ngừng bán"}
                        </span>
                      </td>
                      <td>
                        <div className="cc-row-actions">
                          <button
                            className="cc-icon-button"
                            aria-label={`Sửa ${product.ProductName}`}
                            title="Chỉnh sửa"
                            onClick={() => setEditing(product)}
                          >
                            <Pencil size={16} />
                          </button>
                          <button
                            className={`cc-switch ${product.IsActive ? "is-on" : ""}`}
                            role="switch"
                            aria-checked={product.IsActive}
                            aria-label={`Kinh doanh ${product.ProductName}`}
                            onClick={() => toggle(product)}
                          >
                            <span />
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
              </tbody>
            </table>
          </div>
        ) : (
          <EmptyState
            action={
              <button
                className="cc-button"
                onClick={() => {
                  setQuery("");
                  setCategory("all");
                  setStatus("all");
                }}
              >
                Xóa bộ lọc
              </button>
            }
          />
        )}
        <Pagination
          page={currentPage}
          count={filtered.length}
          pageSize={8}
          onChange={setPage}
        />
      </div>
      <p className="cc-footnote">
        Dữ liệu mẫu • Thay đổi được lưu trên trình duyệt này.
      </p>
      {editing && (
        <ProductForm
          product={editing === "new" ? null : editing}
          categories={data.categories}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setNotice({
              text:
                editing === "new"
                  ? "Đã thêm sản phẩm vào thực đơn."
                  : "Đã cập nhật thông tin sản phẩm.",
            });
            setEditing(null);
          }}
        />
      )}
    </section>
  );
}
export default ProductsPage;
