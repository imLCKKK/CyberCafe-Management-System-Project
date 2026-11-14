import assert from 'node:assert/strict';
import { cdp, evaluate, until, screenshot, errors, socket, sleep, origin, setupWorkflowLogin, clearDemoStorage, press } from './cdp.mjs';
import { createClientDemo } from '../src/client/shared/demoData.js';

const key = 'tram.client.dbml.demo.v2';
const data = () => evaluate(`JSON.parse(localStorage.getItem('${key}'))`);
const demo = createClientDemo();
const catalog = {
  categories: demo.product_categories.map(row => ({ id: row.id, name: row.name, isActive: true })),
  products: demo.products.map(row => ({ id: row.id, name: row.name, categoryId: row.category_id, currentPrice: row.current_price, stockQuantity: row.stock_quantity, isActive: true })),
};

async function openSuggestion() {
  await until('Boolean(document.querySelector(".today-teaser"))');
  await evaluate('document.querySelector(".today-teaser").click()');
  await until('Boolean(document.querySelector(".today-experience-actions .primary"))');
}

async function resultDialog() {
  await until('Boolean(document.querySelector("dialog.today-result-dialog[open]"))');
  await evaluate('Promise.all(document.querySelector("dialog").getAnimations().map(animation => animation.finished.catch(() => {}))).then(() => true)');
}

