import assert from 'node:assert/strict';
import { cdp, evaluate, until, errors, socket, go, click, input, setupWorkflowLogin } from './cdp.mjs';

const rowAction = (name, action = 'Chỉnh sửa') => evaluate(`(() => {
  const row = [...document.querySelectorAll('tbody tr')].find(row => row.textContent.includes(${JSON.stringify(name)}));
  [...row.querySelectorAll('button')].find(button => button.textContent === ${JSON.stringify(action)}).click();
})()`);
const submit = () => evaluate('document.querySelector("dialog[open] form").requestSubmit()');
const close = () => evaluate('document.querySelector("dialog[open] [aria-label=\\"Đóng\\"]").click()');
const waitForForm = () => until('Boolean(document.querySelector("dialog[open] select[name=role]"))');
const fillProfile = async () => {
  await input('dialog [name=position]', 'Thu ngân');
  await input('dialog [name=shift]', '08:00–16:00');
};

try {
  await cdp('Runtime.enable');
  await cdp('Page.enable');
  await setupWorkflowLogin('employee', { resetDemo: true });
  await cdp('Page.addScriptToEvaluateOnNewDocument', { source: `(() => {
    const originalFetch = window.fetch.bind(window);
    const roles = [
      { roleId: 41, roleName: 'Admin' },
      { roleId: 16, roleName: 'Manager' },
      { roleId: 87, roleName: 'Cashier' }
    ];
    let employees = [
      { employeeId: 3, fullName: 'API Admin', phone: '0900000101', roleName: 'Admin', isActive: true },
      { employeeId: 102, fullName: 'API Manager', phone: '0900000102', roleName: 'Manager', isActive: true },
      { employeeId: 103, fullName: 'API Cashier', phone: '0900000103', roleName: 'Cashier', isActive: false },
      { employeeId: 104, fullName: 'API Custom', phone: '0900000104', roleName: 'CustomRole', isActive: true }
    ].map(employee => ({ ...employee, createdAt: '2026-11-17T00:00:00Z', version: 'AAAAAAAAAAE=' }));
    window.__staffCalls = [];
    window.__passwordCalls = [];
    window.__staffLoads = [];
    window.__staffListMode = sessionStorage.getItem('__staffListMode');
    window.fetch = async (request, options = {}) => {
      const path = new URL(typeof request === 'string' ? request : request.url, location.href).pathname;
      const method = options.method || 'GET';
      if (path === '/api/auth/me' && sessionStorage.getItem('__staffAccessRole') === 'Manager') {
        return Response.json({ employeeId: 102, username: 'view-only-manager', role: 'Manager', permissions: ['employee.view'] });
      }
      if (path === '/api/auth/me' && sessionStorage.getItem('__staffAccessRole') === 'Cashier') {
        return Response.json({ employeeId: 103, username: 'cashier-no-staff-view', role: 'Cashier', permissions: ['order.view'] });
      }
      if (path === '/api/auth/change-password') {
        const body = JSON.parse(options.body);
        window.__passwordCalls.push({ body, authorization: options.headers.get('Authorization') });
        if (body.currentPassword !== 'Manager-current-2026!') return Response.json({ detail: 'Mật khẩu hiện tại không chính xác.' }, { status: 400 });
        if (window.__passwordPending) await new Promise(resolve => window.__releasePassword = resolve);
        return new Response(null, { status: 204 });
      }
      if (path === '/api/admin/roles') {
        if (window.__rolesMode === 'error') return Response.json({ message: 'Catalog unavailable' }, { status: 500 });
        return Response.json(window.__rolesMode === 'empty' ? [] : roles);
      }
      if (path === '/api/employees' && method === 'GET') {
        if (window.__staffListMode === 'pending') await new Promise(resolve => window.__staffLoads.push(resolve));
        if (window.__staffListMode === 'error') return Response.json({ message: 'Employees unavailable' }, { status: 500 });
        return Response.json(window.__staffListMode === 'empty' ? [] : employees);
      }
      if (path.startsWith('/api/employees') && ['PUT', 'POST'].includes(method)) {
        const body = JSON.parse(options.body || '{}');
        window.__staffCalls.push({ path, method, body });
        if (method === 'PUT') {
          if (window.__staffConflict) {
            employees = employees.map(employee => path.endsWith('/' + employee.employeeId)
              ? { ...employee, fullName: 'External edit', version: 'AAAAAAAAAAk=' } : employee);
            window.__staffConflict = false;
          }
          const current = employees.find(employee => path.endsWith('/' + employee.employeeId));
          if (body.expectedVersion !== current.version) return Response.json({ detail: 'VersionConflict: hồ sơ nhân viên đã thay đổi. Vui lòng tải lại.' }, { status: 409 });
        }
        const roleName = roles.find(role => role.roleId === body.roleId)?.roleName;
        if (path.endsWith('/lock') || path.endsWith('/unlock')) {
          const employeeId = Number(path.split('/').at(-2));
          employees = employees.map(employee => employee.employeeId === employeeId
            ? { ...employee, isActive: path.endsWith('/unlock') } : employee);
        } else if (method === 'POST') employees.push({ employeeId: 105, ...body, roleName, isActive: true });
        else employees = employees.map(employee => path.endsWith('/' + employee.employeeId)
          ? { ...employee, ...body, ...(roleName ? { roleName } : {}), version: 'AAAAAAAAAAI=' } : employee);
        return Response.json({});
      }
      return originalFetch(request, options);
    };
  })()` });

  await evaluate('sessionStorage.setItem("__staffListMode", "pending")');
  await go('/admin/staff');
  await until('window.__staffLoads.length > 0');
  const assertUnavailable = async () => {
    assert.equal(await evaluate('document.querySelectorAll("tbody tr, .cafe-stats, .admin-donut, dialog[open]").length'), 0);
    assert.equal(await evaluate('[...document.querySelectorAll("button")].find(button => button.textContent === "Thêm nhân viên").disabled'), true);
    await evaluate('[...document.querySelectorAll("button")].find(button => button.textContent === "Thêm nhân viên").click()');
    assert.equal(await evaluate('Boolean(document.querySelector("dialog[open]"))'), false);
  };
  await assertUnavailable();
  assert.ok(await evaluate('document.body.textContent.includes("Đang tải danh sách nhân viên")'));
  await evaluate('window.__staffListMode = "error"; window.__staffLoads.splice(0).forEach(resolve => resolve())');
  await until('Boolean(document.querySelector(".admin-module [role=alert]"))');
  await assertUnavailable();
  assert.equal(await evaluate('window.__staffCalls.length'), 0, 'No writes may target demo IDs while loading or failed');
  await evaluate('window.__staffListMode = "pending"');
  await click('Thử lại');
  await until('window.__staffLoads.length > 0');
  await assertUnavailable();
  await evaluate('window.__staffListMode = "success"; window.__staffLoads.splice(0).forEach(resolve => resolve())');
  await until('document.querySelector("tbody")?.textContent.includes("API Custom")');
  assert.deepEqual(await evaluate('[...document.querySelectorAll("tbody .admin-status")].filter((_, index) => index % 2 === 0).map(el => el.textContent)'), ['Quản trị viên', 'Manager', 'Cashier', 'CustomRole']);
  assert.equal(await evaluate('document.querySelectorAll(".cafe-stats .cafe-stat-value")[2].textContent'), '1');
  assert.equal(await evaluate('document.querySelector(".admin-donut svg").getAttribute("aria-label")'), 'nhân sự: 4');
  await input('select[aria-label="Vai trò nhân viên"]', 'Admin');
  assert.equal(await evaluate('document.querySelector("tbody").textContent.includes("API Admin")'), true);
  assert.equal(await evaluate('document.querySelectorAll("tbody tr").length'), 1);
  await input('select[aria-label="Vai trò nhân viên"]', 'Manager');
  assert.equal(await evaluate('document.querySelector("tbody").textContent.includes("API Manager")'), true);
  await input('select[aria-label="Vai trò nhân viên"]', 'all');

  await rowAction('API Admin');
  await waitForForm();
  assert.equal(await evaluate('document.querySelector("dialog select[name=role]").value'), '41');
  await fillProfile();
  await submit();
  await until('!document.querySelector("dialog[open]")');
  assert.equal(await evaluate('Object.hasOwn(window.__staffCalls.at(-1).body, "roleId")'), false);
  assert.equal(await evaluate('window.__staffCalls.at(-1).body.expectedVersion'), 'AAAAAAAAAAE=');

  await until('document.querySelector("tbody")?.textContent.includes("API Admin")');
  await rowAction('API Admin');
  await waitForForm();
  await fillProfile();
  await input('dialog [name=display_name]', 'Stale UI edit');
  await evaluate('window.__staffConflict = true');
  await submit();
  await until('document.querySelector("dialog [role=alert]")?.textContent.includes("VersionConflict")');
  assert.equal(await evaluate('document.querySelector("dialog [name=display_name]").value'), 'Stale UI edit', 'Conflict preserves unsaved input');
  assert.equal(await evaluate('window.__staffCalls.at(-1).body.expectedVersion'), 'AAAAAAAAAAI=');
  await click('Tải lại danh sách', 'dialog button');
  await until('!document.querySelector("dialog[open]") && document.querySelector("tbody")?.textContent.includes("External edit")');
  await rowAction('External edit');
  await waitForForm();
  await fillProfile();
  await input('dialog [name=display_name]', 'API Admin');
  await submit();
  await until('!document.querySelector("dialog[open]")');
  assert.equal(await evaluate('window.__staffCalls.at(-1).body.expectedVersion'), 'AAAAAAAAAAk=', 'Reloaded form uses the fresh server version');

  await rowAction('API Manager');
  await waitForForm();
  assert.equal(await evaluate('document.querySelector("dialog select[name=role]").value'), '16');
  await input('dialog select[name=role]', '87');
  await fillProfile();
  await submit();
  await until('!document.querySelector("dialog[open]")');
  assert.equal(await evaluate('window.__staffCalls.at(-1).body.roleId'), 87);
  await until('[...document.querySelectorAll("tbody tr")].find(row => row.textContent.includes("API Manager"))?.textContent.includes("Cashier")');

  await click('Thêm nhân viên');
  await waitForForm();
  assert.equal(await evaluate('document.querySelector("dialog select[name=role]").value'), '');
  assert.deepEqual(await evaluate('[...document.querySelector("dialog select[name=role]").options].map(option => option.value)'), ['', '41', '16', '87']);
  await input('dialog [name=display_name]', 'API New');
  await input('dialog [name=phone]', '0900000105');
  await input('dialog [name=password]', 'StaffTest@2026');
  await input('dialog select[name=role]', '16');
  await submit();
  await until('!document.querySelector("dialog[open]")');
  assert.equal(await evaluate('window.__staffCalls.at(-1).body.roleId'), 16);
  await until('document.querySelector("tbody").textContent.includes("API New")');

  await rowAction('API Custom');
  await waitForForm();
  assert.equal(await evaluate('document.querySelector("dialog select[name=role]").value'), '', 'Unknown role must not silently become Admin or Staff');
  await close();

  for (const mode of ['error', 'empty']) {
    await evaluate(`window.__rolesMode = ${JSON.stringify(mode)}`);
    await click('Thêm nhân viên');
    await until('Boolean(document.querySelector("dialog [role=alert]"))');
    assert.equal(await evaluate('Boolean(document.querySelector("dialog button[type=submit]"))'), false);
    await close();
  }
  await evaluate('window.__rolesMode = null; window.__staffListMode = "error"');
  await rowAction('API Admin', 'Khóa');
  await submit();
  await until('!document.querySelector("dialog[open]") && Boolean(document.querySelector(".admin-module [role=alert]"))');
  await assertUnavailable();
  assert.equal(await evaluate('window.__staffCalls.at(-1).path'), '/api/employees/3/lock', 'Only a server-loaded employee ID may be locked');
  const writes = await evaluate('window.__staffCalls.length');
  await evaluate('window.__staffListMode = "empty"');
  await click('Thử lại');
  await until('document.body.textContent.includes("Chưa có nhân viên trên máy chủ.")');
  assert.equal(await evaluate('document.querySelectorAll("tbody tr").length'), 0);
  assert.equal(await evaluate('document.querySelector(".cafe-stat-value").textContent'), '0');
  assert.equal(await evaluate('[...document.querySelectorAll("button")].find(button => button.textContent === "Thêm nhân viên").disabled'), false);
  assert.equal(await evaluate('window.__staffCalls.length'), writes, 'Retry must not repeat a successful mutation');
  await evaluate('sessionStorage.removeItem("__staffListMode")');
  await evaluate('sessionStorage.setItem("__staffAccessRole", "Manager")');
  await go('/admin/staff');
  await until('document.querySelector("tbody")?.textContent.includes("API Admin")');
  assert.equal(await evaluate('document.querySelector(".user b").textContent'), 'view-only-manager');
  assert.equal(await evaluate('[...document.querySelectorAll("button")].some(button => ["Thêm nhân viên", "Chỉnh sửa", "Khóa", "Mở khóa"].includes(button.textContent))'), false, 'A view-only Manager has no management actions');
  await evaluate('document.querySelector("tbody .admin-link").click()');
  await until('Boolean(document.querySelector("dialog[open]"))');
  await close();
  await evaluate('document.querySelector("[aria-label=\\"Đổi mật khẩu\\"]").click()');
  await until('Boolean(document.querySelector("dialog [name=currentPassword]"))');
  await input('dialog [name=currentPassword]', 'Wrong-current-2026!');
  await input('dialog [name=newPassword]', 'Manager-new-password-2026!');
  await input('dialog [name=confirmNewPassword]', 'Mismatched-confirmation-2026!');
  await submit();
  await until('document.querySelector("dialog [role=alert]")?.textContent.includes("không khớp")');
  assert.equal(await evaluate('window.__passwordCalls.length'), 0, 'Client mismatch is shown before a request');
  await input('dialog [name=confirmNewPassword]', 'Manager-new-password-2026!');
  await submit();
  await until('document.querySelector("dialog [role=alert]")?.textContent.includes("Mật khẩu hiện tại không chính xác")');
  assert.equal(await evaluate('Boolean(localStorage.getItem("cybermanagement.auth.v1"))'), true, '400 does not sign out the employee');
  await input('dialog [name=currentPassword]', 'Manager-current-2026!');
  await evaluate('window.__passwordPending = true');
  await submit();
  await until('Boolean(window.__releasePassword)');
  await submit();
  assert.equal(await evaluate('window.__passwordCalls.length'), 2, 'The busy guard prevents duplicate password changes');
  assert.deepEqual(await evaluate('window.__passwordCalls.at(-1)'), {
    body: { currentPassword: 'Manager-current-2026!', newPassword: 'Manager-new-password-2026!', confirmNewPassword: 'Manager-new-password-2026!' },
    authorization: 'Bearer workflow-employee-token',
  });
  await evaluate('window.__releasePassword()');
  await until('!document.querySelector("dialog[open]") && document.body.textContent.includes("Đã đổi mật khẩu.")');
  await evaluate('document.querySelector("[aria-label=\\"Đổi mật khẩu\\"]").click()');
  await until('Boolean(document.querySelector("dialog [name=currentPassword]"))');
  assert.equal(await evaluate('[...document.querySelectorAll("dialog input")].every(input => input.value === "")'), true, 'Saved passwords are not retained when reopening the form');
  await close();
  await evaluate('sessionStorage.setItem("__staffAccessRole", "Cashier")');
  await go('/admin/dashboard');
  await until('document.querySelector(".user b")?.textContent === "cashier-no-staff-view"');
  assert.equal(await evaluate('Boolean(document.querySelector(".sidebar a[href=\\"/admin/staff\\"]"))'), false, 'Cashier has no employee.view menu');
  await evaluate('document.querySelector("[aria-label=\\"Đổi mật khẩu\\"]").click()');
  await until('Boolean(document.querySelector("dialog [name=currentPassword]"))');
  await close();
  await evaluate('sessionStorage.removeItem("__staffAccessRole")');
  assert.deepEqual(errors, []);
  console.log('Staff: loading/retry, roles/versions, Manager read-only UI and self-password flow with errors, bearer token, busy guard and cleared credentials (API fixtures).');
} finally {
  socket.close();
}
