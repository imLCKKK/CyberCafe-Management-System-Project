import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, input, screenshot, setupWorkflowLogin } from './cdp.mjs';

try {
  await cdp('Runtime.enable'); await cdp('Page.enable');
  await cdp('Emulation.setDeviceMetricsOverride', { width: 390, height: 900, deviceScaleFactor: 1, mobile: false });
  await setupWorkflowLogin('customer');
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const realFetch = window.fetch.bind(window);
    window.__refreshCalls = 0;
    window.fetch = async (url, options = {}) => {
      const path = new URL(typeof url === 'string' ? url : url.url, location.href).pathname;
      const tournaments = /^\\/api\\/(client|admin)\\/tournaments/.test(path);
      if (path === '/api/admin/games') return Response.json([{gameId:7,gameName:'Valorant',isActive:true}]);
      if (path === '/api/client/games/7/ranks') return Response.json([{rankId:8,rankName:'Hạng khách khai'}]);
      if (!tournaments) return realFetch(url, options);
      if (path.endsWith('/tournaments')) return Response.json([{id:21,gameId:7,name:'OP.GG fixture',gameName:'Valorant',
        status:'RegistrationOpen',teamSize:5,entryFee:0,registeredCount:1, version:'AAAAAAAAAAE=',
        registrationStartUtc:new Date(Date.now()-60000), registrationEndUtc:new Date(Date.now()+3600000), startTimeUtc:new Date(Date.now()+7200000)}]);
      const row = JSON.parse(sessionStorage.getItem('opgg.fixture.row') || 'null');
      if (path.endsWith('/registrations') && options.method === 'POST') {
        const request = JSON.parse(options.body);
        sessionStorage.setItem('opgg.fixture.row',JSON.stringify({id:31,customerName:'Người chơi',riotId:request.riotId,rankName:'Hạng khách khai',
          registeredAtUtc:new Date(),preferredRole:request.preferredRole,status:'Registered',isCurrentCustomer:true,opggSnapshot:null}));
        return Response.json({id:31},{status:201});
      }
      if (path.endsWith('/registrations')) return Response.json(row ? [row] : []);
      if (path.endsWith('/31/opgg/refresh')) {
        if (options.method !== 'POST' || options.body !== '{}') throw Error('Unexpected refresh request');
        window.__refreshCalls++;
        if (!sessionStorage.getItem('opgg.fixture.success') || window.__fail) return Response.json({message:'OP.GG tạm thời không phản hồi'},{status:502});
        row.opggSnapshot = { provenance:{origin:'fixture'},stats:{riotId:row.riotId,source:'opgg',queue:'competitive',scope:'Fixture · Competitive',
          fetchedAt:new Date().toISOString(),sourceUpdatedAt:null,rank:{tier:14,name:'Gold',division:3,label:'Gold 3'},
          matches:20,winRate:55,headshotRate:null,damagePerRound:145.2,kda:0}};
        sessionStorage.setItem('opgg.fixture.row',JSON.stringify(row));
        return Response.json(row.opggSnapshot);
      }
      return realFetch(url,options);
    };
  })()` });
  await go('/client/tournaments');
  await evaluate("sessionStorage.removeItem('opgg.fixture.row'); sessionStorage.removeItem('opgg.fixture.success')");
  await go('/client/tournaments');
  await click('Đăng ký tham gia'); await input('dialog input[name=gameName]', 'Player');
  await input('dialog input[name=tagLine]', 'VN 1');
  assert.equal(await evaluate("document.querySelector('dialog form').checkValidity()"), false);
  await input('dialog input[name=tagLine]', 'VN1'); await click('Xác nhận đăng ký', 'dialog button');
  await until("document.body.textContent.includes('Đồng bộ OP.GG thất bại')");
  assert.equal(await evaluate("window.__refreshCalls"), 1);
  assert.equal(await evaluate("document.querySelector('.tour-table tbody').textContent.includes('Hạng khách khai')"), false);
  await evaluate("sessionStorage.setItem('opgg.fixture.success','1')");
  await click('Đồng bộ OP.GG');
  await until("document.querySelector('.tour-table tbody')?.textContent.includes('Gold 3')");
  assert.deepEqual(await evaluate("Array.from(document.querySelectorAll('.tour-metric'), e => e.textContent)"), ['55%', '—', '145,2', '0']);
  await go('/client/tournaments');
  await until("document.querySelector('.tour-table tbody')?.textContent.includes('Gold 3')");
  assert.equal(await evaluate("window.__refreshCalls"), 0);
  await evaluate('window.__fail = true'); await click('Đồng bộ OP.GG');
  await until("document.querySelector('.tour-sync')?.textContent.includes('Đang hiển thị bản đã lưu')");
  assert.equal(await evaluate("window.__refreshCalls"), 1);
  await screenshot('opgg-client-fixture.png');
  await setupWorkflowLogin('employee');
  await go('/admin/tournaments'); await click('Quản lý');
  await until("document.querySelector('dialog')?.textContent.includes('Gold 3')");
  assert.ok(await evaluate("document.querySelector('dialog').textContent.includes('Hạng khách khai')"));
  await screenshot('opgg-admin-fixture.png');
  assert.deepEqual(errors, []);
  console.log('PASS OP.GG browser HTTP fixtures: register, provider error, manual retry, null vs zero, persisted list, admin view.');
} finally { socket.close(); }
