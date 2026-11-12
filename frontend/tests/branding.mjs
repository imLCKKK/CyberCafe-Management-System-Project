import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { cdp, evaluate, until, screenshot, errors, socket, go, press, outputDir } from './cdp.mjs';
import { computerFixture } from './computer-fixture.mjs';
import { createClientDemo } from '../src/client/shared/demoData.js';

// These HTTP fixtures exercise presentation and keyboard behavior, not SQL or realtime acceptance.
const baseline = process.env.NEXORA_BASELINE === '1';
const computers = computerFixture();
const demo = createClientDemo(new Date('2026-11-09T05:00:00.000Z'));
const catalog = {
  categories: demo.product_categories.map(row => ({ id: row.id, name: row.name, isActive: true })),
  products: demo.products.map(row => ({ id: row.id, name: row.name, categoryId: row.category_id, currentPrice: row.current_price, stockQuantity: row.stock_quantity, isActive: true })),
};
const employee = { employeeId: 4, username: 'branding-review', role: 'Admin', permissions: ['order.view', 'computer.view', 'computer.status.update', 'customer.view', 'product.view', 'stockreceipt.view', 'invoice.view', 'tournament.view', 'employee.view', 'report.view'] };
const customer = { customerId: 7, phone: '0900000007', fullName: 'Người chơi NEXORA' };
const wallet = { customerId: 7, balance: 153000, page: 1, pageSize: 20, totalCount: 2, transactions: [
  { transactionId: 2, type: 'Debit', amount: -28000, balanceAfter: 153000, description: 'Thanh toán món', createdAtUtc: '2026-11-09T04:00:00Z' },
  { transactionId: 1, type: 'Credit', amount: 181000, balanceAfter: 181000, description: 'Nạp tại quầy', createdAtUtc: '2026-11-09T03:00:00Z' },
] };
const routes = ['/admin/dashboard', '/admin/computers', '/admin/invoices', '/client/home', '/client/ordering', '/client/wallet', '/client/invoices', '/client/tournaments'];
const observations = [];

async function credentials(area) {
  await evaluate(`localStorage.setItem('cybermanagement.auth.v1', JSON.stringify({ accessToken: 'branding-${area}', accountType: '${area === 'admin' ? 'employee' : 'customer'}' }))`);
}

