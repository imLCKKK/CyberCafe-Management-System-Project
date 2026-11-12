import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, input, origin, setupWorkflowLogin } from './cdp.mjs';
import { createClientDemo } from '../src/client/shared/demoData.js';

// Presentation fixtures follow the current HTTP DTOs; API/SQL correctness has separate suites.
const demo = createClientDemo();
const categories = demo.product_categories.map(row => ({ id: row.id, name: row.name, isActive: true }));
const products = Array.from({ length: 18 }, (_, index) => {
  const row = demo.products[index % demo.products.length];
  return { id: index + 1, name: `${row.name} ${index + 1}`, categoryId: row.category_id, currentPrice: row.current_price, stockQuantity: row.stock_quantity, isActive: true };
});
const customers = Array.from({ length: 25 }, (_, index) => ({ customerId: index + 1, fullName: `Khách kiểm thử ${String(index + 1).padStart(2, '0')}`, phone: `090000${String(index + 1).padStart(4, '0')}`, email: null, status: 'Active', createdAtUtc: '2026-11-09T00:00:00Z' }));
const controls = [];
try {
  await cdp('Runtime.enable'); await cdp('Page.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 900, deviceScaleFactor: 1, mobile: false });
  await cdp('Page.navigate', { url: origin + '/admin/auth' });
  await evaluate("localStorage.removeItem('cybermanagement.auth.v1')");
  await go('/admin/computers', 'document.querySelector(".auth-form")');
  assert.equal(await evaluate('location.pathname'), '/admin/auth', 'Unauthenticated staff routes go to the actual login form');
  assert.equal(await evaluate("document.querySelectorAll('.cafe-station').length"), 0);
  assert.equal(await evaluate("Boolean(document.querySelector('dialog input[name=ComputerName]'))"), false);
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const originalFetch = window.fetch.bind(window);
    const products = ${JSON.stringify(products)};
    const categories = ${JSON.stringify(categories)};
    const customers = ${JSON.stringify(customers)};
    window.fetch = async (request, options) => {
      const url = new URL(typeof request === 'string' ? request : request.url, location.href);
      if (url.pathname === '/api/admin/categories') return Response.json(categories);
      if (url.pathname === '/api/admin/products') return Response.json(products);
      if (url.pathname === '/api/admin/inventory') return Response.json(products.map(row => ({ id: row.id, name: row.name, currentPrice: row.currentPrice, stockQuantity: row.stockQuantity, isActive: row.isActive })));
      if (url.pathname === '/api/admin/customers') {
        const page = Number(url.searchParams.get('page') || 1);
        const pageSize = Number(url.searchParams.get('pageSize') || 20);
        const query = (url.searchParams.get('search') || '').toLocaleLowerCase('vi');
        const rows = customers.filter(row => (row.fullName + ' ' + row.phone).toLocaleLowerCase('vi').includes(query));
        return Response.json({ page, pageSize, totalCount: rows.length, items: rows.slice((page - 1) * pageSize, page * pageSize) });
      }
      if (url.pathname.startsWith('/hubs/realtime')) return new Response(null, { status: 503 });
      if (url.pathname.startsWith('/api/') && !url.pathname.startsWith('/api/auth/')) return Response.json({ message: 'No fixture for this presentation request.' }, { status: 503 });
      return originalFetch(request, options);
    };
  })()` });
  await setupWorkflowLogin('employee', { resetDemo: true });
  // API-backed computer interactions are covered by the computers-api suite.
  // Search/reset, sorting and pagination use the actual shared table component.
  for (const route of ['customers', 'products', 'inventory', 'orders', 'invoices', 'staff', 'tournaments']) {
    await go(`/admin/${route}`);
    await until('document.querySelectorAll("tbody tr").length > 0');
    const checked = { route, search: false, sort: false, pagination: false };
    const selector = '.admin-toolbar .cafe-search input';
    if (await evaluate(`Boolean(document.querySelector('${selector}'))`)) {
      await input(selector, 'zz-no-match-frontend');
      if (route === 'customers') await click('Tìm kiếm', '.admin-toolbar button');
      await until("document.querySelectorAll('tbody tr').length === 0");
      await click(route === 'customers' ? 'Xóa tìm kiếm' : 'Xóa bộ lọc');
      await until("document.querySelectorAll('tbody tr').length > 0");
      checked.search = true;
    }
    if (await evaluate("Boolean(document.querySelector('.admin-sort'))")) {
      await evaluate("document.querySelector('.admin-sort').click()");
      await until("document.querySelector('th[aria-sort]')?.getAttribute('aria-sort') === 'ascending'");
      assert.equal(await evaluate("document.querySelector('th[aria-sort]')?.getAttribute('aria-sort')"), 'ascending');
      await evaluate("document.querySelector('.admin-sort').click()");
      await until("document.querySelector('th[aria-sort]')?.getAttribute('aria-sort') === 'descending'");
      assert.equal(await evaluate("document.querySelector('th[aria-sort]')?.getAttribute('aria-sort')"), 'descending');
      checked.sort = true;
    }
    const next = '.admin-pagination [aria-label="Trang sau"]';
    if (await evaluate(`document.querySelector('${next}') && !document.querySelector('${next}').disabled`)) {
      const before = await evaluate("document.querySelector('tbody tr').innerText");
      await evaluate(`document.querySelector('${next}').click()`);
      await until(`document.querySelector('tbody tr')?.innerText !== ${JSON.stringify(before)}`);
      checked.pagination = true;
    }
    controls.push(checked);
  }
  await go('/admin/reports');
  await input('.admin-date-filter input', '2026-10-23');
  await input('.admin-date-filter label:nth-child(2) input', '2026-10-20');
  await until("Boolean(document.querySelector('[role=alert]'))");
  assert.equal(await evaluate("Boolean(document.querySelector('.admin-chart'))"), false);
  await click('7 ngày'); await until("Boolean(document.querySelector('.admin-chart'))");
  assert.deepEqual(errors, []);
  assert.ok(controls.every(row => row.search), 'All seven module search/reset controls are exercised');
  assert.ok(controls.some(row => row.sort) && controls.some(row => row.pagination), 'Sorting and pagination execute on populated tables');
  console.log(JSON.stringify({ passed: true, controls, checks: ['computers require staff login without demo fallback', 'seven admin module search/reset/sort/pagination', 'invalid report range and recovery', 'mobile controls'] }));
} finally { socket.close(); }
