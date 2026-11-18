import { apiRequest } from '../../shared/apiClient.js';

export function newEmployeeCreateKey() {
  const bytes = globalThis.crypto.getRandomValues(new Uint8Array(16));
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, (byte) => byte.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

export const employeesApi = {
  async getRoles() {
    return await apiRequest('/admin/roles', { method: 'GET' });
  },

  async getAll({ signal } = {}) {
    return await apiRequest('/employees', { method: 'GET', signal });
  },

  async getById(employeeId) {
    return await apiRequest(`/employees/${employeeId}`, { method: 'GET' });
  },

  async create(data, idempotencyKey) {
    return await apiRequest('/employees', {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey },
      body: data
    });
  },

  async update(employeeId, data, idempotencyKey = newEmployeeCreateKey()) {
    return await apiRequest(`/employees/${employeeId}`, {
      method: 'PUT',
      headers: { 'Idempotency-Key': idempotencyKey },
      body: data
    });
  },

  async lock(employeeId, idempotencyKey = newEmployeeCreateKey()) {
    return await apiRequest(`/employees/${employeeId}/lock`, {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey }
    });
  },

  async unlock(employeeId, idempotencyKey = newEmployeeCreateKey()) {
    return await apiRequest(`/employees/${employeeId}/unlock`, {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey }
    });
  },

  async changePassword(data, idempotencyKey = newEmployeeCreateKey()) {
    return await apiRequest('/auth/change-password', {
      method: 'POST',
      headers: { 'Idempotency-Key': idempotencyKey },
      body: data
    });
  }
};