async function inspectPage(route, width) {
  await until('[...document.images].every(image => image.complete)');
  const observation = await evaluate(`(() => {
    const nav = document.querySelector('aside nav');
    return {
      route: location.pathname,
      width: innerWidth,
      clientWidth: document.documentElement.clientWidth,
      scrollWidth: document.documentElement.scrollWidth,
      title: document.title,
      heading: document.querySelector('.cafe-heading h1, .home-heading h1, .tour-heading h1, .auth-heading h1')?.textContent,
      oldBrand: /\\b(?:TRAM|NET CAFE|Net Cafe)\\b/.test(document.body.innerText),
      sidebarBrand: document.querySelector('aside .nexora-brand')?.textContent,
      headerBrand: document.querySelector('header .nexora-brand')?.textContent,
      activeLinks: nav?.querySelectorAll('[aria-current="page"]').length,
      brokenImages: [...document.images].filter(image => !image.naturalWidth).map(image => image.src),
      invalid: /NaN|Invalid Date/.test(document.body.innerText),
      narrowOrderText: [...document.querySelectorAll('.cafe-order-info')].filter(element => element.getBoundingClientRect().width < 100).length,
      favicon: document.querySelector('link[rel="icon"]')?.getAttribute('href'),
    };
  })()`);
  observations.push(observation);
  await screenshot(`${baseline ? 'before' : 'after'}-${route.slice(1).replaceAll('/', '-')}-${width}.png`);
  if (baseline) return;
  assert.equal(observation.route, route);
  assert.ok(observation.heading, `Missing page heading: ${route}`);
  assert.ok(observation.scrollWidth <= observation.clientWidth + 1, `Page overflows at ${width}px: ${route} (${observation.scrollWidth}px content, ${observation.clientWidth}px available)`);
  assert.match(observation.title, /NEXORA CYBER/i);
  assert.equal(observation.oldBrand, false, `Old brand remains visible: ${route}`);
  assert.deepEqual(observation.brokenImages, [], `Broken brand/image: ${route}`);
  assert.equal(observation.invalid, false, `Invalid visible data: ${route}`);
  assert.equal(observation.narrowOrderText, 0, `Order descriptions need readable space: ${route} at ${width}px`);
  assert.match(observation.favicon, /branding\//);
  if (!route.endsWith('/auth')) {
    assert.match(observation.sidebarBrand, /NEXORA/);
    assert.match(observation.headerBrand, /NEXORA/);
    assert.equal(observation.activeLinks, 1, `Active navigation: ${route}`);
    const area = route.startsWith('/admin/') ? 'Quản trị' : 'Khách hàng';
    assert.ok((observation.sidebarBrand + observation.headerBrand).includes(area), `Area label missing: ${route}`);
  }
}

async function checkNavigationKeyboard(area) {
  const route = area === 'admin' ? '/admin/dashboard' : '/client/home';
  await credentials(area);
  await go(route);
  await evaluate(`document.querySelector('aside nav a').focus()`);
  await press('Tab');
  assert.equal(await evaluate(`document.activeElement === document.querySelectorAll('aside nav a')[1]`), true, `${area}: Tab follows the navigation order`);
  const last = await evaluate(`(() => {
    const link = [...document.querySelectorAll('aside nav a')].at(-1);
    link.focus(); link.scrollIntoView({ block: 'nearest', inline: 'nearest' });
    const bounds = link.getBoundingClientRect();
    return { focused: document.activeElement === link, left: bounds.left, right: bounds.right, width: innerWidth };
  })()`);
  assert.equal(last.focused, true, `${area}: last navigation link is focusable`);
  assert.ok(last.left >= 0 && last.right <= last.width + 1, `${area}: focused navigation is visible`);
}

async function activateFocusedButton() {
  // Include the text event Chromium uses for native Enter activation; do not synthesize a click.
  await cdp('Input.dispatchKeyEvent', { type: 'keyDown', key: 'Enter', code: 'Enter', windowsVirtualKeyCode: 13, nativeVirtualKeyCode: 13, text: '\r', unmodifiedText: '\r' });
  await cdp('Input.dispatchKeyEvent', { type: 'keyUp', key: 'Enter', code: 'Enter', windowsVirtualKeyCode: 13, nativeVirtualKeyCode: 13 });
}

async function checkDialogKeyboard() {
  await credentials('client');
  await go('/client/home');
  await evaluate(`(() => { const button = document.querySelector('[aria-label="Thông báo"]'); button.focus(); button.click(); })()`);
  await until('Boolean(document.querySelector("dialog[open]"))');
  assert.equal(await evaluate('document.querySelector("dialog[open]").contains(document.activeElement)'), true, 'Dialog receives focus');
  // Native modal content must make the rest of the page inert to keyboard/programmatic focus.
  await evaluate('document.querySelector("aside nav a").focus()');
  assert.equal(await evaluate('document.querySelector("dialog[open]").contains(document.activeElement)'), true, 'Background navigation cannot take modal focus');
  await press('Tab');
  await press('Tab');
  assert.equal(await evaluate('document.activeElement.closest("aside, header") !== null'), false, 'Tab stays out of background controls');
  const bounds = await evaluate(`(() => { const box = document.querySelector('dialog[open]').getBoundingClientRect(); return { left: box.left, right: box.right, top: box.top, bottom: box.bottom, width: innerWidth, height: innerHeight }; })()`);
  assert.ok(bounds.left >= 0 && bounds.right <= bounds.width && bounds.top >= 0 && bounds.bottom <= bounds.height, 'Dialog fits mobile viewport');
  await screenshot('after-client-dialog-320.png');
  await press('Escape');
  await until('!document.querySelector("dialog[open]")');
  assert.equal(await evaluate('document.activeElement.getAttribute("aria-label")'), 'Thông báo', 'Escape restores focus to the trigger');
}

async function checkTableKeyboard() {
  await credentials('client');
  await go('/client/invoices');
  const before = await evaluate(`(() => {
    const region = document.querySelector('.invoice-table-wrap[role="region"][tabindex="0"]');
    if (!region) throw new Error('Invoice table needs a named, keyboard-focusable scroll region');
    region.focus();
    region.scrollIntoView({ block: 'center' });
    return { focused: document.activeElement === region, named: Boolean(region.getAttribute('aria-label')), scrollLeft: region.scrollLeft, scrollWidth: region.scrollWidth, clientWidth: region.clientWidth };
  })()`);
  assert.equal(before.focused, true, 'Invoice scroll region receives keyboard focus');
  assert.equal(before.named, true, 'Invoice scroll region has an accessible name');
  assert.ok(before.scrollWidth > before.clientWidth, 'Mobile invoice table scrolls within its own region');
  for (let step = 0; step < 4; step++) {
    for (const type of ['keyDown', 'keyUp']) {
      await cdp('Input.dispatchKeyEvent', { type, key: 'ArrowRight', code: 'ArrowRight', windowsVirtualKeyCode: 39, nativeVirtualKeyCode: 39 });
    }
  }
  await until(`document.querySelector('.invoice-table-wrap').scrollLeft > ${before.scrollLeft}`);
  assert.equal(await evaluate('document.documentElement.scrollLeft'), 0, 'Arrow keys scroll the table instead of the page');
  await screenshot('after-client-invoice-keyboard-320.png');
}

try {
  await cdp('Runtime.enable');
  await cdp('Page.enable');
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const originalFetch = window.fetch.bind(window);
    const fixtures = ${JSON.stringify({ '/api/auth/me': employee, '/api/auth/customer/me': customer, '/api/admin/computers': computers.computers, '/api/admin/computer-types': computers.types, '/api/admin/computer-rooms': computers.rooms, '/api/menu': catalog })};
    window.fetch = async (request, options) => {
      const url = new URL(typeof request === 'string' ? request : request.url, location.href);
      if (url.pathname.startsWith('/api/')) (window.__brandingRequests ||= []).push(url.pathname);
      if (fixtures[url.pathname]) return Response.json(fixtures[url.pathname]);
      if (url.pathname === '/api/customers/me/wallet') return Response.json(${JSON.stringify(wallet)});
      if (url.pathname.startsWith('/hubs/realtime')) return new Response(null, { status: 503 });
      if (url.pathname.startsWith('/api/')) return Response.json({ message: 'Presentation fixture has no response for this request.' }, { status: 503 });
      return originalFetch(request, options);
    };
  })()` });
  await evaluate('localStorage.clear()');
  for (const width of [1440, 1024, 390, 320]) {
    await cdp('Emulation.setDeviceMetricsOverride', { width, height: 900, deviceScaleFactor: 1, mobile: false });
    for (const route of routes) {
      await credentials(route.startsWith('/admin/') ? 'admin' : 'client');
      await go(route);
      if (route === '/admin/computers') await until('document.querySelectorAll(".cafe-station").length === 4');
      if (route === '/client/wallet') await until('document.querySelectorAll(".customer-wallet-history tbody tr").length === 2');
      await inspectPage(route, width);
      if (!baseline && route === '/admin/computers') {
        const statusLabels = await evaluate('[...document.querySelectorAll(".cafe-station .cafe-badge")].map(badge => badge.textContent.trim())');
        assert.equal(new Set(statusLabels).size, 4, 'Machine states keep distinct readable labels alongside their colors');
      }
    }
    for (const area of ['admin', 'client']) {
      await evaluate('localStorage.removeItem("cybermanagement.auth.v1")');
      await go(`/${area}/auth`, 'document.querySelector(".auth-form")');
      await inspectPage(`/${area}/auth`, width);
      if (!baseline) {
        assert.equal(await evaluate('[...document.querySelectorAll(".auth-form input[required]")].every(input => input.getBoundingClientRect().width >= 150 && parseFloat(getComputedStyle(input).fontSize) >= 14)'), true, 'Login fields remain readable and usable');
      }
    }
  }
  if (!baseline) {
    await cdp('Emulation.setDeviceMetricsOverride', { width: 1024, height: 900, deviceScaleFactor: 1, mobile: false });
    await credentials('admin');
    await go('/admin/dashboard');
    const expandedWidth = await evaluate('document.querySelector("aside").getBoundingClientRect().width');
    await evaluate('document.querySelector("[aria-controls=admin-navigation]").focus()');
    await activateFocusedButton();
    await until(`document.querySelector('aside').getBoundingClientRect().width < ${expandedWidth}`);
    await screenshot('after-admin-menu-collapsed-1024.png');
    await activateFocusedButton();
    await until(`document.querySelector('aside').getBoundingClientRect().width === ${expandedWidth}`);
    assert.ok(await evaluate('document.querySelector("aside nav a span").getBoundingClientRect().width > 0'), 'Expanded menu restores visible labels at 1024px');
    await screenshot('after-admin-menu-expanded-1024.png');
    await cdp('Emulation.setDeviceMetricsOverride', { width: 320, height: 900, deviceScaleFactor: 1, mobile: false });
    await checkNavigationKeyboard('admin');
    const toggle = await evaluate(`document.querySelector('[aria-controls="admin-navigation"]')?.getAttribute('aria-expanded')`);
    assert.ok(toggle !== undefined, 'Admin menu exposes its expanded state');
    await evaluate('document.querySelector("[aria-controls=admin-navigation]").focus()');
    assert.equal(await evaluate('document.activeElement === document.querySelector("[aria-controls=admin-navigation]")'), true, 'Menu toggle receives keyboard focus');
    await activateFocusedButton();
    await until(`document.querySelector('[aria-controls="admin-navigation"]').getAttribute('aria-expanded') !== ${JSON.stringify(toggle)}`);
    assert.notEqual(await evaluate('document.querySelector("[aria-controls=admin-navigation]").getAttribute("aria-expanded")'), toggle, 'Keyboard toggles the admin menu');
    await activateFocusedButton();
    await until(`document.querySelector('[aria-controls="admin-navigation"]').getAttribute('aria-expanded') === ${JSON.stringify(toggle)}`);
    await checkNavigationKeyboard('client');
    await checkDialogKeyboard();
    await checkTableKeyboard();
    const assets = await evaluate(`Promise.all(['/branding/nexora-cyber.png', document.querySelector('link[rel="icon"]').getAttribute('href')].map(async url => ({ url, status: (await fetch(url)).status })))`);
    assert.ok(assets.every(asset => asset.status === 200), `Brand assets load: ${JSON.stringify(assets)}`);
    assert.deepEqual(errors, []);
  }
  console.log(JSON.stringify({ passed: true, baseline, viewportChecks: observations.length, widths: [1440, 1024, 390, 320], keyboard: !baseline }));
} catch (error) {
  console.error('Branding failure state:', await evaluate('JSON.stringify({url:location.href, body:document.body.innerHTML.slice(0,3000), requests:window.__brandingRequests, resources:performance.getEntriesByType("resource").slice(-8).map(entry=>({name:entry.name,status:entry.responseStatus}))})'));
  await screenshot('branding-timeout.png');
  throw error;
} finally {
  await writeFile(`${outputDir}/branding.json`, JSON.stringify({ baseline, observations, errors }, null, 2));
  socket.close();
}
