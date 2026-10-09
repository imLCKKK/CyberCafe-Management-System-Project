import React, { useState } from "react";
import { useSearchParams } from "react-router-dom";
import {
  Monitor,
  Search,
  Plus,
  LayoutGrid,
  List,
  Wrench,
  CircleCheck,
  Users,
  Pencil,
  Cpu,
  MemoryStick,
  CircuitBoard,
} from "lucide-react";
import {
  rooms,
  computerTypes,
  demoSessions,
  statusLabels,
  currency,
} from "./mockData";
import { useComputers, saveComputer } from "./computerStore";
import {
  PageHeading,
  StatCard,
  Station,
  StatusBadge,
  Modal,
} from "./components/CafeUI";
import { foodAlertIds } from "../dashboard/mockData";
import "./cafe.css";

function ComputerForm({ computer, onClose, onSaved }) {
  const [form, setForm] = useState(
    computer || {
      ComputerName: "",
      RoomId: 1,
      ComputerTypeId: 1,
      Status: "Available",
    },
  );
  const [error, setError] = useState("");
  const change = (event) =>
    setForm({
      ...form,
      [event.target.name]: ["RoomId", "ComputerTypeId"].includes(
        event.target.name,
      )
        ? Number(event.target.value)
        : event.target.value,
    });
  return (
    <Modal
      title={computer ? `Chỉnh sửa ${computer.ComputerName}` : "Thêm máy tính"}
      onClose={onClose}
    >
      <form
        className="cafe-form"
        onSubmit={(event) => {
          event.preventDefault();
          try {
            const saved = saveComputer(form);
            onSaved(saved);
          } catch (err) {
            setError(err.message);
          }
        }}
      >
        <label>
          Tên máy{" "}
          <input
            autoFocus
            name="ComputerName"
            placeholder="Ví dụ: PC-51"
            value={form.ComputerName}
            onChange={change}
            required
            maxLength={100}
          />
        </label>
        <label>
          Phòng máy
          <select name="RoomId" value={form.RoomId} onChange={change}>
            {rooms.map((room) => (
              <option key={room.RoomId} value={room.RoomId}>
                {room.RoomName} · Tầng {room.Floor}
              </option>
            ))}
          </select>
        </label>
        <label>
          Loại máy
          <select
            name="ComputerTypeId"
            value={form.ComputerTypeId}
            onChange={change}
          >
            {computerTypes.map((type) => (
              <option key={type.ComputerTypeId} value={type.ComputerTypeId}>
                {type.TypeName} · {currency(type.PricePerHour)}/giờ
              </option>
            ))}
          </select>
        </label>
        <label>
          Trạng thái
          <select name="Status" value={form.Status} onChange={change}>
            <option value="Available">Còn trống</option>
            <option value="Maintenance">Bảo trì</option>
          </select>
        </label>
        {error && (
          <p className="cafe-error" role="alert">
            {error}
          </p>
        )}
        <div className="cafe-dialog-actions">
          <button type="button" className="cafe-button" onClick={onClose}>
            Hủy
          </button>
          <button className="cafe-button primary" type="submit">
            {computer ? "Lưu thay đổi" : "Thêm máy"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

export function ComputersPage() {
  const computers = useComputers();
  const [params, setParams] = useSearchParams();
  const [search, setSearch] = useState("");
  const [room, setRoom] = useState("all");
  const [type, setType] = useState("all");
  const [status, setStatus] = useState("all");
  const [view, setView] = useState("grid");
  const [editing, setEditing] = useState(undefined);
  const [notice, setNotice] = useState("");
  const selected = computers.find(
    (pc) => pc.ComputerId === Number(params.get("computer")),
  );
  const select = (pc) =>
    setParams(pc ? { computer: String(pc.ComputerId) } : {});
  const filtered = computers.filter(
    (pc) =>
      pc.ComputerName.toLocaleLowerCase("vi").includes(
        search.trim().toLocaleLowerCase("vi"),
      ) &&
      (room === "all" || pc.RoomId === Number(room)) &&
      (type === "all" || pc.ComputerTypeId === Number(type)) &&
      (status === "all" || pc.Status === status),
  );
  const counts = Object.fromEntries(
    Object.keys(statusLabels).map((key) => [
      key,
      computers.filter((pc) => pc.Status === key).length,
    ]),
  );
  const selectedType =
    selected &&
    computerTypes.find((t) => t.ComputerTypeId === selected.ComputerTypeId);
  const session =
    selected && demoSessions.find((s) => s.ComputerId === selected.ComputerId);
  function updateMaintenance() {
    try {
      saveComputer({
        ...selected,
        Status: selected.Status === "Maintenance" ? "Available" : "Maintenance",
      });
      setNotice(`Đã cập nhật trạng thái ${selected.ComputerName}.`);
      select(null);
    } catch (err) {
      setNotice(err.message);
    }
  }
  return (
    <section className="cafe-page cafe-computers">
      <PageHeading
        eyebrow="KHÔNG GIAN LÀM VIỆC / MÁY TÍNH"
        title="Quản lý máy tính"
        description="Theo dõi trạng thái và quản lý toàn bộ máy tại quán."
      >
        <button
          className="cafe-button primary"
          onClick={() => {
            setNotice("");
            setEditing(null);
          }}
        >
          <Plus size={17} />
          Thêm máy tính
        </button>
      </PageHeading>
      <div className="cafe-stats">
        <StatCard label="Tổng số máy" value={computers.length} icon={Monitor}>
          <span>Trong {rooms.length} phòng máy</span>
        </StatCard>
        <StatCard label="Đang sử dụng" value={counts.Occupied} icon={Users}>
          <span className="cafe-blue">
            {computers.length
              ? Math.round((counts.Occupied / computers.length) * 100)
              : 0}
            % công suất
          </span>
        </StatCard>
        <StatCard
          label="Sẵn sàng sử dụng"
          value={counts.Available}
          icon={CircleCheck}
        >
          <span className="cafe-positive">Có thể đón khách</span>
        </StatCard>
        <StatCard label="Đang bảo trì" value={counts.Maintenance} icon={Wrench}>
          <span className="cafe-muted">Tạm ngừng phục vụ</span>
        </StatCard>
      </div>
      {notice && (
        <div className="cafe-notice" role="status">
          {notice}
          <button className="cafe-text-button" onClick={() => setNotice("")}>
            Đóng
          </button>
        </div>
      )}
      <article className="cafe-panel">
        <div className="cafe-panel-heading">
          <div>
            <h2>Danh sách máy tính</h2>
            <p>
              {filtered.length} / {computers.length} máy <span>·</span> Dữ liệu
              mẫu lưu trên trình duyệt
            </p>
          </div>
          <div className="cafe-view-switch">
            <button
              aria-label="Dạng lưới"
              aria-pressed={view === "grid"}
              onClick={() => setView("grid")}
            >
              <LayoutGrid size={17} />
            </button>
            <button
              aria-label="Dạng bảng"
              aria-pressed={view === "list"}
              onClick={() => setView("list")}
            >
              <List size={18} />
            </button>
          </div>
        </div>
        <div className="cafe-filters">
          <label className="cafe-search">
            <Search size={17} />
            <input
              aria-label="Tìm kiếm tên máy"
              placeholder="Tìm kiếm tên máy..."
              value={search}
              onChange={(e) => setSearch(e.target.value)}
            />
          </label>
          <select
            aria-label="Lọc phòng máy"
            value={room}
            onChange={(e) => setRoom(e.target.value)}
          >
            <option value="all">Tất cả phòng</option>
            {rooms.map((r) => (
              <option key={r.RoomId} value={r.RoomId}>
                {r.RoomName}
              </option>
            ))}
          </select>
          <select
            aria-label="Lọc loại máy"
            value={type}
            onChange={(e) => setType(e.target.value)}
          >
            <option value="all">Tất cả loại máy</option>
            {computerTypes.map((t) => (
              <option key={t.ComputerTypeId} value={t.ComputerTypeId}>
                {t.TypeName}
              </option>
            ))}
          </select>
        </div>
        <div className="cafe-tabs" aria-label="Lọc trạng thái">
          <button
            aria-pressed={status === "all"}
            onClick={() => setStatus("all")}
          >
            Tất cả <span>{computers.length}</span>
          </button>
          {Object.entries(statusLabels).map(([key, label]) => (
            <button
              key={key}
              aria-pressed={status === key}
              onClick={() => setStatus(key)}
            >
              <i className={key.toLowerCase()} />
              {label}
              <span>{counts[key]}</span>
            </button>
          ))}
        </div>
        {filtered.length === 0 ? (
          <div className="cafe-empty">
            <Monitor size={36} />
            <h3>Không tìm thấy máy tính</h3>
            <p>Thử tên máy khác hoặc đặt lại bộ lọc.</p>
            <button
              className="cafe-button"
              onClick={() => {
                setSearch("");
                setRoom("all");
                setType("all");
                setStatus("all");
              }}
            >
              Đặt lại bộ lọc
            </button>
          </div>
        ) : view === "grid" ? (
          <div className="cafe-computer-grid">
            {filtered.map((pc) => (
              <Station
                key={pc.ComputerId}
                computer={pc}
                room={rooms.find((r) => r.RoomId === pc.RoomId).RoomName}
                type={
                  computerTypes.find(
                    (t) => t.ComputerTypeId === pc.ComputerTypeId,
                  ).TypeName
                }
                foodAlert={foodAlertIds.has(pc.ComputerId)}
                onClick={() => select(pc)}
              />
            ))}
          </div>
        ) : (
          <div className="cafe-table-wrap">
            <table className="cafe-table">
              <thead>
                <tr>
                  <th>Tên máy</th>
                  <th>Phòng</th>
                  <th>Loại máy</th>
                  <th>Giá / giờ</th>
                  <th>Trạng thái</th>
                  <th />
                </tr>
              </thead>
              <tbody>
                {filtered.map((pc) => (
                  <tr key={pc.ComputerId}>
                    <td>
                      <strong>{pc.ComputerName}</strong>
                    </td>
                    <td>
                      {rooms.find((r) => r.RoomId === pc.RoomId).RoomName}
                    </td>
                    <td>
                      {
                        computerTypes.find(
                          (t) => t.ComputerTypeId === pc.ComputerTypeId,
                        ).TypeName
                      }
                    </td>
                    <td>
                      {currency(
                        computerTypes.find(
                          (t) => t.ComputerTypeId === pc.ComputerTypeId,
                        ).PricePerHour,
                      )}
                    </td>
                    <td>
                      <StatusBadge status={pc.Status} />
                    </td>
                    <td>
                      <button
                        className="cafe-text-button"
                        aria-label={`Chi tiết ${pc.ComputerName}`}
                        onClick={() => select(pc)}
                      >
                        Chi tiết
                      </button>
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}
        <div className="cafe-computer-footer">
          <span>
            <i className="cafe-live-dot" />
            Trạng thái máy được đồng bộ với trang tổng quan
          </span>
          <span>{filtered.length} máy</span>
        </div>
      </article>
      {selected && editing === undefined && (
        <Modal title={selected.ComputerName} onClose={() => select(null)}>
          <div className="cafe-computer-detail">
            <StatusBadge status={selected.Status} />
            <p>
              {rooms.find((r) => r.RoomId === selected.RoomId).RoomName} ·{" "}
              {selectedType.TypeName}
            </p>
            <div className="cafe-specs">
              <span>
                <Cpu size={18} />
                {selectedType.CPU}
              </span>
              <span>
                <MemoryStick size={18} />
                {selectedType.RAM} RAM
              </span>
              <span>
                <CircuitBoard size={18} />
                {selectedType.GPU}
              </span>
            </div>
            <div className="cafe-detail-row">
              <span>Giá sử dụng</span>
              <strong>{currency(selectedType.PricePerHour)} / giờ</strong>
            </div>
            {selected.Status === "Occupied" && session && (
              <div className="cafe-session">
                <h3>Phiên sử dụng mẫu</h3>
                <div className="cafe-detail-row">
                  <span>Khách hàng</span>
                  <strong>{session.CustomerName}</strong>
                </div>
                <div className="cafe-detail-row">
                  <span>Thời gian chơi</span>
                  <strong>
                    {Math.floor(session.DurationMinutes / 60)} giờ{" "}
                    {session.DurationMinutes % 60} phút
                  </strong>
                </div>
              </div>
            )}
            {selected.Status === "Occupied" ? (
              <p className="cafe-detail-note">
                Máy đang có phiên sử dụng. Chỉ có thể chỉnh sửa hoặc bảo trì sau
                khi kết thúc phiên.
              </p>
            ) : (
              <div className="cafe-dialog-actions">
                <button
                  className="cafe-button"
                  onClick={() => setEditing(selected)}
                >
                  <Pencil size={15} />
                  Chỉnh sửa
                </button>
                <button
                  className="cafe-button primary"
                  onClick={updateMaintenance}
                >
                  <Wrench size={15} />
                  {selected.Status === "Maintenance"
                    ? "Hoàn tất bảo trì"
                    : "Chuyển bảo trì"}
                </button>
              </div>
            )}
          </div>
        </Modal>
      )}
      {editing !== undefined && (
        <ComputerForm
          key={editing?.ComputerId || "new"}
          computer={editing}
          onClose={() => setEditing(undefined)}
          onSaved={(saved) => {
            setEditing(undefined);
            select(null);
            setNotice(`Đã lưu máy ${saved.ComputerName}.`);
          }}
        />
      )}
    </section>
  );
}
