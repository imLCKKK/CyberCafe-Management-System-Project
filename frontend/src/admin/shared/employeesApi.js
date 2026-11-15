import { apiRequest } from '../shared/apiClient.js';

export const employeesApi = {
  async getAll() {
    return await apiRequest('/api/employees', { method: 'GET' });
  },

  async getById(employeeId) {
    return await apiRequest(`/api/employees/${employeeId}`, { method: 'GET' });
  },

  async create(data) {
    return await apiRequest('/api/employees', { 
      method: 'POST',
      body: data
    });
  },

  async update(employeeId, data) {
    return await apiRequest(`/api/employees/${employeeId}`, {
      method: 'PUT',
      body: data
    });
  },

  async lock(employeeId) {
    return await apiRequest(`/api/employees/${employeeId}/lock`, {
      method: 'POST'
    });
  },

  async unlock(employeeId) {
    return await apiRequest(`/api/employees/${employeeId}/unlock`, {
      method: 'POST'
    });
  },

  async changePassword(employeeId, data) {
    return await apiRequest(`/api/employees/${employeeId}/change-password`, {
      method: 'POST',
      body: data
    });
  }
};
