import React, { useState } from "react";
import { Link } from "react-router-dom";
import {
  ArrowRight,
  ArrowUpRight,
  Monitor,
  RefreshCw,
  ShoppingBag,
  CircleDollarSign,
  ChartNoAxesCombined,
} from "lucide-react";
import { useComputers } from "../computers/computerStore";
import { currency } from "../computers/mockData";
import { PageHeading, StatCard } from "../computers/components/CafeUI";
import { recentOrders, revenue, foodAlertIds } from "./mockData";
import "../computers/cafe.css";

export function DashboardPage() {
  const computers = useComputers();
  const [updatedAt, setUpdatedAt] = useState(null);
  const [showAll, setShowAll] = useState(false);
  const occupied = computers.filter((pc) => pc.Status === "Occupied").length;
  const available = computers.filter((pc) => pc.Status === "Available").length;
  const maintenance = computers.length - occupied - available;
  const occupancy = computers.length
    ? Math.round((occupied / computers.length) * 100)
    : 0;
  const pending = recentOrders.filter(
    (order) => order.Status === "Pending",
  ).length;
  const alerts = computers.filter((pc) =>
    foodAlertIds.has(pc.ComputerId),
  ).length;
  return (
    <section className="cafe-page cafe-dashboard">
      <PageHeading
        eyebrow={new Intl.DateTimeFormat("vi-VN", {
          weekday: "long",
          day: "numeric",
          month: "long",
          year: "numeric",
        }).format(new Date())}
        title="Xin chào, Admin"
        description="Cùng xem hoạt động tại quán của bạn hôm nay."
      >
        <span className="cafe-system">
          <i />
          Chế độ dữ liệu mẫu
        </span>
      </PageHeading>
      <div className="cafe-stats">
        <StatCard
          label="Doanh thu hôm nay"
          value={currency(revenue.Today)}
          icon={CircleDollarSign}
        >
          <span className="cafe-positive">
            <ArrowUpRight size={14} /> +
            {Math.round((revenue.Today / revenue.Yesterday - 1) * 100)}% so với
            hôm qua
          </span>
        </StatCard>
        <StatCard
          label="Máy đang hoạt động"
          value={occupied}
          suffix={`/${computers.length}`}
          icon={Monitor}
          progress={occupancy}
        />
        <StatCard
          label="Tỷ lệ sử dụng"
          value={`${occupancy}%`}
          icon={ChartNoAxesCombined}
          progress={occupancy}
        />
        <StatCard label="Đơn món đang chờ" value={pending} icon={ShoppingBag}>
          <span className="cafe-attention">Cần xử lý</span>
        </StatCard>
      </div>
      <div className="cafe-dashboard-grid">
        <article className="cafe-panel cafe-map-panel">
          <div className="cafe-panel-heading">
            <div>
              <h2>Sơ đồ máy tính</h2>
              <p>
                Trạng thái hiện tại <span>·</span> {computers.length} máy{" "}
                <span>·</span>{" "}
                <span aria-live="polite">
                  {updatedAt ? `Cập nhật ${updatedAt}` : "Vừa cập nhật"}
                </span>
              </p>
            </div>
            <button
              className="cafe-button"
              onClick={() =>
                setUpdatedAt(new Date().toLocaleTimeString("vi-VN"))
              }
            >
              <RefreshCw size={14} />
              Làm mới
            </button>
          </div>
          <div className="cafe-station-map">
            {computers.map((pc) => (
              <Link
                key={pc.ComputerId}
                to={`/admin/computers?computer=${pc.ComputerId}`}
                className={`cafe-map-link ${pc.Status.toLowerCase()} ${foodAlertIds.has(pc.ComputerId) ? "has-food" : ""}`}
                aria-label={`Xem ${pc.ComputerName}`}
              >
                <span>{pc.ComputerName}</span>
                <span
                  className={`cafe-badge ${foodAlertIds.has(pc.ComputerId) ? "food" : pc.Status.toLowerCase()}`}
                >
                  {foodAlertIds.has(pc.ComputerId)
                    ? "Gọi món"
                    : pc.Status === "Occupied"
                      ? "Đang dùng"
                      : pc.Status === "Available"
                        ? "Trống"
                        : "Bảo trì"}
                </span>
              </Link>
            ))}
          </div>
          <div className="cafe-legend">
            <span>
              <i className="occupied" />
              Đang dùng <b>{occupied}</b>
            </span>
            <span>
              <i className="available" />
              Còn trống <b>{available}</b>
            </span>
            <span>
              <i className="food" />
              Gọi món <b>{alerts}</b>
            </span>
            <span>
              <i className="maintenance" />
              Bảo trì <b>{maintenance}</b>
            </span>
          </div>
          <div className="cafe-map-footer">
            Chọn một máy để xem thông tin chi tiết.
            <Link to="/admin/computers">
              Quản lý máy <ArrowRight size={14} />
            </Link>
          </div>
        </article>
        <article className="cafe-panel cafe-orders-panel">
          <div className="cafe-panel-heading">
            <div>
              <h2>Đơn món gần đây</h2>
              <p>{pending} đơn đang chờ xử lý</p>
            </div>
            <span className="cafe-attention">{pending} đang chờ</span>
          </div>
          <div className="cafe-order-list">
            {(showAll ? recentOrders : recentOrders.slice(0, 8)).map(
              (order) => (
                <div className="cafe-order" key={order.OrderId}>
                  <Link
                    to={`/admin/computers?computer=${order.ComputerId}`}
                    className="cafe-order-pc"
                  >
                    PC-{String(order.ComputerId).padStart(2, "0")}
                  </Link>
                  <div className="cafe-order-info">
                    <strong>{order.ProductName}</strong>
                    <small>{order.MinutesAgo} phút trước</small>
                  </div>
                  <span
                    className={`cafe-order-status ${order.Status.toLowerCase()}`}
                  >
                    {order.Status === "Pending" ? "Đang chờ" : "Đã giao"}
                  </span>
                </div>
              ),
            )}
          </div>
          <button
            className="cafe-text-button cafe-orders-more"
            onClick={() => setShowAll((value) => !value)}
          >
            {showAll
              ? "Thu gọn danh sách"
              : `Xem tất cả ${recentOrders.length} đơn`}
            <ArrowRight size={15} />
          </button>
        </article>
      </div>
      <p className="cafe-demo-note">
        Dữ liệu minh họa · Doanh thu, phiên chơi và đơn món chưa kết nối với hệ
        thống thực tế.
      </p>
    </section>
  );
}
