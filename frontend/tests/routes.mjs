import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { cdp, evaluate, until, screenshot, errors, socket, go, input, outputDir } from './cdp.mjs';

const admin = ['dashboard', 'computers', 'customers', 'orders', 'products', 'inventory', 'invoices', 'tournaments', 'staff', 'reports'];
const client = ['home', 'ordering', 'wallet', 'invoices', 'account', 'tournaments'];
const results = [];
try {
  await cdp('Runtime.enable'); await cdp('Page.enable');
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    window.fetch = async url => {
      const path = new URL(url, location.href).pathname;
      if (path === '/api/auth/me') return Response.json({ employeeId: 4, username: 'test-admin', role: 'Admin', permissions: ['order.view', 'computer.view', 'customer.view', 'product.view', 'stockreceipt.view', 'invoice.view', 'tournament.view', 'employee.view', 'report.view', 'promotion.manage'] });
      if (path === '/api/auth/customer/me') return Response.json({ customerId: 7, phone: '0900000007', fullName: 'Test customer' });
      return Response.json({ message: 'Unexpected request.' }, { status: 500 });
    };
  })()` });
  for (const width of [1440, 1024, 768, 390, 320]) {
    await cdp('Emulation.setDeviceMetricsOverride', { width, height: 900, deviceScaleFactor: 1, mobile: false });
    for (const [area, routes] of [['admin', admin], ['client', client]]) {
      for (const route of routes) {
        await evaluate(`localStorage.setItem("cybermanagement.auth.v1", JSON.stringify({ accessToken: "${area}-browser-test-token", accountType: "${area === 'admin' ? 'employee' : 'customer'}" }))`);
        await go(`/${area}/${route}`);
        const result = await evaluate(`({ route: location.pathname, width: innerWidth, scrollWidth: document.documentElement.scrollWidth, heading: document.querySelector('.cafe-heading h1, .home-heading h1, .tour-heading h1')?.textContent, activeLinks: document.querySelectorAll('nav a[aria-current="page"]').length, invalid: /NaN|Invalid Date|CHƯA TRIỂN KHAI/.test(document.body.innerText), brokenImages: [...document.images].filter(img => img.complete && !img.naturalWidth).map(img => img.src), unnamedButtons: [...document.querySelectorAll('button')].filter(el => !el.textContent.trim() && !el.getAttribute('aria-label') && !el.getAttribute('title')).length })`);
        results.push(result);
        await screenshot(`${area}-${route}-${width}.png`);
        assert.ok(result.heading, `Missing heading: ${area}/${route}`);
        assert.equal(result.invalid, false, `Invalid content: ${area}/${route}`);
        assert.equal(result.activeLinks, 1, `Active navigation: ${area}/${route}`);
        assert.ok(result.scrollWidth <= width, `Overflow: ${area}/${route} at ${width}: ${result.scrollWidth}`);
        assert.deepEqual(result.brokenImages, [], `Broken image: ${area}/${route}`);
        assert.equal(result.unnamedButtons, 0, `Unnamed buttons: ${area}/${route}`);
      }
    }
  }
  for (const [route, target] of [['/', '/client/home'], ['/admin', '/admin/dashboard'], ['/client', '/client/home'], ['/unknown', '/client/home']]) {
    const accountType = route === '/admin' ? 'employee' : 'customer';
    await evaluate(`localStorage.setItem("cybermanagement.auth.v1", JSON.stringify({ accessToken: "${accountType}-browser-test-token", accountType: "${accountType}" }))`);
    await go(route); await until(`location.pathname === ${JSON.stringify(target)}`);
  }
  await evaluate('localStorage.clear()');
  for (const [route, heading] of [['/admin/auth', 'Đăng nhập nhân viên'], ['/client/auth', 'Đăng nhập tài khoản']]) {
    await go(route, 'document.querySelector(".auth-form")');
    assert.equal(await evaluate('document.querySelector(".auth-heading h1")?.textContent'), heading, `Auth page: ${route}`);
    assert.equal(await evaluate('document.querySelectorAll(".auth-form input[required]").length'), 2, `Required credentials: ${route}`);
    await evaluate(`window.fetch = async (url, options) => {
      window.__authRequest = { url, body: JSON.parse(options.body) };
      await new Promise(resolve => setTimeout(resolve, 250));
      if (url === '/api/auth/login' && window.__authOutcome === 'success') {
        return new Response(JSON.stringify({ accessToken: 'browser-test-token', expiresAtUtc: new Date(Date.now() + 60000).toISOString() }), { status: 200, headers: { 'content-type': 'application/json' } });
      }
      if (url === '/api/auth/me') {
        return new Response(JSON.stringify({ employeeId: '4', username: 'waiter01', role: 'Waiter', permissions: ['order.view'] }), { status: 200, headers: { 'content-type': 'application/json' } });
      }
      return new Response(JSON.stringify({ message: 'Thông tin đăng nhập không chính xác.' }), { status: 401, headers: { 'content-type': 'application/json' } });
    }; window.__authOutcome = 'invalid';`);
    await input('#auth-identifier', route === '/admin/auth' ? 'unknown' : '0900000000');
    await input('#auth-password', 'wrong-password');
    await evaluate('document.querySelector(".auth-submit").click()');
    await until('document.querySelector(".auth-submit")?.disabled');
    assert.ok(await evaluate('document.querySelector(".auth-submit").textContent.includes("Đang đăng nhập")'), `Loading state: ${route}`);
    await until('document.querySelector("[role=alert]")?.textContent');
    assert.match(await evaluate('document.querySelector("[role=alert]").textContent'), /không chính xác/i, `Login error: ${route}`);
    const request = await evaluate('window.__authRequest');
    assert.equal(request.url, route === '/admin/auth' ? '/api/auth/login' : '/api/auth/customer/login');
    assert.equal(request.body.password, 'wrong-password');
  }
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await evaluate(`window.fetch = async (url) => {
    if (url === '/api/auth/login') {
      return new Response(JSON.stringify({ accessToken: 'browser-test-token', expiresAtUtc: new Date(Date.now() + 60000).toISOString() }), { status: 200, headers: { 'content-type': 'application/json' } });
    }
    if (url === '/api/auth/me') {
      return new Response(JSON.stringify({ employeeId: '4', username: 'waiter01', role: 'Waiter', permissions: ['order.view'] }), { status: 200, headers: { 'content-type': 'application/json' } });
    }
    return new Response(JSON.stringify({ message: 'Unexpected request.' }), { status: 500, headers: { 'content-type': 'application/json' } });
  };`);
  await input('#auth-identifier', 'waiter01');
  await input('#auth-password', 'valid-password');
  await evaluate('document.querySelector(".auth-submit").click()');
  await until('location.pathname === "/admin/orders"');
  assert.equal(await evaluate('document.querySelectorAll(".nav-item").length'), 2, 'Only dashboard and permitted order navigation are shown');
  await evaluate('history.pushState({}, "", "/admin/customers"); dispatchEvent(new PopStateEvent("popstate"))');
  await until('location.pathname === "/admin/orders"');
  assert.equal(await evaluate('location.pathname'), '/admin/orders', 'A route without permission returns to an accessible page');
  await evaluate('history.pushState({}, "", "/admin/reports"); dispatchEvent(new PopStateEvent("popstate"))');
  await until('location.pathname === "/admin/orders"');
  await evaluate('history.pushState({}, "", "/admin/dashboard"); dispatchEvent(new PopStateEvent("popstate"))');
  await until('Boolean(document.querySelector(".cafe-page")) && location.pathname === "/admin/dashboard"');
  const dashboardText = await evaluate('document.querySelector(".cafe-page").innerText');
  assert.equal(dashboardText.includes('Doanh thu'), false, 'Dashboard revenue metrics are hidden without report.view');
  assert.equal(dashboardText.includes('tiền mặt đơn món chờ thu'), false, 'Cash due is hidden without report.view');
  assert.equal(await evaluate('[...document.querySelectorAll(".cafe-order-info small")].some(element => element.textContent.includes(" · "))'), false, 'Recent order totals are hidden without report.view');
  await evaluate('localStorage.clear()');
  await evaluate('localStorage.setItem("cybermanagement.auth.v1", JSON.stringify({ accessToken: "expired-token", accountType: "employee", expiresAtUtc: "2000-01-01T00:00:00.000Z" }))');
  await go('/admin/auth', 'document.querySelector(".auth-form")');
  await until('Boolean(document.querySelector(".auth-notice"))');
  assert.match(await evaluate('document.querySelector(".auth-notice").textContent'), /hết hạn/i, 'Expired stored session is reported');
  await evaluate('localStorage.clear()');
  await go('/client/auth', 'document.querySelector(".auth-form")');
  await evaluate('localStorage.setItem("cybermanagement.auth.v1", JSON.stringify({ accessToken: "employee-browser-test-token", accountType: "employee" }))');
  await go('/admin/promotions', 'document.querySelector(".placeholder")');
  assert.ok(await evaluate("document.body.innerText.includes('CHƯA TRIỂN KHAI')"), 'Promotion placeholder remains documented');
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, routeViewportChecks: results.length, widths: [1440, 1024, 768, 390, 320], redirects: 4, authForms: 2, permissionGuard: true, expiredSession: true, documentedPlaceholders: 1 }));
} finally {
  await writeFile(outputDir + '/routes.json', JSON.stringify({ results, errors }, null, 2));
  socket.close();
}