try {
  await cdp('Runtime.enable');
  await cdp('Page.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1360, height: 1000, deviceScaleFactor: 1, mobile: false });
  await cdp('Page.navigate', { url: origin + '/client/auth' });
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const originalFetch = window.fetch.bind(window);
    Math.random = () => 0;
    window.fetch = async (request, options) => {
      const url = new URL(typeof request === 'string' ? request : request.url, location.href);
      if (url.pathname === '/api/menu') { window.__suggestionMenuRequests = (window.__suggestionMenuRequests || 0) + 1; return Response.json(${JSON.stringify(catalog)}); }
      if (url.pathname.startsWith('/hubs/realtime')) return new Response(null, { status: 503 });
      if (url.pathname.startsWith('/api/') && !url.pathname.startsWith('/api/auth/')) return Response.json({ message: 'No fixture for this presentation request.' }, { status: 503 });
      return originalFetch(request, options);
    };
  })()` });
  await setupWorkflowLogin('customer', { resetDemo: true });
  await clearDemoStorage();
  await cdp('Page.navigate', { url: origin + '/client/ordering' });
  await openSuggestion();
  const before = await data();
  await evaluate('document.querySelector(".today-experience-actions .primary").focus(); document.querySelector(".today-experience-actions .primary").click()');
  const offset = () => evaluate('new DOMMatrixReadOnly(getComputedStyle(document.querySelector(".today-wheel-track")).transform).m41');
  await sleep(350); const first = await offset();
  await sleep(1000); const second = await offset();
  await sleep(1000); const third = await offset();
  assert.ok(first < 0 && second < first && third < second, 'Wheel moves forward');
  assert.ok(Math.abs(second - first) > Math.abs(third - second), 'Wheel decelerates');
  await resultDialog();
  const winner = await evaluate('document.querySelector(".today-prize-hero h3").textContent');
  assert.equal(await evaluate('document.querySelector(".today-outcome > strong").textContent'), winner, 'Wheel outcome and result dialog name the same product');
  const product = catalog.products.find(row => row.name === winner);
  assert.ok(product, 'Result comes from the live-shaped menu fixture');
  await screenshot('suggestion-result-desktop.png');

  await evaluate('document.querySelector(".today-prize-quantity .home-stepper button:last-child").click()');
  await until('document.querySelector(".today-prize-quantity .home-stepper > span").textContent === "2"');
  assert.deepEqual(await data(), before, 'Choosing a quantity does not create an order or change wallet data');
  await press('Escape');
  await until('!document.querySelector("dialog[open]")');
  // Catalog refresh on window focus can reset a previous winner; another spin must remain usable.
  await cdp('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-reduced-motion', value: 'reduce' }] });
  await until('Boolean(document.querySelector(".today-experience-actions .primary:not(:disabled)"))');
  await evaluate('document.querySelector(".today-experience-actions .primary").click()');
  await resultDialog();
  assert.equal(await evaluate('document.querySelector(".today-prize-hero h3").textContent'), winner, 'The next spin recovers after Escape using the deterministic fixture choice');
  await evaluate('document.querySelector(".today-prize-quantity .home-stepper button:last-child").click()');
  await until('document.querySelector(".today-prize-quantity .home-stepper > span").textContent === "2"');
  await evaluate('document.querySelector(".today-prize-checkout > .primary").click()');
  await until('!document.querySelector("dialog[open]") && document.querySelector(".order-cart .client-count").textContent === "2"');
  assert.ok(await evaluate(`document.querySelector('.order-cart-items').textContent.includes(${JSON.stringify(winner)})`), 'Selected product reaches the cart');
  assert.equal(await evaluate('document.querySelector(".order-cart-body > .primary").disabled'), true, 'Current online ordering remains explicitly unavailable');
  assert.deepEqual(await data(), before, 'Cart handoff does not debit the wallet, reserve stock or create orders');
  await screenshot('suggestion-cart-handoff-desktop.png');

  // Fill the cart to the fixture stock limit without invoking the unavailable checkout flow.
  await until('Boolean(document.querySelector(".today-experience-actions .primary:not(:disabled)"))');
  await evaluate('document.querySelector(".today-experience-actions .primary").click()');
  await resultDialog();
  for (let index = 0; index < 99; index++) {
    if (await evaluate('document.querySelector(".today-prize-quantity .home-stepper button:last-child").disabled')) break;
    await evaluate('document.querySelector(".today-prize-quantity .home-stepper button:last-child").click()');
    await sleep(10);
  }
  assert.equal(await evaluate('Number(document.querySelector(".today-prize-quantity .home-stepper > span").textContent)'), product.stockQuantity - 2, 'Result quantity respects stock already selected in the cart');
  await evaluate('document.querySelector(".today-prize-checkout > .primary").click()');
  await until(`document.querySelector('.order-cart .client-count').textContent === '${product.stockQuantity}'`);
  assert.deepEqual(await data(), before, 'Selecting the final available portion still leaves business data unchanged');

  await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: false });
  await cdp('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-reduced-motion', value: 'reduce' }] });
  await cdp('Page.navigate', { url: origin + '/client/ordering' });
  await openSuggestion();
  await evaluate('document.querySelector(".today-experience-actions .primary").click()');
  await resultDialog();
  assert.ok(await evaluate('(() => { const dialog = document.querySelector("dialog"); const box = dialog.getBoundingClientRect(); return box.left >= 0 && box.right <= document.documentElement.clientWidth && box.top >= 0 && box.bottom <= innerHeight && dialog.scrollWidth <= dialog.clientWidth; })()'), 'Mobile result dialog fits the available viewport');
  await screenshot('suggestion-result-mobile-top.png');
  await evaluate('document.querySelector("dialog").scrollTop = document.querySelector("dialog").scrollHeight');
  await screenshot('suggestion-result-mobile-quantity.png');
  for (let index = 0; index < 10; index++) {
    await press('Tab');
    const focused = await evaluate('({ tag: document.activeElement.tagName, label: document.activeElement.getAttribute("aria-label") || document.activeElement.textContent.slice(0,80), inDialog: Boolean(document.activeElement.closest("dialog")), dialogOpen: Boolean(document.querySelector("dialog[open]")), menuRequests: window.__suggestionMenuRequests, documentFocused: document.hasFocus() })');
    assert.ok(focused.dialogOpen && (!['BUTTON', 'INPUT', 'A', 'SELECT', 'TEXTAREA'].includes(focused.tag) || focused.inDialog), `Dialog stays open and focus must not reach background controls: ${JSON.stringify({ index, ...focused })}`);
  }
  await evaluate('document.querySelector("dialog button").focus()');
  for (const type of ['keyDown', 'keyUp']) await cdp('Input.dispatchKeyEvent', { type, key: 'Tab', code: 'Tab', windowsVirtualKeyCode: 9, nativeVirtualKeyCode: 9, modifiers: 8 });
  assert.equal(await evaluate('document.activeElement === document.querySelector(".today-prize-checkout > .primary")'), true, 'Shift+Tab wraps from the first to the last enabled dialog control');
  await press('Escape');
  await until('!document.querySelector("dialog[open]")');
  assert.deepEqual(await data(), before);
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, checks: ['forward decelerating wheel', 'matching result and menu product', 'quantity, Escape and next-spin recovery', 'cart handoff without wallet/order changes', 'stock cap includes cart quantity', 'online checkout remains unavailable', 'reduced-motion mobile dialog and keyboard focus'] }));
} finally {
  socket.close();
}
