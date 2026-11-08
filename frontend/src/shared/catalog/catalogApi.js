import { apiRequest } from '../auth/api.js';

function validateCatalog(categories, products) {
  if (!Array.isArray(categories) || !Array.isArray(products)
    || categories.some(c => !Number.isSafeInteger(c.id) || c.id <= 0 || typeof c.name !== 'string' || typeof c.isActive !== 'boolean')
    || products.some(p => !Number.isSafeInteger(p.id) || p.id <= 0 || typeof p.name !== 'string'
      || !Number.isSafeInteger(p.categoryId) || !Number.isSafeInteger(p.currentPrice) || p.currentPrice < 0
      || !Number.isSafeInteger(p.stockQuantity) || p.stockQuantity < 0 || typeof p.isActive !== 'boolean')) {
    throw new Error('Dữ liệu thực đơn trả về không hợp lệ.');
  }
  return {
    product_categories: categories.map(c => ({ id: c.id, name: c.name, is_active: c.isActive })),
    products: products.map(p => ({ id: p.id, name: p.name, category_id: p.categoryId, current_price: p.currentPrice, stock_quantity: p.stockQuantity, is_active: p.isActive })),
    order_items: [],
  };
}

export async function getCatalog(admin, signal) {
  if (admin) {
    const [categories, products] = await Promise.all([
      apiRequest('/admin/categories', { signal }), apiRequest('/admin/products', { signal }),
    ]);
    return validateCatalog(categories, products);
  }
  const menu = await apiRequest('/menu', { signal });
  return validateCatalog(menu?.categories, menu?.products);
}

export function saveCategory(form) {
  return apiRequest(`/admin/categories${form.id ? `/${form.id}` : ''}`, {
    method: form.id ? 'PUT' : 'POST', body: JSON.stringify({ name: form.name, isActive: form.is_active }),
  });
}

export function saveProduct(form) {
  return apiRequest(`/admin/products${form.id ? `/${form.id}` : ''}`, {
    method: form.id ? 'PUT' : 'POST', body: JSON.stringify({ name: form.name, categoryId: Number(form.category_id), currentPrice: Number(form.current_price), isActive: form.is_active }),
  });
}

export function disableCatalogItem(kind, id) {
  return apiRequest(`/admin/${kind}/${id}`, { method: 'DELETE' });
}
