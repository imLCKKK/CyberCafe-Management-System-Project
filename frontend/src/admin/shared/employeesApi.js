import { apiRequest } from '../../shared/apiClient.js';

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

  async create(data) {
    return await apiRequest('/employees', {
      method: 'POST',
      body: data
    });
  },

  async update(employeeId, data) {
    return await apiRequest(`/employees/${employeeId}`, {
      method: 'PUT',
      body: data
    });
  },

  async lock(employeeId) {
    return await apiRequest(`/employees/${employeeId}/lock`, {
      method: 'POST'
    });
  },

  async unlock(employeeId) {
    return await apiRequest(`/employees/${employeeId}/unlock`, {
      method: 'POST'
    });
  },

  async changePassword(data) {
    return await apiRequest('/auth/change-password', {
      method: 'POST',
      body: data
    });
  }
};
