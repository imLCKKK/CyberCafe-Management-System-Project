import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, input, press } from './cdp.mjs';
try {
  await cdp('Runtime.enable'); await cdp('Page.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 900, deviceScaleFactor: 1, mobile: false });
  await go('/admin/computers'); await evaluate('localStorage.clear()'); await go('/admin/computers');
  await click('Thêm máy tính');
  await input('dialog input[name=ComputerName]', 'PC-RETEST');
  await click('Thêm máy', 'dialog button');
  await until("!document.querySelector('dialog[open]')");
  await input('[aria-label="Tìm kiếm tên máy"]', 'PC-RETEST');
  await until("document.querySelectorAll('.cafe-station').length === 1");
  await evaluate("document.querySelector('.cafe-station').click()");
  await until("Boolean(document.querySelector('dialog[open] .cafe-computer-detail'))");
  await click('Chuyển bảo trì', 'dialog button');
  await until("!document.querySelector('dialog[open]')");
  await go('/admin/computers');
  await input('[aria-label="Tìm kiếm tên máy"]', 'PC-RETEST');
  await until("document.querySelector('.cafe-station')?.classList.contains('maintenance')");
  await evaluate("document.querySelector('[aria-label=\"Dạng bảng\"]').click()");
  await click('Chi tiết', 'tbody button');
  await until("Boolean(document.querySelector('dialog[open] .cafe-computer-detail'))");
  await click('Hoàn tất bảo trì', 'dialog button');
  await until("!document.querySelector('dialog[open]')");
  await click('Thêm máy tính'); await input('dialog input[name=ComputerName]', 'pc-retest');
  await click('Thêm máy', 'dialog button');
  await until("document.querySelector('dialog [role=alert]')?.textContent.includes('đã tồn tại')");
  await press('Escape');
  await input('[aria-label="Tìm kiếm tên máy"]', 'no-result');
  await until("Boolean(document.querySelector('.cafe-empty'))");
  await click('Đặt lại bộ lọc');
  await until("document.querySelectorAll('tbody tr').length === 51");
  // Search/reset, sorting and pagination use the actual shared table component.
  for (const route of ['customers', 'products', 'inventory', 'orders', 'invoices', 'staff', 'tournaments']) {
    await go(`/admin/${route}`);
    const selector = '.admin-toolbar .cafe-search input';
    if (await evaluate(`Boolean(document.querySelector('${selector}'))`)) {
      await input(selector, 'zz-no-match-frontend');
      await until("document.querySelectorAll('tbody tr').length === 0");
      await click('Xóa bộ lọc');
      await until("document.querySelectorAll('tbody tr').length > 0");
    }
    if (await evaluate("Boolean(document.querySelector('.admin-sort'))")) {
      await evaluate("document.querySelector('.admin-sort').click()");
      assert.equal(await evaluate("document.querySelector('th[aria-sort]')?.getAttribute('aria-sort')"), 'ascending');
      await evaluate("document.querySelector('.admin-sort').click()");
      assert.equal(await evaluate("document.querySelector('th[aria-sort]')?.getAttribute('aria-sort')"), 'descending');
    }
    const next = '.admin-pagination [aria-label="Trang sau"]';
    if (await evaluate(`document.querySelector('${next}') && !document.querySelector('${next}').disabled`)) {
      const before = await evaluate("document.querySelector('tbody tr').innerText");
      await evaluate(`document.querySelector('${next}').click()`);
      await until(`document.querySelector('tbody tr')?.innerText !== ${JSON.stringify(before)}`);
    }
  }
  await go('/admin/reports');
  await input('.admin-date-filter input', '2026-10-23');
  await input('.admin-date-filter label:nth-child(2) input', '2026-10-20');
  await until("Boolean(document.querySelector('[role=alert]'))");
  assert.equal(await evaluate("Boolean(document.querySelector('.admin-chart'))"), false);
  await click('7 ngày'); await until("Boolean(document.querySelector('.admin-chart'))");
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, checks: ['computer create/maintenance/persistence/list view/duplicate/empty reset', 'seven admin module search/reset/sort/pagination', 'invalid report range and recovery', 'mobile controls'] }));
} finally { socket.close(); }
