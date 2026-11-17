import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, input, setupWorkflowLogin } from './cdp.mjs';

try {
  await cdp('Runtime.enable');
  await cdp('Page.enable');
  await setupWorkflowLogin('customer');
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const originalFetch = window.fetch.bind(window);
    window.fetch = async (url, options = {}) => {
      const path = new URL(typeof url === 'string' ? url : url.url, location.href).pathname;
      const now = Date.now();
      if (path === '/api/client/tournaments') return Response.json([{
        id: 21, gameId: 7, name: 'Giải có phí', gameName: 'Valorant', teamSize: 5,
        status: 'RegistrationOpen', registrationStartUtc: new Date(now - 60000).toISOString(),
        registrationEndUtc: new Date(now + 3600000).toISOString(),
        startTimeUtc: new Date(now + 7200000).toISOString(), entryFee: 1000, version: 'AAAAAAAAAAE='
      }]);
      if (path === '/api/client/games/7/ranks') return Response.json([{ rankId: 3, rankName: 'Gold' }]);
      if (path === '/api/client/tournaments/21/registrations') {
        if (options.method === 'POST') {
          sessionStorage.setItem('paidRegistration', options.body);
          return Response.json({ id: 1 }, { status: 201 });
        }
        return Response.json([]);
      }
      return originalFetch(url, options);
    };
  })()` });
  await go('/client/tournaments');
  await until("document.body.textContent.includes('Giải có phí') && [...document.querySelectorAll('button')].some(b => b.textContent.trim() === 'Đăng ký tham gia' && !b.disabled)");
  assert.ok(await evaluate("document.body.textContent.includes('Thanh toán bằng ví')"));
  await click('Đăng ký tham gia');
  assert.ok(await evaluate("document.querySelector('dialog').textContent.includes('sẽ được trừ từ ví')"));
  assert.ok(await evaluate("document.querySelector('dialog').textContent.includes('chưa hỗ trợ hủy đăng ký hoặc hoàn phí')"));
  await input('dialog input[name=gameName]', 'Player');
  await input('dialog input[name=tagLine]', 'VN1');
  await click('Xác nhận đăng ký', 'dialog button');
  await until("sessionStorage.getItem('paidRegistration') !== null");
  assert.deepEqual(await evaluate("JSON.parse(sessionStorage.getItem('paidRegistration'))"), {
    riotId: 'Player#VN1', rankId: 3, preferredRole: 'Flex', expectedVersion: 'AAAAAAAAAAE=',
  });
  assert.deepEqual(errors, []);
  console.log('Paid tournament UI confirms wallet fee and submits the displayed version.');
} finally {
  socket.close();
}
