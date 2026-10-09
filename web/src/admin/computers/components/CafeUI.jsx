import React, { useEffect, useRef } from "react";
import { X, Monitor, Wrench, ShoppingBag } from "lucide-react";
import { statusLabels } from "../mockData";

export function PageHeading({ eyebrow, title, description, children }) {
  return (
    <div className="cafe-heading">
      <div>
        <div className="cafe-eyebrow">{eyebrow}</div>
        <h1>{title}</h1>
        <p>{description}</p>
      </div>
      <div className="cafe-heading-actions">{children}</div>
    </div>
  );
}
export function StatCard({
  label,
  value,
  suffix,
  icon: Icon,
  children,
  progress,
}) {
  return (
    <article className="cafe-stat">
      <div className="cafe-stat-label">
        {label}
        {Icon && <Icon size={19} />}
      </div>
      <div className="cafe-stat-value">
        {value}
        <span>{suffix}</span>
      </div>
      {progress !== undefined && (
        <div
          className="cafe-progress"
          role="progressbar"
          aria-label={label}
          aria-valuemin={0}
          aria-valuemax={100}
          aria-valuenow={progress}
        >
          <i style={{ width: `${progress}%` }} />
        </div>
      )}
      {children && <div className="cafe-stat-foot">{children}</div>}
    </article>
  );
}
export function StatusBadge({ status, foodAlert = false }) {
  return (
    <span className={`cafe-badge ${foodAlert ? "food" : status.toLowerCase()}`}>
      {foodAlert ? "Gọi món" : statusLabels[status]}
    </span>
  );
}
export function Station({ computer, foodAlert, onClick, type, room }) {
  return (
    <button
      type="button"
      className={`cafe-station ${computer.Status.toLowerCase()} ${foodAlert ? "has-food" : ""}`}
      onClick={onClick}
      aria-label={`${computer.ComputerName}, ${statusLabels[computer.Status]}${foodAlert ? ", có yêu cầu gọi món" : ""}`}
    >
      <div className="cafe-station-top">
        <span>{type}</span>
        {foodAlert ? (
          <ShoppingBag size={16} />
        ) : computer.Status === "Maintenance" ? (
          <Wrench size={16} />
        ) : (
          <Monitor size={18} />
        )}
      </div>
      <strong>{computer.ComputerName}</strong>
      <small>{room}</small>
      <StatusBadge status={computer.Status} foodAlert={foodAlert} />
    </button>
  );
}
export function Modal({ title, children, onClose }) {
  const dialog = useRef(null);
  useEffect(() => {
    const element = dialog.current;
    element.showModal();
    return () => {
      element.close();
    };
  }, []);
  return (
    <dialog
      ref={dialog}
      className="cafe-dialog"
      aria-labelledby="cafe-dialog-title"
      onCancel={onClose}
      onClick={(event) => {
        if (event.target === event.currentTarget) onClose();
      }}
    >
      <div className="cafe-dialog-content">
        <div className="cafe-dialog-title">
          <h2 id="cafe-dialog-title">{title}</h2>
          <button
            type="button"
            className="cafe-icon-button"
            aria-label="Đóng"
            onClick={onClose}
          >
            <X size={20} />
          </button>
        </div>
        {children}
      </div>
    </dialog>
  );
}
