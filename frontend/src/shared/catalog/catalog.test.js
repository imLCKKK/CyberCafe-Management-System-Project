import assert from 'node:assert/strict';
import { afterEach, beforeEach, test } from 'node:test';
import { getCatalog, saveProduct } from './catalogApi.js';
import { reconcileCart } from './catalogModel.js';
import { clearVerifiedSession, storeSession, markSessionVerified } from '../auth/api.js';

const original = { fetch: globalThis.fetch, localStorage: globalThis.localStorage };
beforeEach(() => {
  const storage = new Map();
  globalThis.localStorage = { getItem: k => storage.get(k) ?? null, setItem: (k, v) => storage.set(k, v), removeItem: k => storage.delete(k) };
  const session = { accessToken: 'test-only', accountType: 'employee' };
  storeSession(session); markSessionVerified(session);
});
afterEach(() => { globalThis.fetch = original.fetch; globalThis.localStorage = original.localStorage; clearVerifiedSession(); });
const categories = [{ id: 123, name: 'SQL category', isActive: true }];
const products = [{ id: 987, name: 'SQL product', categoryId: 123, currentPrice: 30000, stockQuantity: 2, isActive: true }];

test('catalog preserves real IDs, price and SQL stock; cart follows availability', async () => {
  globalThis.fetch = async () => Response.json({ categories, products });
  const db = await getCatalog(false);
  assert.deepEqual(reconcileCart(db, { 987: 10, 1: 1 }), { 987: 2 });
  assert.equal(db.products[0].current_price, 30000);
  db.product_categories[0].is_active = false;
  assert.deepEqual(reconcileCart(db, { 987: 1 }), {});
  db.product_categories[0].is_active = true;
  db.products[0].is_active = false;
  assert.deepEqual(reconcileCart(db, { 987: 1 }), {});
  db.products[0].is_active = true;
  db.products[0].stock_quantity = 0;
  assert.deepEqual(reconcileCart(db, { 987: 1 }), {});
});

test('empty catalog stays empty; invalid or failed API never returns demo products', async () => {
  globalThis.fetch = async () => Response.json({ categories: [], products: [] });
  assert.deepEqual((await getCatalog(false)).products, []);
  globalThis.fetch = async () => Response.json({ categories, products: [{ ...products[0], stockQuantity: '2' }] });
  await assert.rejects(getCatalog(false), /không hợp lệ/);
  globalThis.fetch = async () => Response.json({ detail: 'Catalog offline' }, { status: 503 });
  await assert.rejects(getCatalog(false), /Catalog offline/);
});

test('product writes send only catalog fields with authenticated request', async () => {
  globalThis.fetch = async (url, options) => {
    assert.equal(url, '/api/admin/products/987');
    assert.equal(options.method, 'PUT');
    assert.equal(options.headers.get('Authorization'), 'Bearer test-only');
    assert.deepEqual(JSON.parse(options.body), { name: 'Updated', categoryId: 123, currentPrice: 40000, isActive: true });
    return Response.json(products[0]);
  };
  await saveProduct({ id: 987, name: 'Updated', category_id: '123', current_price: '40000', is_active: true, stock_quantity: 999, total: 1 });
});
