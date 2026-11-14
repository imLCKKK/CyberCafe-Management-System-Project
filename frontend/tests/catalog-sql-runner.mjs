import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import path from 'node:path';

// Owns one disposable SQL host for this suite. Build API.Checks before running.
const root = fileURLToPath(new URL('../..', import.meta.url));
if (!process.env.AUTH_CHECKS_SQL_CONNECTION) throw new Error('AUTH_CHECKS_SQL_CONNECTION is required; no fixture fallback.');
const portable = path.join(root, '.tools/dotnet/dotnet.exe');
const dotnet = existsSync(portable) ? portable : 'dotnet';
const host = spawn(dotnet, [path.join(root, 'Backend/tests/CyberManagement.API.Checks/bin/Debug/net10.0/CyberManagement.API.Checks.dll'), '--catalog-browser-host'], {
  cwd: root, windowsHide: true, stdio: ['pipe', 'pipe', 'pipe'],
});
let log = '';
const closed = new Promise(resolve => host.on('exit', resolve));
const ready = new Promise((resolve, reject) => {
  const timer = setTimeout(() => reject(new Error('Catalog SQL host timed out: ' + log)), 60000);
  host.on('error', error => { clearTimeout(timer); reject(error); });
  host.on('exit', code => { clearTimeout(timer); reject(new Error(`SQL host exited ${code}: ${log}`)); });
  host.stderr.on('data', chunk => { log += chunk; });
  host.stdout.on('data', chunk => {
    log += chunk;
    for (const line of log.split(/\r?\n/)) {
      if (!line.startsWith('{')) continue;
      try { const row = JSON.parse(line); if (row.type === 'ready') { clearTimeout(timer); resolve(row.url); } } catch { /* incomplete chunk */ }
    }
  });
});
try {
  const url = await ready;
  const code = await new Promise((resolve, reject) => {
    const browser = spawn(process.execPath, ['tests/run.mjs', 'catalog-sql'], {
      cwd: path.join(root, 'frontend'), windowsHide: true, stdio: 'inherit',
      env: { ...process.env, VITE_API_PROXY_TARGET: url },
    });
    browser.on('error', reject);
    browser.on('exit', resolve);
  });
  if (code !== 0) process.exitCode = 1;
} finally {
  host.stdin.end('\n');
  await closed;
  const cleanup = log.split(/\r?\n/).find(line => line.startsWith('Removed isolated'));
  if (cleanup) console.log(cleanup);
  else { console.error('SQL host cleanup not confirmed: ' + log); process.exitCode = 1; }
}
