import React, { useState } from "react";
import {
  ShoppingBag,
  Clock3,
  ChefHat,
  CircleCheck,
  Monitor,
  ArrowUpRight,
  ArrowRight,
  UtensilsCrossed,
} from "lucide-react";
import { Link } from "react-router-dom";
import { useCommerce, commerce } from "../../shared/commerce/store";
import {
  statuses,
  nextStatus,
  nextAction,
  getOrderContext,
  fold,
  money,
} from "../../shared/commerce/model";
import {
  SearchBox,
  StatusBadge,
  EmptyState,
  Notice,
  Pagination,
} from "../../shared/commerce/components";
import OrderDetail from "./components/OrderDetail";
import "../../shared/commerce/commerce.css";

export function OrdersPage() {
  const data = useCommerce();
  const [query, setQuery] = useState(""),
    [status, setStatus] = useState("all"),
    [period, setPeriod] = useState("today"),
    [page, setPage] = useState(1),
    [selected, setSelected] = useState(null),
    [notice, setNotice] = useState(null);
  const today = new Date().toDateString();
  const todayOrders = data.orders.filter(
    (order) => new Date(order.OrderTime).toDateString() === today,
  );
  const periodOrders = period === "today" ? todayOrders : data.orders;
  const filtered = periodOrders
    .filter((order) => {
      const context = getOrderContext(data, order);
      return (
        (status === "all" || order.Status === status) &&
        fold(
          `GM${order.OrderId} ${context.customer} ${context.computer}`,
        ).includes(fold(query.trim()))
      );
    })
    .sort((a, b) => new Date(b.OrderTime) - new Date(a.OrderTime));
  const currentPage = Math.min(
    page,
    Math.max(1, Math.ceil(filtered.length / 7)),
  );
  const order = data.orders.find((item) => item.OrderId === selected);
  function advance(item) {
    try {
      commerce.changeOrderStatus(item.OrderId, nextStatus[item.Status]);
      setNotice({
        text: `Đơn #GM${item.OrderId}: ${statuses[nextStatus[item.Status]]}.`,
      });
    } catch (error) {
      setNotice({ text: error.message, error: true });
    }
  }
  return (
    <section className="cc-page cc-admin">
      <div className="cc-heading">
        <div>
          <div className="cc-eyebrow">PHỤC VỤ TẠI MÁY</div>
          <h1>Quản lý đơn gọi món</h1>
          <p>Từ căn bếp đến bàn chơi, theo dõi từng đơn hàng.</p>
        </div>
        <Link to="/admin/products" className="cc-button">
          <UtensilsCrossed size={16} />
          Quản lý thực đơn
          <ArrowUpRight size={15} />
        </Link>
      </div>
      <Notice notice={notice} onClose={() => setNotice(null)} />
      <div className="cc-stats">
        {[
          [
            ShoppingBag,
            "Đơn hôm nay",
            todayOrders.length,
            "Tất cả trạng thái",
            "blue",
          ],
          [
            Clock3,
            "Chờ xác nhận",
            data.orders.filter((item) => item.Status === "Pending").length,
            "Cần tiếp nhận từ khách",
            "orange",
          ],
          [
            ChefHat,
            "Đang phục vụ",
            data.orders.filter((item) =>
              ["Preparing", "Ready"].includes(item.Status),
            ).length,
            "Đang chuẩn bị & sẵn sàng giao",
            "blue",
          ],
          [
            CircleCheck,
            "Đã giao hôm nay",
            todayOrders.filter((item) => item.Status === "Delivered").length,
            "Đã mang món đến máy",
            "green",
          ],
        ].map(([Icon, label, value, hint, color]) => (
          <div className="cc-stat" key={label}>
            <div>
              <span>{label}</span>
              <Icon size={19} className={`cc-color-${color}`} />
            </div>
            <strong>{String(value).padStart(2, "0")}</strong>
            <small>{hint}</small>
          </div>
        ))}
      </div>
      <div className="cc-panel">
        <div className="cc-panel-title">
          <div>
            <h2>Đơn gọi món</h2>
            <p>Tiếp nhận, chuẩn bị và giao món ngay tại máy.</p>
          </div>
          <span className="cc-live">
            <i />
            Đồng bộ nội bộ
          </span>
        </div>
        <div className="cc-tabs" role="group" aria-label="Lọc trạng thái đơn">
          <button
            className={status === "all" ? "active" : ""}
            onClick={() => {
              setStatus("all");
              setPage(1);
            }}
          >
            Tất cả <span>{periodOrders.length}</span>
          </button>
          {Object.entries(statuses).map(([key, label]) => (
            <button
              key={key}
              className={status === key ? "active" : ""}
              onClick={() => {
                setStatus(key);
                setPage(1);
              }}
            >
              {label}
              <span>
                {periodOrders.filter((item) => item.Status === key).length}
              </span>
            </button>
          ))}
        </div>
        <div className="cc-toolbar">
          <SearchBox
            value={query}
            onChange={(value) => {
              setQuery(value);
              setPage(1);
            }}
            placeholder="Tìm mã đơn, khách hàng, máy..."
          />
          <select
            aria-label="Thời gian đơn hàng"
            value={period}
            onChange={(event) => {
              setPeriod(event.target.value);
              setPage(1);
            }}
          >
            <option value="today">Hôm nay</option>
            <option value="all">Tất cả thời gian</option>
          </select>
          <span className="cc-muted">{filtered.length} đơn hàng</span>
        </div>
        {filtered.length ? (
          <div className="cc-table-wrap">
            <table className="cc-table cc-orders-table">
              <thead>
                <tr>
                  <th>Đơn hàng</th>
                  <th>Máy / khách hàng</th>
                  <th>Món đã gọi</th>
                  <th>Tổng tiền</th>
                  <th>Trạng thái</th>
                  <th>Thao tác</th>
                </tr>
              </thead>
              <tbody>
                {filtered
                  .slice((currentPage - 1) * 7, currentPage * 7)
                  .map((item) => {
                    const context = getOrderContext(data, item),
                      lines = data.orderDetails.filter(
                        (line) => line.OrderId === item.OrderId,
                      );
                    return (
                      <tr key={item.OrderId}>
                        <td>
                          <button
                            className="cc-text-button"
                            onClick={() => setSelected(item.OrderId)}
                          >
                            #GM{item.OrderId}
                          </button>
                          <small>
                            {new Date(item.OrderTime).toLocaleTimeString(
                              "vi-VN",
                              { hour: "2-digit", minute: "2-digit" },
                            )}{" "}
                            ·{" "}
                            {new Date(item.OrderTime).toLocaleDateString(
                              "vi-VN",
                              { day: "2-digit", month: "2-digit" },
                            )}
                          </small>
                        </td>
                        <td>
                          <span className="cc-machine">
                            <Monitor size={14} />
                            {context.computer}
                          </span>
                          <small>{context.customer}</small>
                        </td>
                        <td>
                          <div className="cc-order-lines">
                            {lines.map((line) => (
                              <div key={line.OrderDetailId}>
                                <b>{line.Quantity}×</b>{" "}
                                {
                                  data.products.find(
                                    (product) =>
                                      product.ProductId === line.ProductId,
                                  )?.ProductName
                                }
                              </div>
                            ))}
                          </div>
                        </td>
                        <td className="cc-nowrap">
                          <strong>{money(item.TotalAmount)}</strong>
                        </td>
                        <td>
                          <StatusBadge status={item.Status} />
                        </td>
                        <td>
                          {nextStatus[item.Status] ? (
                            <button
                              className="cc-button cc-small cc-soft"
                              onClick={() => advance(item)}
                            >
                              {nextAction[item.Status]}
                              <ArrowRight size={13} />
                            </button>
                          ) : (
                            <button
                              className="cc-text-button"
                              onClick={() => setSelected(item.OrderId)}
                            >
                              Xem chi tiết
                              <ArrowUpRight size={14} />
                            </button>
                          )}
                        </td>
                      </tr>
                    );
                  })}
              </tbody>
            </table>
          </div>
        ) : (
          <EmptyState
            title="Chưa có đơn hàng phù hợp"
            description="Các đơn mới từ khách sẽ xuất hiện tại đây."
            action={
              <button
                className="cc-button"
                onClick={() => {
                  setQuery("");
                  setStatus("all");
                  setPeriod("all");
                }}
              >
                Xem tất cả đơn
              </button>
            }
          />
        )}
        <Pagination
          page={currentPage}
          count={filtered.length}
          pageSize={7}
          onChange={setPage}
        />
      </div>
      <div className="cc-info-strip">
        <ChefHat size={19} />
        <span>Quy trình phục vụ</span>
        <b>Chờ xác nhận</b>
        <ArrowRight size={14} />
        <b>Đang chuẩn bị</b>
        <ArrowRight size={14} />
        <b>Sẵn sàng giao</b>
        <ArrowRight size={14} />
        <b>Đã giao</b>
      </div>
      <p className="cc-footnote">
        Dữ liệu mẫu • Thay đổi được lưu trên trình duyệt này.
      </p>
      {order && (
        <OrderDetail
          order={order}
          data={data}
          onClose={() => setSelected(null)}
          onUpdated={(status) =>
            setNotice({
              text: `Đã cập nhật đơn #GM${order.OrderId}: ${statuses[status]}.`,
            })
          }
        />
      )}
    </section>
  );
}
export default OrdersPage;
