import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, input, screenshot, press, sleep } from './cdp.mjs';

const storageKey = 'tram.client.home.demo.v1';
const payload = { riotId: 'Player#VN1', source: 'opgg', queue: 'competitive', scope: 'V26 - ACT6 · Competitive', sourceUpdatedAt: '2026-10-21T00:00:00Z', rank: { tier: 14, name: 'Gold', division: 3, label: 'Gold 3' }, matches: 20, winRate: 55, headshotRate: 25.5, damagePerRound: 145.2, kda: 1.6 };
try {
  await cdp('Runtime.enable'); await cdp('Page.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 900, deviceScaleFactor: 1, mobile: false });
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `
    const realFetch = window.fetch;
    window.__opggCalls = 0; window.__opggFail = false;
    window.fetch = (url, options) => {
      if (!String(url).includes('/api/valorant/opgg/stats')) return realFetch(url, options);
      window.__opggCalls++;
      const response = window.__opggFail ? { message: 'OP.GG tạm thời không phản hồi' } : { ...${JSON.stringify(payload)}, fetchedAt: new Date().toISOString() };
      return Promise.resolve(new Response(JSON.stringify(response), { status: window.__opggFail ? 502 : 200, headers: { 'content-type': 'application/json' } }));
    }` });
  await go('/client/tournaments'); await evaluate('localStorage.clear()'); await go('/client/tournaments');
  await click('Đăng ký tham gia');
  assert.equal(await evaluate("document.querySelectorAll('dialog input').length"), 2);
  assert.equal(await evaluate("document.querySelectorAll('dialog select').length"), 1, 'Only role, no manual rank');
  assert.ok(await evaluate("document.querySelector('dialog .opgg-privacy-note').textContent.includes('Private') && document.querySelector('dialog .opgg-privacy-note').textContent.includes('Public')"));
  await input('dialog input[name=gameName]', 'Player');
  await input('dialog input[name=tagLine]', 'VN 1');
  assert.equal(await evaluate("document.querySelector('dialog form').checkValidity()"), false, 'Tag cannot contain whitespace');
  await input('dialog input[name=tagLine]', 'VN1'); await input('dialog select', 'Duelist');
  assert.ok(await evaluate("(() => { const el = document.querySelector('.riot-id-parts'); const rect = el.getBoundingClientRect(); return rect.right <= innerWidth && el.scrollWidth <= el.clientWidth; })()"));
  await screenshot('opgg-split-id-registration-mobile.png');
  await click('Xác nhận đăng ký', 'dialog button');
  await until("document.querySelector('.tour-table tbody')?.textContent.includes('Gold 3')");
  assert.equal(await evaluate('window.__opggCalls'), 1);
  let saved = await evaluate(`JSON.parse(localStorage.getItem('${storageKey}'))`);
  assert.equal(saved.TournamentRegistrations[0].RankId, 4);
  assert.equal(saved.TournamentRegistrations[0].OpggSnapshot.rank.label, 'Gold 3');
  assert.ok(await evaluate("document.querySelector('.tour-table tbody').textContent.includes('55%')"));
  await screenshot('opgg-client-synced-mobile.png');
  await go('/client/tournaments');
  assert.ok(await evaluate("document.querySelector('.tour-table tbody').textContent.includes('Gold 3')"));
  assert.equal(await evaluate('window.__opggCalls'), 0, 'Fresh persisted result does not refetch on reload');
  await go('/admin/tournaments');
  await evaluate("[...document.querySelectorAll('tbody button')].find(button => button.textContent === 'Quản lý').click()");
  await until("document.querySelector('dialog')?.textContent.includes('Gold 3')");
  await screenshot('opgg-admin-synced-mobile.png');
  await click('Thêm người tham gia', 'dialog button');
  assert.equal(await evaluate("document.querySelectorAll('dialog[open]:last-of-type [name=rankId]').length"), 0);
  await press('Escape'); await press('Escape');
  await go('/client/tournaments');
  await evaluate(`(() => { const db = JSON.parse(localStorage.getItem('${storageKey}')); db.TournamentRegistrations[0].OpggSnapshot.fetchedAt = '2020-01-01T00:00:00Z'; localStorage.setItem('${storageKey}', JSON.stringify(db)); })()`);
  await go('/client/tournaments');
  await until('window.__opggCalls === 1');
  await until("!document.querySelector('.tour-section-heading button').disabled");
  assert.ok(Date.parse((await evaluate(`JSON.parse(localStorage.getItem('${storageKey}'))`)).TournamentRegistrations[0].OpggSnapshot.fetchedAt) > Date.parse('2020-01-01'));
  // Failed synchronization preserves a registration and has no automatic loop.
  await evaluate(`(() => { const db = JSON.parse(localStorage.getItem('${storageKey}')); db.TournamentRegistrations = []; localStorage.setItem('${storageKey}', JSON.stringify(db)); })()`);
  await go('/client/tournaments'); await evaluate('window.__opggFail = true');
  await click('Đăng ký tham gia'); await input('dialog input', 'Player#VN1'); await click('Xác nhận đăng ký', 'dialog button');
  await until("document.querySelector('.tour-sync')?.textContent.includes('OP.GG tạm thời không phản hồi')");
  await sleep(600); assert.equal(await evaluate('window.__opggCalls'), 1);
  saved = await evaluate(`JSON.parse(localStorage.getItem('${storageKey}'))`);
  assert.equal(saved.TournamentRegistrations[0].RankId, null);
  await evaluate('window.__opggFail = false'); await click('Đồng bộ OP.GG');
  await until("document.querySelector('.tour-table tbody').textContent.includes('Gold 3')");
  assert.equal(await evaluate('window.__opggCalls'), 2);
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, checks: ['Riot ID and role only', 'rank/stats automatic lookup and source snapshot', 'reload cache and stale refresh', 'rank shared with admin', 'mobile', 'failed provider preserves pending registration without retry loop', 'manual retry recovers'] }));
} finally { socket.close(); }
