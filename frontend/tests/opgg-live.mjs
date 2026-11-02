import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { cdp, evaluate, until, errors, socket, go, click, input, screenshot, outputDir, origin } from './cdp.mjs';

const riotId = process.env.OPGG_RIOT_ID || 'Blue#00h';
try {
  await cdp('Page.enable'); await cdp('Runtime.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 1440, height: 1000, deviceScaleFactor: 1, mobile: false });
  const response = await fetch(`${origin}/api/valorant/opgg/stats?${new URLSearchParams({ riotId })}`, { signal: AbortSignal.timeout(30000) });
  const stats = await response.json();
  assert.equal(response.status, 200, JSON.stringify(stats));
  assert.equal(stats.source, 'opgg'); assert.ok(stats.rank?.label, 'Live OP.GG rank');
  await writeFile(outputDir + '/opgg-live-response.json', JSON.stringify(stats, null, 2));
  await go('/client/tournaments'); await evaluate('localStorage.clear()'); await go('/client/tournaments');
  await click('Đăng ký tham gia');
  assert.equal(await evaluate("document.querySelectorAll('dialog select').length"), 1);
  const [gameName, tagLine] = riotId.split('#');
  await input('dialog input[name=gameName]', gameName); await input('dialog input[name=tagLine]', tagLine);
  await input('dialog select', 'Duelist'); await click('Xác nhận đăng ký', 'dialog button');
  await until(`document.querySelector('.tour-table tbody')?.textContent.includes(${JSON.stringify(stats.rank.label)})`);
  const saved = await evaluate("JSON.parse(localStorage.getItem('tram.client.home.demo.v1')).TournamentRegistrations[0]");
  assert.equal(saved.OpggSnapshot.rank.label, stats.rank.label);
  assert.equal(saved.OpggSnapshot.kda, stats.kda);
  await screenshot('opgg-live-client.png');
  await go('/admin/tournaments');
  await evaluate("[...document.querySelectorAll('tbody button')].find(button => button.textContent === 'Quản lý').click()");
  await until(`document.querySelector('dialog')?.textContent.includes(${JSON.stringify(stats.rank.label)})`);
  await click('Đồng bộ rank', 'dialog button');
  await until("document.querySelector('dialog button') && ![...document.querySelectorAll('dialog button')].some(button => button.textContent.includes('Đang tải'))");
  await screenshot('opgg-live-admin.png');
  assert.deepEqual(errors, []);
  console.log(JSON.stringify({ passed: true, live: true, riotId, rank: stats.rank.label, matches: stats.matches, checks: ['live HTTP endpoint', 'two-field client registration and automatic rank', 'persisted rank/statistics', 'admin display and refresh'], fetchedAt: stats.fetchedAt }));
} finally { socket.close(); }
