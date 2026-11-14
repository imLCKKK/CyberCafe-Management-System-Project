import { ApiError, apiRequest } from '../../shared/apiClient.js';

const positiveId = (value) => Number.isInteger(value) && value > 0;
const nullableText = (value) => value === null || typeof value === 'string';
const validComputer = (row) => row && positiveId(row.computerId) && positiveId(row.roomId)
  && positiveId(row.computerTypeId) && typeof row.computerName === 'string' && row.computerName.trim()
  && typeof row.status === 'string' && row.status.trim()
  && typeof row.hasOpenSession === 'boolean' && typeof row.hasOpenMaintenance === 'boolean';
const toComputer = (row) => ({
  ComputerId: row.computerId, ComputerName: row.computerName, RoomId: row.roomId,
  ComputerTypeId: row.computerTypeId, Status: row.status,
  HasOpenSession: row.hasOpenSession, HasOpenMaintenance: row.hasOpenMaintenance,
});

function validateRows(rows, key, predicate) {
  if (!Array.isArray(rows) || !rows.every(predicate) || new Set(rows.map((row) => row[key])).size !== rows.length)
    throw new ApiError('Dữ liệu danh sách máy, phòng hoặc loại máy không hợp lệ.', 200);
  return rows;
}

export async function getComputerData(signal) {
  const [computers, types, rooms] = await Promise.all([
    apiRequest('/admin/computers', { signal }),
    apiRequest('/admin/computer-types', { signal }),
    apiRequest('/admin/computer-rooms', { signal }),
  ]);
  validateRows(computers, 'computerId', validComputer);
  validateRows(types, 'computerTypeId', (row) => row && positiveId(row.computerTypeId)
    && typeof row.typeName === 'string' && Number.isFinite(row.pricePerHour) && row.pricePerHour >= 0
    && nullableText(row.cpu) && nullableText(row.ram) && nullableText(row.gpu) && nullableText(row.description));
  validateRows(rooms, 'roomId', (row) => row && positiveId(row.roomId) && typeof row.roomName === 'string'
    && Number.isInteger(row.floor) && nullableText(row.description));
  const roomIds = new Set(rooms.map((row) => row.roomId));
  const typeIds = new Set(types.map((row) => row.computerTypeId));
  if (computers.some((row) => !roomIds.has(row.roomId) || !typeIds.has(row.computerTypeId)))
    throw new ApiError('Dữ liệu máy chưa khớp với danh sách phòng hoặc loại máy. Vui lòng tải lại.', 200);
  return {
    computers: computers.map(toComputer),
    rooms: rooms.map((row) => ({ RoomId: row.roomId, RoomName: row.roomName, Floor: row.floor })),
    types: types.map((row) => ({
      ComputerTypeId: row.computerTypeId, TypeName: row.typeName, CPU: row.cpu, RAM: row.ram,
      GPU: row.gpu, PricePerHour: row.pricePerHour, Description: row.description,
    })),
  };
}

export async function updateComputerStatus(computer, signal) {
  const targetStatus = computer.Status === 'Maintenance' ? 'Available' : 'Maintenance';
  const result = await apiRequest(`/admin/computers/${computer.ComputerId}/status`, {
    method: 'PATCH', signal,
    body: { status: targetStatus, expectedStatus: computer.Status },
  });
  if (!validComputer(result) || result.computerId !== computer.ComputerId || result.status !== targetStatus
    || result.roomId !== computer.RoomId || result.computerTypeId !== computer.ComputerTypeId)
    throw new ApiError('Máy chủ trả về dữ liệu máy không hợp lệ. Vui lòng tải lại.', 200);
  return toComputer(result);
}
