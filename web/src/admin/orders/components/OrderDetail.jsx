import React, { useState } from "react";
import { Monitor, UserRound, ArrowRight, Clock3 } from "lucide-react";
import {
  Modal,
  StatusBadge,
  FoodArt,
} from "../../../shared/commerce/components";
import {
  getOrderContext,
  money,
  statuses,
  nextStatus,
  nextAction,
} from "../../../shared/commerce/model";
import { commerce } from "../../../shared/commerce/store";

export default function OrderDetail({
  order,
  data,
  onClose,
  onUpdated,
  readOnly = false,
}) {
  const [error, setError] = useState(""),
    [confirmCancel, setConfirmCancel] = useState(false);
  const context = getOrderContext(data, order);
  const details = data.orderDetails.filter(
    (item) => item.OrderId === order.OrderId,
  );
  function update(status) {
    try {
      commerce.changeOrderStatus(order.OrderId, status);
      setConfirmCancel(false);
      setError("");
      onUpdated?.(status);
    } catch (error) {
      setError(error.message);
    }
  }
  return (
    <Modal title={`Chi tiết đơn #GM${order.OrderId}`} onClose={onClose} wide>
      <div className="cc-order-detail">
        <div className="cc-detail-status">
          <StatusBadge status={order.Status} />
          <span>{new Date(order.OrderTime).toLocaleString("vi-VN")}</span>
        </div>
        <div className="cc-detail-meta">
          <div>
            <Monitor size={19} />
            <div>
              <small>Giao đến</small>
              <strong>{context.computer}</strong>
            </div>
          </div>
          <div>
            <UserRound size={19} />
            <div>
              <small>Khách hàng</small>
              <strong>{context.customer}</strong>
            </div>
          </div>
        </div>
        <div className="cc-detail-items">
          {details.map((line) => {
            const product = data.products.find(
              (item) => item.ProductId === line.ProductId,
            );
            return (
              <div className="cc-detail-item" key={line.OrderDetailId}>
                <FoodArt product={product || {}} small />
                <div>
                  <strong>
                    {product?.ProductName || "Sản phẩm không còn tồn tại"}
                  </strong>
                  <small>
                    {money(line.UnitPrice)} × {line.Quantity}
                  </small>
                </div>
                <strong>{money(line.UnitPrice * line.Quantity)}</strong>
              </div>
            );
          })}
        </div>
        <div className="cc-total">
          <span>Tổng tiền</span>
          <strong>{money(order.TotalAmount)}</strong>
        </div>
        <p className="cc-muted">
          {order.Status === "Cancelled"
            ? "Đơn đã hủy. Không tính tiền vào hóa đơn."
            : "Thanh toán cùng hóa đơn phiên chơi. Trạng thái giao món không phải trạng thái thanh toán."}
        </p>
        <h3 className="cc-timeline-title">
          <Clock3 size={16} />
          Lịch sử đơn hàng
        </h3>
        <ol className="cc-timeline">
          {data.orderStatusHistory
            .filter((item) => item.OrderId === order.OrderId)
            .map((item) => (
              <li key={item.HistoryId}>
                <div>
                  <strong>{statuses[item.NewStatus]}</strong>
                  <small>
                    {item.ChangedBy ? "Nhân viên quản lý" : "Khách đặt món"}
                  </small>
                </div>
                <time>
                  {new Date(item.ChangedAt).toLocaleTimeString("vi-VN", {
                    hour: "2-digit",
                    minute: "2-digit",
                  })}
                </time>
              </li>
            ))}
        </ol>
        {error && (
          <p role="alert" className="cc-form-error">
            {error}
          </p>
        )}
        {!readOnly &&
          confirmCancel &&
          ["Pending", "Preparing"].includes(order.Status) && (
            <div className="cc-cancel-confirm">
              <strong>Hủy đơn #GM{order.OrderId}?</strong>
              <p>Các món trong đơn sẽ được hoàn lại tồn kho.</p>
              <div>
                <button
                  className="cc-button"
                  onClick={() => setConfirmCancel(false)}
                >
                  Giữ đơn
                </button>
                <button
                  className="cc-button cc-danger"
                  onClick={() => update("Cancelled")}
                >
                  Xác nhận hủy đơn
                </button>
              </div>
            </div>
          )}
        {!readOnly && !confirmCancel && (
          <div className="cc-modal-actions">
            {["Pending", "Preparing"].includes(order.Status) && (
              <button
                className="cc-button cc-danger-outline"
                onClick={() => setConfirmCancel(true)}
              >
                Hủy đơn
              </button>
            )}
            {nextStatus[order.Status] ? (
              <button
                className="cc-button cc-primary"
                onClick={() => update(nextStatus[order.Status])}
              >
                {nextAction[order.Status]}
                <ArrowRight size={16} />
              </button>
            ) : (
              <button className="cc-button" onClick={onClose}>
                Đóng
              </button>
            )}
          </div>
        )}
      </div>
    </Modal>
  );
}
