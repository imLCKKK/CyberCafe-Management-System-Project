export const rooms = [
  { RoomId: 1, RoomName: "Phòng Standard", Floor: 1 },
  { RoomId: 2, RoomName: "Phòng VIP", Floor: 2 },
  { RoomId: 3, RoomName: "Phòng thi đấu", Floor: 2 },
];
export const computerTypes = [
  {
    ComputerTypeId: 1,
    TypeName: "Standard",
    CPU: "Intel Core i5-12400F",
    RAM: "16 GB",
    GPU: "RTX 3060",
    PricePerHour: 10000,
  },
  {
    ComputerTypeId: 2,
    TypeName: "VIP",
    CPU: "Intel Core i7-13700F",
    RAM: "32 GB",
    GPU: "RTX 4070",
    PricePerHour: 18000,
  },
  {
    ComputerTypeId: 3,
    TypeName: "Gaming Pro",
    CPU: "Intel Core i7-14700F",
    RAM: "32 GB",
    GPU: "RTX 4070 Ti",
    PricePerHour: 25000,
  },
];
const available = [3, 8, 16, 24, 31, 39, 48, 49, 50];
export const mockData = Array.from({ length: 50 }, (_, i) => {
  const id = i + 1;
  const type = id <= 30 ? 1 : id <= 40 ? 2 : 3;
  return {
    ComputerId: id,
    ComputerName: `PC-${String(id).padStart(2, "0")}`,
    RoomId: type,
    ComputerTypeId: type,
    Status: available.includes(id)
      ? "Available"
      : id === 40
        ? "Maintenance"
        : "Occupied",
  };
});
// UI demonstration only; the backend will own sessions and billing when connected.
export const demoSessions = mockData
  .filter((pc) => pc.Status === "Occupied")
  .map((pc) => ({
    SessionId: pc.ComputerId,
    ComputerId: pc.ComputerId,
    CustomerName: [
      "Nguyễn Minh Anh",
      "Trần Hoàng Nam",
      "Lê Gia Huy",
      "Phạm Tuấn Kiệt",
    ][pc.ComputerId % 4],
    DurationMinutes: 35 + ((pc.ComputerId * 13) % 180),
  }));
export const statusLabels = {
  Available: "Còn trống",
  Occupied: "Đang dùng",
  Maintenance: "Bảo trì",
};
export const currency = (value) =>
  new Intl.NumberFormat("vi-VN", {
    style: "currency",
    currency: "VND",
    maximumFractionDigits: 0,
  }).format(value);
