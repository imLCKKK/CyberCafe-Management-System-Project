import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { cdp, evaluate, until, screenshot, errors, socket, go, outputDir } from './cdp.mjs';

const admin = ['dashboard', 'computers', 'customers', 'orders', 'products', 'inventory', 'invoices', 'tournaments', 'staff', 'reports'];
const client = ['home', 'ordering', 'wallet', 'invoices', 'account', 'tournaments'];
const results = [];
try {
  await cdp('Runtime.enable'); await cdp('Page.enable');
  await go('/client/home');
  await evaluate('localStorage.clear()');
  for (const width of [1440, 1024, 768, 390, 320]) {
    await cdp('Emulation.setDeviceMetricsOverride', { width, height: 900, deviceScaleFactor: 1, mobile: false });
    for (const [area, routes] of [['admin', admin], ['client', client]]) {
      for (const route of routes) {
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
    await go(route); await until(`location.pathname === ${JSON.stringify(target)}`);
  }
  for (const route of ['/admin/auth', '/admin/promotions', '/client/auth']) {
    await go(route, 'document.querySelector(".placeholder, .client-page-placeholder")');
    assert.ok(await evaluate("document.body.innerText.includes('CHƯA TRIỂN KHAI')"), `Document placeholder: ${route}`);
  }
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, routeViewportChecks: results.length, widths: [1440, 1024, 768, 390, 320], redirects: 4, documentedPlaceholders: 3 }));
} finally {
  await writeFile(outputDir + '/routes.json', JSON.stringify({ results, errors }, null, 2));
  socket.close();
}
