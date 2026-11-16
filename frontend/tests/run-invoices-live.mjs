import { spawn } from 'node:child_process';
import { createInterface } from 'node:readline';
import { fileURLToPath } from 'node:url';

if (!process.env.AUTH_CHECKS_SQL_CONNECTION) throw Error('AUTH_CHECKS_SQL_CONNECTION is required.');
const root = fileURLToPath(new URL('../..', import.meta.url));
const dotnet = process.env.DOTNET_EXE || 'dotnet';
const run = (command, args, env = process.env) => new Promise((resolve, reject) => {
  const child = spawn(command, args, { cwd: root, windowsHide: true, env, stdio: 'inherit' });
  child.once('error', reject); child.once('exit', code => code === 0 ? resolve() : reject(Error(`Process exited ${code}.`)));
});
await run(dotnet, ['build', 'Backend/tests/CyberManagement.API.Checks', '--no-restore']);
const host = spawn(dotnet, ['Backend/tests/CyberManagement.API.Checks/bin/Debug/net10.0/CyberManagement.API.Checks.dll', '--invoice-browser-host'],
  { cwd: root, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'] });
host.stderr.on('data', chunk => process.stderr.write(chunk));
let exited = false;
const stopped = new Promise((resolve, reject) => {
  host.once('error', reject); host.once('exit', code => { exited = true; code === 0 ? resolve() : reject(Error(`SQL host exited ${code}.`)); });
});
void stopped.catch(() => {});
const lines = createInterface({ input: host.stdout });
try {
  const url = await new Promise((resolve, reject) => {
    const timer = setTimeout(() => reject(Error('SQL host startup timed out.')), 60000);
    lines.on('line', line => {
      console.log(line);
      if (line.startsWith('INVOICE_HOST_READY ')) { clearTimeout(timer); resolve(line.slice('INVOICE_HOST_READY '.length)); }
    });
    stopped.then(() => { clearTimeout(timer); reject(Error('Host stopped before readiness.')); }, error => { clearTimeout(timer); reject(error); });
  });
  await run(process.execPath, ['frontend/tests/run.mjs', 'invoices-live'], {
    ...process.env, VITE_API_PROXY_TARGET: url, API_PROXY_TARGET: url, VITE_API_BASE_URL: '/api', INVOICES_LIVE_API_URL: url,
  });
} finally {
  if (!exited) host.stdin.end('stop\n');
  await stopped; lines.close();
}
