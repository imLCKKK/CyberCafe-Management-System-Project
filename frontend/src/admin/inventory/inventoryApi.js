import { apiRequest } from '../../shared/auth/api';

export async function getInventory(signal) {
  const items = await apiRequest('/admin/inventory', { signal });
  if (!Array.isArray(items)) throw new Error('Dữ liệu tồn kho trả về không hợp lệ.');
  return items;
}
