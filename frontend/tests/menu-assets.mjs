import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import { cdp, evaluate, go, screenshot, socket } from './cdp.mjs';

// Verify every delivered photo and collect browser-rendered contact sheets for visual QA.
// The asset index has no product IDs, prices or stock and is not an application catalog.
const { assets } = JSON.parse(await readFile(new URL('../../docs/nexora-menu-assets.json', import.meta.url), 'utf8'));
assert.equal(assets.length, 62);
assert.equal(new Set(assets.map(a => a.key)).size, 62);
const groups = { rice: 'Cơm', noodles: 'Mì', tea: 'Trà sữa & trà', snacks: 'Ăn vặt', addons: 'Món thêm', bottles: 'Nước đóng chai', combos: 'Combo' };

try {
  await cdp('Page.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1100, deviceScaleFactor: 1, mobile: false });
  await go('/client/auth', 'document.body');
  for (const [group, title] of Object.entries(groups)) {
    const items = assets.filter(a => a.group === group);
    const dimensions = await evaluate(`(async () => {
      document.body.replaceChildren();
      document.body.style.cssText = 'margin:0;padding:24px;background:#0b0f14;color:#e5f0ff;font-family:Arial,sans-serif';
      const heading = document.createElement('h1'); heading.textContent = ${JSON.stringify(title)}; document.body.append(heading);
      const grid = document.createElement('div'); grid.style.cssText = 'display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:16px'; document.body.append(grid);
      const result = [];
      for (const item of ${JSON.stringify(items)}) {
        const card = document.createElement('figure'); card.style.cssText = 'margin:0;background:#131c28;border:1px solid #283446;border-radius:12px;overflow:hidden';
        const image = document.createElement('img'); image.alt = item.name; image.style.cssText = 'width:100%;height:235px;object-fit:contain;display:block';
        const loaded = new Promise((resolve, reject) => { image.onload = resolve; image.onerror = () => reject(Error('Missing photo: ' + item.imageUrl)); });
        image.src = item.imageUrl; card.append(image);
        const name = document.createElement('figcaption'); name.textContent = item.name; name.style.cssText = 'padding:10px 12px;font-size:15px;line-height:20px'; card.append(name); grid.append(card);
        await loaded; result.push([image.naturalWidth, image.naturalHeight]);
        const thumbnail = new Image(); const ready = new Promise((resolve, reject) => { thumbnail.onload = resolve; thumbnail.onerror = reject; }); thumbnail.src = item.thumbnailUrl; await ready;
        result.push([thumbnail.naturalWidth, thumbnail.naturalHeight]);
      }
      return result;
    })()`);
    assert.deepEqual(dimensions, items.flatMap(() => [[640, 640], [320, 320]]));
    await screenshot(`menu-assets-${group}.png`);
  }
  console.log('Menu assets passed: 62 unique photos, all 124 JPEG variants load at 320px/640px; 7 contact sheets saved.');
} finally { socket.close(); }
