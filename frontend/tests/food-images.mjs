import assert from 'node:assert/strict';
import { cdp, evaluate, until, screenshot, socket, errors, go, input, click, setupWorkflowLogin, press } from './cdp.mjs';

// Presentation-only fixtures: these checks do not claim SQL/API integration.
const categories = [{ id: 71, name: 'Menu kiểm tra ảnh', isActive: true }];
const product = (id, name, imageUrl, stockQuantity = 12) => ({ id, name, categoryId: 71, currentPrice: 38000, stockQuantity, isActive: true, imageUrl });
const products = [
  product(901, 'Cơm gà xối mỡ', '/food/com-ga-xoi-mo-v1.640.jpg'),
  { ...product(902, 'Mì cay hải sản', '/food/mi-cay-hai-san-v1.640.jpg'), currentPrice: 45000 },
  { ...product(903, 'Trà đào cam sả', '/food/tra-dao-cam-sa-v1.640.jpg'), currentPrice: 22000 },
  product(904, 'Ảnh bị lỗi', '/food/missing-food-image-test.png'),
  product(1, 'Món mới chưa có ảnh', null),
  product(906, 'Món hết hàng', null, 0),
  ...Array.from({ length: 40 }, (_, index) => product(1000 + index, `Món danh sách dài ${index + 1}`, null)),
];
const imageRequests = [];
socket.addEventListener('message', ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Network.requestWillBeSent' && message.params.type === 'Image') imageRequests.push(message.params.request.url);
});

