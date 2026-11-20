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
const validType = (row) => row && positiveId(row.computerTypeId)
  && typeof row.typeName === 'string' && row.typeName.trim() && Number.isFinite(row.pricePerHour) && row.pricePerHour >= 0
  && nullableText(row.cpu) && nullableText(row.ram) && nullableText(row.gpu) && nullableText(row.description);
const toType = (row) => ({
  ComputerTypeId: row.computerTypeId, TypeName: row.typeName, CPU: row.cpu, RAM: row.ram,
  GPU: row.gpu, PricePerHour: row.pricePerHour, Description: row.description,
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
  validateRows(types, 'computerTypeId', validType);
  validateRows(rooms, 'roomId', (row) => row && positiveId(row.roomId) && typeof row.roomName === 'string'
    && Number.isInteger(row.floor) && nullableText(row.description));
  const roomIds = new Set(rooms.map((row) => row.roomId));
  const typeIds = new Set(types.map((row) => row.computerTypeId));
  if (computers.some((row) => !roomIds.has(row.roomId) || !typeIds.has(row.computerTypeId)))
    throw new ApiError('Dữ liệu máy chưa khớp với danh sách phòng hoặc loại máy. Vui lòng tải lại.', 200);
  return {
    computers: computers.map(toComputer),
    rooms: rooms.map((row) => ({ RoomId: row.roomId, RoomName: row.roomName, Floor: row.floor })),
    types: types.map(toType),
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

export async function saveComputer(id, draft, signal) {
  const result = await apiRequest(`/admin/computers${id ? `/${id}` : ''}`, {
    method: id ? 'PUT' : 'POST', signal,
    body: { computerName: draft.ComputerName.trim(), roomId: Number(draft.RoomId), computerTypeId: Number(draft.ComputerTypeId) },
  });
  if (!validComputer(result) || (id && result.computerId !== id))
    throw new ApiError('Máy chủ trả về dữ liệu máy không hợp lệ. Vui lòng tải lại.', 200);
  return toComputer(result);
}

export const deleteComputer = (id, signal) => apiRequest(`/admin/computers/${id}`, { method: 'DELETE', signal });

export async function saveComputerType(id, draft, signal) {
  const result = await apiRequest(`/admin/computer-types${id ? `/${id}` : ''}`, {
    method: id ? 'PUT' : 'POST', signal,
    body: { typeName: draft.TypeName.trim(), pricePerHour: Number(draft.PricePerHour),
      cpu: draft.CPU?.trim() || null, ram: draft.RAM?.trim() || null, gpu: draft.GPU?.trim() || null,
      description: draft.Description?.trim() || null },
  });
  if (!validType(result) || (id && result.computerTypeId !== id))
    throw new ApiError('Máy chủ trả về dữ liệu loại máy không hợp lệ. Vui lòng tải lại.', 200);
  return toType(result);
}

export const deleteComputerType = (id, signal) => apiRequest(`/admin/computer-types/${id}`, { method: 'DELETE', signal });

const validMaintenance = (row) => row && positiveId(row.maintenanceId) && positiveId(row.computerId)
  && positiveId(row.employeeId) && nullableText(row.description) && typeof row.status === 'string'
  && typeof row.startTime === 'string' && Number.isFinite(Date.parse(row.startTime))
  && (row.endTime === null || (typeof row.endTime === 'string' && Number.isFinite(Date.parse(row.endTime))));

export async function getMaintenances(computerId, signal) {
  const records = await apiRequest(`/admin/computers/${computerId}/maintenances`, { signal });
  return validateRows(records, 'maintenanceId', (row) => validMaintenance(row) && row.computerId === computerId);
}

export async function saveMaintenance(computerId, id, description, signal) {
  const result = await apiRequest(`/admin/computers/${computerId}/maintenances${id ? `/${id}` : ''}`, {
    method: id ? 'PUT' : 'POST', signal, body: { description: description.trim() || null },
  });
  if (!validMaintenance(result) || result.computerId !== computerId || (id && result.maintenanceId !== id))
    throw new ApiError('Máy chủ trả về hồ sơ bảo trì không hợp lệ. Vui lòng tải lại.', 200);
  return result;
}

export async function completeMaintenance(computerId, id, signal) {
  const result = await apiRequest(`/admin/computers/${computerId}/maintenances/${id}/complete`, { method: 'POST', signal });
  if (!validMaintenance(result) || result.computerId !== computerId || result.maintenanceId !== id
    || result.status !== 'Completed' || result.endTime === null)
    throw new ApiError('Máy chủ trả về hồ sơ bảo trì không hợp lệ. Vui lòng tải lại.', 200);
  return result;
}
