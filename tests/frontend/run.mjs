import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { mkdir, mkdtemp, writeFile } from 'node:fs/promises';
import path from 'node:path';
import { tmpdir } from 'node:os';
import { createServer as createTcpServer } from 'node:net';
import { createServer } from 'vite';

const outputDir = path.resolve('.tools/frontend-retest', new Date().toISOString().replace(/[:.]/g, '-'));
await mkdir(outputDir, { recursive: true });
const candidates = [process.env.CHROME_PATH, 'C:/Program Files/Google/Chrome/Application/chrome.exe', 'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe', '/usr/bin/google-chrome', '/usr/bin/chromium'].filter(Boolean);
const executable = candidates.find(candidate => existsSync(candidate));
if (!executable) throw Error('Set CHROME_PATH to a Chrome or Edge executable.');
const port = await new Promise(resolve => { const server = createTcpServer(); server.listen(0, '127.0.0.1', () => { const value = server.address().port; server.close(() => resolve(value)); }); });
const server = await createServer({ server: { host: '127.0.0.1', port: 0, open: false } });
await server.listen();
const origin = server.resolvedUrls.local[0].replace(/\/$/, '');
const profile = await mkdtemp(path.join(tmpdir(), 'cybercafe-frontend-'));
const args = ['--headless=new', '--disable-gpu', '--no-first-run', '--no-default-browser-check', `--remote-debugging-port=${port}`, `--user-data-dir=${profile}`, 'about:blank'];
// Opt in only when the execution environment cannot run Chrome's process sandbox.
if (process.env.CHROME_NO_SANDBOX === '1') args.unshift('--no-sandbox', '--disable-software-rasterizer');
const browser = spawn(executable, args, { windowsHide: true, stdio: ['ignore', 'ignore', 'pipe'] });
let browserLog = '';
browser.stderr.on('data', data => { browserLog += data; });
const suites = process.argv.slice(2);
const selected = suites.length ? suites : ['routes', 'admin-workflows', 'admin-controls', 'invoices', 'suggestion-checkout', 'client-workflows', 'opgg', 'cross-tab', 'resilience'];
const results = [];
try {
  let ready = false;
  for (let index = 0; index < 100; index++) {
    try { await fetch(`http://127.0.0.1:${port}/json/version`).then(r => r.json()); ready = true; break; } catch { await new Promise(resolve => setTimeout(resolve, 100)); }
  }
  if (!ready) throw Error(`Chrome did not start: ${browserLog.slice(-2000)}`);
  console.log(`Frontend: ${origin}\nArtifacts: ${outputDir}`);
  for (const suite of selected) {
    const started = Date.now();
    const result = await new Promise(resolve => {
      const child = spawn(process.execPath, [`tests/frontend/${suite}.mjs`], { windowsHide: true, env: { ...process.env, FRONTEND_URL: origin, FRONTEND_CDP_PORT: String(port), FRONTEND_ARTIFACTS: outputDir }, stdio: ['ignore', 'pipe', 'pipe'] });
      let output = '';
      child.stdout.on('data', data => { output += data; process.stdout.write(data); });
      child.stderr.on('data', data => { output += data; process.stderr.write(data); });
      const timeout = setTimeout(() => child.kill(), 180000);
      child.on('error', error => { clearTimeout(timeout); resolve({ code: 1, output: error.message }); });
      child.on('exit', code => { clearTimeout(timeout); resolve({ code, output }); });
    });
    results.push({ suite, ...result, durationMs: Date.now() - started });
  }
  if (results.some(result => result.code !== 0)) process.exitCode = 1;
} finally {
  browser.kill(); await server.close();
  await writeFile(path.join(outputDir, 'summary.json'), JSON.stringify(results, null, 2));
  await writeFile(path.join(outputDir, 'browser.log'), browserLog);
}