try {
  await cdp('Page.enable'); await cdp('Runtime.enable'); await cdp('Network.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  await cdp('Emulation.setEmulatedMedia', { features: [{ name: 'prefers-reduced-motion', value: 'reduce' }] });
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const original = window.fetch.bind(window);
    window.__foodMenu = ${JSON.stringify({ categories, products })};
    Math.random = () => 0;
    window.fetch = async (request, options) => {
      const path = new URL(typeof request === 'string' ? request : request.url, location.href).pathname;
      if (path === '/api/menu') return Response.json(window.__foodMenu);
      if (path.startsWith('/hubs/realtime')) return new Response(null, { status: 503 });
      if (path.startsWith('/api/') && !path.startsWith('/api/auth/')) return Response.json({ message: 'Presentation fixture only' }, { status: 503 });
      return original(request, options);
    };
  })()` });
  await setupWorkflowLogin('customer', { resetDemo: true });
  await until('document.querySelectorAll(".home-food-section article").length === 3');
  const baseline = process.env.FOOD_IMAGE_BASELINE === '1';
  const suffix = baseline ? 'before' : 'after';
  if (!baseline) await until('[...document.querySelectorAll(".home-food-section img")].filter(img => img.complete && img.naturalWidth > 0).length === 3');
  await screenshot(`food-images-home-${suffix}.png`);
  await evaluate('document.querySelector(".home-food-section .home-add").click()');
  await until('document.querySelectorAll(".ordering-food-grid article").length === 46 && document.querySelectorAll(".order-cart-line").length === 1');
  if (!baseline) {
    await until('document.querySelector(".ordering-food-grid article img")?.naturalWidth > 0');
    assert.equal(await evaluate('document.querySelector(".ordering-food-grid article img").getAttribute("src")'), products[0].imageUrl);
    assert.match(await evaluate('document.querySelector(".ordering-food-grid article img").alt'), /Cơm gà xối mỡ/);
    assert.match(await evaluate('document.querySelector(".ordering-food-grid article img").currentSrc'), /\.320\.jpg$/);
    assert.equal(await evaluate('document.querySelector(".order-cart-line img").getAttribute("src")'), products[0].imageUrl);
    assert.equal(await evaluate('document.querySelectorAll(".ordering-food-grid article")[5].querySelector("button").disabled'), true);
    await evaluate('document.querySelectorAll(".ordering-food-grid article")[3].scrollIntoView()');
    await until('Boolean(document.querySelectorAll(".ordering-food-grid article")[3].querySelector(".food-art-placeholder"))');
    assert.equal(await evaluate('document.querySelectorAll(".ordering-food-grid article")[3].querySelector("img")'), null);
    assert.equal(await evaluate('document.querySelectorAll(".ordering-food-grid article")[4].querySelector("img")'), null, 'A real SQL ID must not pick an old demo emoji/photo');
    assert.equal(imageRequests.filter(url => url.endsWith('/food/missing-food-image-test.png')).length, 1, 'Broken source is not retried in a loop');
    await evaluate('window.scrollTo(0, 0)');
  }
  await screenshot(`food-images-menu-${suffix}.png`);
  await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 844, deviceScaleFactor: 1, mobile: true });
  await evaluate('window.scrollTo(0, 0)');
  assert.equal(await evaluate('document.documentElement.scrollWidth <= window.innerWidth'), true, 'Menu fits mobile viewport');
  await evaluate('document.querySelector(".ordering-food-grid article").scrollIntoView({ block: "start" })');
  await screenshot(`food-images-mobile-${suffix}.png`);
  if (!baseline) {
    await input('input[placeholder="Tìm món bạn thích…"]', 'Cơm gà');
    await until('document.querySelectorAll(".ordering-food-grid article").length === 1');
    await input('input[placeholder="Tìm món bạn thích…"]', '');
    await until('document.querySelector(".today-teaser-food img")?.naturalWidth > 0');
    assert.equal(await evaluate('document.querySelector(".today-teaser-food img").getAttribute("src")'), products[0].imageUrl);
    await click('Hôm nay ăn gì?');
    await until('document.querySelector(".today-wheel-card img")?.naturalWidth > 0');
    assert.equal(await evaluate('document.querySelector(".today-wheel-card img").getAttribute("src")'), products[0].imageUrl);
    assert.equal(await evaluate(`(() => {
      const card = document.querySelector('.today-wheel-card').getBoundingClientRect();
      const image = document.querySelector('.today-wheel-card img').getBoundingClientRect();
      const price = document.querySelector('.today-wheel-card small').getBoundingClientRect();
      return image.top >= card.top && image.bottom <= price.top && price.bottom <= card.bottom;
    })()`), true, 'Wheel image, name and price fit the existing card');
    await evaluate('document.querySelector(".today-experience").scrollIntoView({ block: "start" })');
    await screenshot('food-images-wheel-mobile-after.png');
    await click('Quay chọn món ngay');
    await until('Boolean(document.querySelector("dialog.today-result-dialog[open] img"))');
    await until('document.querySelector("dialog img").complete && document.querySelector("dialog img").naturalWidth > 0');
    assert.equal(await evaluate('document.querySelector("dialog img").getAttribute("src")'), products[0].imageUrl);
    await screenshot('food-images-result-mobile-after.png');
    await press('Escape');
    await until('!document.querySelector("dialog") && Boolean(document.querySelector(".today-back"))');
    await evaluate('document.querySelector(".today-back").click()');
    // A refreshed URL replaces a failed image without changing cart quantities.
    await evaluate(`window.__foodMenu.products[3].imageUrl = ${JSON.stringify(products[1].imageUrl)}; window.dispatchEvent(new Event('focus'))`);
    await until('document.querySelectorAll(".ordering-food-grid article").length === 46');
    await evaluate('document.querySelectorAll(".ordering-food-grid article")[3].scrollIntoView()');
    await until('document.querySelectorAll(".ordering-food-grid article")[3].querySelector("img")?.naturalWidth > 0');
    assert.equal(await evaluate('document.querySelector(".client-count").textContent'), '1', 'Image refresh preserves cart quantity');
    assert.deepEqual(errors, []);
    console.log('Food images passed: API-shaped fixture, Home/menu/cart/teaser/wheel/result, image errors/missing/new URL, 46 products, mobile/search/stock. No SQL integration claimed.');
  } else console.log('Captured food image baseline before UI changes.');
} catch (error) {
  await screenshot('food-images-failure.png');
  throw error;
} finally { socket.close(); }
