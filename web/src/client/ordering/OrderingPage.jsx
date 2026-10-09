import React, { useRef, useState } from "react";
import {
  UtensilsCrossed,
  Coffee,
  Cookie,
  LayoutGrid,
  Clock3,
  Monitor,
  ArrowRight,
  Zap,
  ChefHat,
} from "lucide-react";
import { useCommerce, commerce } from "../../shared/commerce/store";
import { fold, money, getOrderContext } from "../../shared/commerce/model";
import {
  SearchBox,
  EmptyState,
  Notice,
  StatusBadge,
} from "../../shared/commerce/components";
import { currentCustomerId, currentSessionId } from "./mockData";
import OrderDetail from "../../admin/orders/components/OrderDetail";
import Cart from "./components/Cart";
import MenuCard from "./components/MenuCard";
import FoodCase, { FoodCaseButton } from "./components/FoodCase";
import { getCaseChoices, resolveCaseSelection } from "./foodCase";
import "../../shared/commerce/commerce.css";
import "./ordering.css";

export function OrderingPage() {
  const data = useCommerce();
  const [query, setQuery] = useState(""),
    [category, setCategory] = useState("all"),
    [sort, setSort] = useState("default"),
    [cart, setCart] = useState([]),
    [tab, setTab] = useState("menu"),
    [notice, setNotice] = useState(null),
    [selected, setSelected] = useState(null),
    [busy, setBusy] = useState(false),
    [caseOpen, setCaseOpen] = useState(false);
  const submitting = useRef(false);
  const cartRef = useRef(null);
  const caseChoices = getCaseChoices(data.products, data.categories, cart);
  function resolveFoodCase(selected, filters) {
    const result = resolveCaseSelection(
      data.products,
      data.categories,
      cart,
      selected,
      filters,
    );
    if (result.ok) setCart(result.cart);
    return result;
  }
  const session = data.gamingSessions.find(
    (item) => item.SessionId === currentSessionId,
  );
  const machine =
    data.computers.find((item) => item.ComputerId === session?.ComputerId)
      ?.ComputerName || "máy hiện tại";
  const available = data.products.filter(
    (item) =>
      item.IsActive &&
      data.categories.some(
        (cat) => cat.CategoryId === item.CategoryId && cat.IsActive,
      ),
  );
  const filtered = available.filter(
    (item) =>
      fold(item.ProductName).includes(fold(query.trim())) &&
      (category === "all" || item.CategoryId === category),
  );
  filtered.sort((a, b) =>
    sort === "price-asc"
      ? a.Price - b.Price
      : sort === "price-desc"
        ? b.Price - a.Price
        : Number(a.Stock === 0) - Number(b.Stock === 0) ||
          a.ProductId - b.ProductId,
  );
  const orders = data.orders
    .filter((item) => item.CustomerId === currentCustomerId)
    .sort((a, b) => new Date(b.OrderTime) - new Date(a.OrderTime));
  const active = orders.filter(
    (item) => !["Delivered", "Cancelled"].includes(item.Status),
  );
  const order = orders.find((item) => item.OrderId === selected);
  function quantity(id, value) {
    const product = data.products.find((item) => item.ProductId === id);
    if (value > 0 && (!product?.IsActive || value > product.Stock)) return;
    setCart((current) =>
      value <= 0
        ? current.filter((line) => line.ProductId !== id)
        : current.some((line) => line.ProductId === id)
          ? current.map((line) =>
              line.ProductId === id ? { ...line, Quantity: value } : line,
            )
          : [...current, { ProductId: id, Quantity: value }],
    );
  }
  function checkout() {
    if (submitting.current) return;
    submitting.current = true;
    setBusy(true);
    try {
      const updated = commerce.placeOrder(
        cart,
        currentCustomerId,
        currentSessionId,
      );
      const order = updated.orders[0];
      setCart([]);
      setNotice({
        text: `Đặt món thành công! Đơn #GM${order.OrderId} sẽ được giao đến ${machine}.`,
      });
      setTab("orders");
    } catch (error) {
      setNotice({ text: error.message, error: true });
    } finally {
      submitting.current = false;
      setBusy(false);
    }
  }
  const categoryIcons = { 1: UtensilsCrossed, 2: Cookie, 3: Coffee };
  return (
    <section className="cc-page cc-ordering">
      <div className="cc-heading">
        <div>
          <div className="cc-eyebrow">TRẠM NẠP NĂNG LƯỢNG</div>
          <h1>Đói rồi? Gọi món thôi.</h1>
          <p>Món ngon đến tận máy, cuộc chơi không gián đoạn.</p>
        </div>
        <div className="cc-serving">
          <Monitor size={17} />
          <span>
            Đang phục vụ tại <strong>{machine}</strong>
          </span>
          <i />
        </div>
      </div>
      <Notice notice={notice} onClose={() => setNotice(null)} />
      <div className="cc-ordering-layout">
        <div className="cc-menu-main">
          <div className="cc-menu-banner">
            <div>
              <span>
                <Zap size={13} />
                NẠP NĂNG LƯỢNG, TIẾP TRẬN
              </span>
              <h2>Ăn ngon. Chơi hết mình.</h2>
              <p>
                Từ món nóng đến đồ uống mát lạnh.
                <br />
                Chọn món yêu thích, chúng mình lo phần còn lại.
              </p>
              <div>
                <Clock3 size={14} />
                Chuẩn bị dự kiến 10–15 phút
              </div>
            </div>
            <div className="cc-banner-art" aria-hidden="true">
              <span>🍜</span>
              <span>🥤</span>
              <i>+ ENERGY</i>
            </div>
          </div>
          <div className="cc-ordering-tabs">
            <button
              className={tab === "menu" ? "active" : ""}
              onClick={() => setTab("menu")}
            >
              <UtensilsCrossed size={16} />
              Thực đơn
            </button>
            <button
              className={tab === "orders" ? "active" : ""}
              onClick={() => setTab("orders")}
            >
              <Clock3 size={16} />
              Đơn của tôi{active.length > 0 && <span>{active.length}</span>}
            </button>
          </div>
          {tab === "menu" ? (
            <>
              <div className="cc-menu-filters">
                <SearchBox
                  value={query}
                  onChange={setQuery}
                  placeholder="Hôm nay bạn muốn ăn gì?"
                />
                <select
                  aria-label="Sắp xếp thực đơn"
                  value={sort}
                  onChange={(event) => setSort(event.target.value)}
                >
                  <option value="default">Mặc định</option>
                  <option value="price-asc">Giá thấp đến cao</option>
                  <option value="price-desc">Giá cao đến thấp</option>
                </select>
              </div>
              <div className="cc-category-tabs">
                <button
                  className={category === "all" ? "active" : ""}
                  onClick={() => setCategory("all")}
                >
                  <LayoutGrid size={16} />
                  Tất cả<span>{available.length}</span>
                </button>
                {data.categories
                  .filter((item) => item.IsActive)
                  .map((item) => {
                    const Icon =
                      categoryIcons[item.CategoryId] || UtensilsCrossed;
                    return (
                      <button
                        key={item.CategoryId}
                        className={category === item.CategoryId ? "active" : ""}
                        onClick={() => setCategory(item.CategoryId)}
                      >
                        <Icon size={16} />
                        {item.CategoryName}
                        <span>
                          {
                            available.filter(
                              (product) =>
                                product.CategoryId === item.CategoryId,
                            ).length
                          }
                        </span>
                      </button>
                    );
                  })}
              </div>
              <div className="cc-menu-title">
                <h2>
                  {category === "all"
                    ? "Thực đơn hôm nay"
                    : data.categories.find(
                        (item) => item.CategoryId === category,
                      )?.CategoryName}
                </h2>
                <span>{filtered.length} món dành cho bạn</span>
              </div>
              {filtered.length ? (
                <div className="cc-menu-grid">
                  {filtered.map((product) => {
                    const count =
                      cart.find((line) => line.ProductId === product.ProductId)
                        ?.Quantity || 0;
                    return (
                      <MenuCard
                        key={product.ProductId}
                        product={product}
                        category={
                          data.categories.find(
                            (item) => item.CategoryId === product.CategoryId,
                          )?.CategoryName
                        }
                        quantity={count}
                        onAdd={() => quantity(product.ProductId, count + 1)}
                      />
                    );
                  })}
                </div>
              ) : (
                <EmptyState
                  title="Chưa tìm thấy món bạn muốn"
                  description="Thử một từ khóa hoặc danh mục khác nhé."
                  action={
                    <button
                      className="cc-button"
                      onClick={() => {
                        setQuery("");
                        setCategory("all");
                      }}
                    >
                      Xem toàn bộ thực đơn
                    </button>
                  }
                />
              )}
            </>
          ) : (
            <div className="cc-my-orders">
              <div className="cc-menu-title">
                <h2>Đơn gọi món của bạn</h2>
                <span>{orders.length} đơn hàng</span>
              </div>
              {orders.length ? (
                orders.map((item) => (
                  <button
                    className="cc-my-order"
                    key={item.OrderId}
                    onClick={() => setSelected(item.OrderId)}
                  >
                    <div className="cc-my-order-top">
                      <strong>#GM{item.OrderId}</strong>
                      <StatusBadge status={item.Status} />
                    </div>
                    <p>
                      {data.orderDetails
                        .filter((line) => line.OrderId === item.OrderId)
                        .map(
                          (line) =>
                            `${line.Quantity}× ${data.products.find((product) => product.ProductId === line.ProductId)?.ProductName}`,
                        )
                        .join(" · ")}
                    </p>
                    <div className="cc-my-order-bottom">
                      <span>
                        {getOrderContext(data, item).computer} ·{" "}
                        {new Date(item.OrderTime).toLocaleString("vi-VN", {
                          hour: "2-digit",
                          minute: "2-digit",
                          day: "2-digit",
                          month: "2-digit",
                        })}
                      </span>
                      <strong>
                        {money(item.TotalAmount)}
                        <ArrowRight size={15} />
                      </strong>
                    </div>
                  </button>
                ))
              ) : (
                <EmptyState
                  title="Bạn chưa có đơn gọi món"
                  description="Chọn món đầu tiên để nạp thêm năng lượng."
                  action={
                    <button
                      className="cc-button cc-lime"
                      onClick={() => setTab("menu")}
                    >
                      Khám phá thực đơn
                    </button>
                  }
                />
              )}
            </div>
          )}
          <p className="cc-footnote">
            Dữ liệu mẫu • Đơn hàng được lưu trên trình duyệt này.
          </p>
        </div>
        <div className="cc-cart-column" ref={cartRef}>
          <Cart
            cart={cart}
            products={data.products}
            machine={machine}
            onQuantity={quantity}
            onClear={() => setCart([])}
            onOrder={checkout}
            busy={busy}
          />
          <div className="cc-ordering-tip">
            <ChefHat size={22} />
            <div>
              <strong>Nóng hổi, ngay tại máy</strong>
              <p>Nhân viên sẽ mang món đến chỗ bạn sau khi chuẩn bị xong.</p>
            </div>
          </div>
        </div>
      </div>
      <FoodCaseButton
        disabled={!caseChoices.length}
        onClick={() => setCaseOpen(true)}
      />
      {caseOpen && (
        <FoodCase
          choices={caseChoices}
          categories={data.categories}
          onResolve={resolveFoodCase}
          onClose={() => setCaseOpen(false)}
          onViewCart={() => {
            setCaseOpen(false);
            cartRef.current?.scrollIntoView({
              behavior: window.matchMedia("(prefers-reduced-motion: reduce)")
                .matches
                ? "instant"
                : "smooth",
              block: "center",
            });
          }}
        />
      )}
      {order && (
        <OrderDetail
          order={order}
          data={data}
          readOnly
          onClose={() => setSelected(null)}
        />
      )}
    </section>
  );
}
export default OrderingPage;
