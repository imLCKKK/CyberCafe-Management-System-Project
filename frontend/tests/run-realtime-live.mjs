import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

// Explicit SQL fixture opt-in; owns and removes only its randomly named database.
if (!process.env.AUTH_CHECKS_SQL_CONNECTION) throw Error('AUTH_CHECKS_SQL_CONNECTION is required.');
const root = fileURLToPath(new URL('../..', import.meta.url));
const portable = path.join(root, '.tools/dotnet/dotnet.exe');
const dotnet = process.env.DOTNET_EXE || (existsSync(portable) ? portable : 'dotnet');
const env = { ...process.env, REALTIME_CHECKS_URL: 'http://127.0.0.1:0' };
if (dotnet === portable) Object.assign(env, {
  DOTNET_ROOT: path.dirname(portable), DOTNET_CLI_HOME: path.join(root, '.tools/dotnet-home'),
  NUGET_PACKAGES: path.join(root, '.tools/nuget-packages'),
});
const run = (command, args, options = {}) => new Promise((resolve, reject) => {
  const child = spawn(command, args, { cwd: root, windowsHide: true, env, stdio: 'inherit', ...options });
  child.once('error', reject);
  child.once('exit', code => code === 0 ? resolve() : reject(Error(`Check process exited with code ${code}.`)));
});
await run(dotnet, ['build', 'Backend/tests/CyberManagement.API.Checks', '--no-restore']);
const host = spawn(dotnet, ['Backend/tests/CyberManagement.API.Checks/bin/Debug/net10.0/CyberManagement.API.Checks.dll', '--realtime-host'],
  { cwd: root, windowsHide: true, env, stdio: ['pipe', 'pipe', 'pipe'] });
let exited = false;
const stopped = new Promise((resolve, reject) => {
  host.once('error', reject);
  host.once('exit', code => { exited = true; code === 0 ? resolve() : reject(Error(`SQL fixture exited with code ${code}.`)); });
});
// Attach a handler immediately, including when host startup fails before readiness.
void stopped.catch(() => {});
try {
  const url = await new Promise((resolve, reject) => {
    let buffer = '';
    const timer = setTimeout(() => reject(Error('SQL fixture startup timed out.')), 60000);
    host.stdout.on('data', chunk => {
      buffer += chunk;
      while (buffer.includes('\n')) {
        const end = buffer.indexOf('\n');
        const line = buffer.slice(0, end).trim(); buffer = buffer.slice(end + 1);
        if (line.startsWith('{')) {
          const message = JSON.parse(line);
          if (message.type === 'ready') { clearTimeout(timer); resolve(message.url); }
        } else console.log(line);
      }
    });
    host.stderr.on('data', chunk => process.stderr.write(chunk));
    stopped.then(() => { clearTimeout(timer); reject(Error('SQL fixture stopped before ready.')); }, error => { clearTimeout(timer); reject(error); });
  });
  await run(process.execPath, ['frontend/tests/run.mjs', 'realtime-live'], {
    env: { ...env, API_PROXY_TARGET: url, REALTIME_LIVE_API_URL: url },
  });
} finally {
  if (!exited) host.stdin.end('stop\n');
  await stopped;
}
