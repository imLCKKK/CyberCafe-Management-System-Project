import { useSyncExternalStore } from "react";
import { mockData, rooms, computerTypes } from "./mockData";

const storageKey = "net-cafe.admin.computers.v1";
const listeners = new Set();
function readComputers() {
  try {
    const data = JSON.parse(localStorage.getItem(storageKey));
    if (
      Array.isArray(data) &&
      data.every(
        (pc) =>
          Number.isInteger(pc.ComputerId) &&
          pc.ComputerId > 0 &&
          typeof pc.ComputerName === "string" &&
          pc.ComputerName.trim() &&
          rooms.some((r) => r.RoomId === pc.RoomId) &&
          computerTypes.some((t) => t.ComputerTypeId === pc.ComputerTypeId) &&
          ["Available", "Occupied", "Maintenance"].includes(pc.Status),
      ) &&
      new Set(data.map((pc) => pc.ComputerId)).size === data.length
    )
      return data;
  } catch {
    /* Unavailable storage or old data: use the sample dataset. */
  }
  return mockData;
}
let computers = readComputers();
const subscribe = (callback) => {
  listeners.add(callback);
  return () => listeners.delete(callback);
};
window.addEventListener("storage", (event) => {
  if (event.key === storageKey || event.key === null) {
    computers = readComputers();
    listeners.forEach((fn) => fn());
  }
});
export function useComputers() {
  return useSyncExternalStore(subscribe, () => computers);
}
export function saveComputer(computer) {
  const name = computer.ComputerName.trim();
  if (!name) throw new Error("Vui lòng nhập tên máy.");
  if (
    computers.some(
      (pc) =>
        pc.ComputerId !== computer.ComputerId &&
        pc.ComputerName.toLocaleLowerCase("vi") ===
          name.toLocaleLowerCase("vi"),
    )
  )
    throw new Error("Tên máy đã tồn tại. Vui lòng chọn tên khác.");
  const existing = computers.find(
    (pc) => pc.ComputerId === computer.ComputerId,
  );
  if (existing?.Status === "Occupied")
    throw new Error("Không thể thay đổi máy đang có phiên sử dụng.");
  const record = {
    ...computer,
    ComputerName: name,
    ComputerId:
      computer.ComputerId ||
      Math.max(0, ...computers.map((pc) => pc.ComputerId)) + 1,
  };
  const next = existing
    ? computers.map((pc) => (pc.ComputerId === record.ComputerId ? record : pc))
    : [...computers, record];
  try {
    localStorage.setItem(storageKey, JSON.stringify(next));
  } catch {
    throw new Error(
      "Không thể lưu dữ liệu vào trình duyệt. Hãy kiểm tra dung lượng hoặc quyền lưu trữ.",
    );
  }
  computers = next;
  listeners.forEach((fn) => fn());
  return record;
}
