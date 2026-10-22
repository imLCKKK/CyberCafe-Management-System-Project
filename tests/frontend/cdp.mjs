import { mkdir, writeFile } from 'node:fs/promises';
import path from 'node:path';

export const origin = process.env.FRONTEND_URL || 'http://127.0.0.1:5174';
export const outputDir = path.resolve(process.env.FRONTEND_ARTIFACTS || '.tools/frontend-retest');
export const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
const targets = await fetch(`http://127.0.0.1:${process.env.FRONTEND_CDP_PORT || 9234}/json/list`).then(r => r.json());
export const socket = new WebSocket(targets.find(target => target.type === 'page').webSocketDebuggerUrl);
await new Promise((resolve, reject) => { socket.addEventListener('open', resolve, { once: true }); socket.addEventListener('error', reject, { once: true }); });
let serial = 0;
const pending = new Map();
export const errors = [];
socket.addEventListener('message', ({ data }) => {
  const message = JSON.parse(data);
  if (message.method === 'Runtime.exceptionThrown') errors.push(message.params.exceptionDetails.exception?.description || message.params.exceptionDetails.text);
  if (message.method === 'Runtime.consoleAPICalled' && message.params.type === 'error') errors.push(message.params.args.map(arg => arg.value || arg.description).join(' '));
  if (message.id && pending.has(message.id)) {
    const task = pending.get(message.id); pending.delete(message.id);
    clearTimeout(task.timeout);
    message.error ? task.reject(new Error(JSON.stringify(message.error))) : task.resolve(message.result);
  }
});
export async function cdp(method, params = {}) {
  let onLoad, timer;
  const navigation = ['Page.navigate', 'Page.reload'].includes(method) ? new Promise((resolve, reject) => {
    onLoad = ({ data }) => { if (JSON.parse(data).method === 'Page.loadEventFired') resolve(); };
    socket.addEventListener('message', onLoad);
    timer = setTimeout(() => reject(new Error(`Navigation timed out: ${method}`)), 15000);
  }) : null;
  try {
  const result = await new Promise((resolve, reject) => {
    const id = ++serial;
    const timeout = setTimeout(() => { pending.delete(id); reject(new Error(`CDP timeout: ${method}`)); }, 15000);
    pending.set(id, { resolve, reject, timeout });
    socket.send(JSON.stringify({ id, method, params }));
  });
  if (navigation) await navigation;
  return result;
  } finally {
    clearTimeout(timer);
    if (onLoad) socket.removeEventListener('message', onLoad);
  }
}
export async function evaluate(expression) {
  const result = await cdp('Runtime.evaluate', { expression, returnByValue: true, awaitPromise: true });
  if (result.exceptionDetails) throw new Error(result.exceptionDetails.exception?.description || result.exceptionDetails.text);
  return result.result.value;
}
export async function until(expression) {
  for (let index = 0; index < 150; index++) { if (await evaluate(expression)) return; await sleep(80); }
  throw new Error(`Timed out: ${expression}; errors: ${JSON.stringify(errors)}; body: ${await evaluate('document.body.innerText.slice(0, 1200)')}`);
}
export async function screenshot(name) {
  await mkdir(outputDir, { recursive: true });
  const { data } = await cdp('Page.captureScreenshot', { format: 'png' });
  await writeFile(path.join(outputDir, name), Buffer.from(data, 'base64'));
}
export async function press(key) {
  const codes = { Escape: 27, Tab: 9, Enter: 13, ' ': 32 };
  for (const type of ['keyDown', 'keyUp']) await cdp('Input.dispatchKeyEvent', { type, key, code: key, windowsVirtualKeyCode: codes[key] });
}
export const click = (text, selector = 'button') => evaluate(`(() => { const target = [...document.querySelectorAll(${JSON.stringify(selector)})].find(el => el.textContent.trim() === ${JSON.stringify(text)}); if (!target || target.disabled) throw Error('Missing/disabled: ' + ${JSON.stringify(text)}); target.click(); })()`);
export const input = (selector, value) => evaluate(`(() => { const el = document.querySelector(${JSON.stringify(selector)}); if (!el) throw Error('Missing input: ' + ${JSON.stringify(selector)}); const proto = el.tagName === 'SELECT' ? HTMLSelectElement.prototype : el.tagName === 'TEXTAREA' ? HTMLTextAreaElement.prototype : HTMLInputElement.prototype; Object.getOwnPropertyDescriptor(proto, 'value').set.call(el, ${JSON.stringify(String(value))}); el.dispatchEvent(new Event(el.tagName === 'SELECT' ? 'change' : 'input', { bubbles: true })); })()`);
export async function go(route, ready = 'document.querySelector(".cafe-heading, .home-heading, .tour-heading, .placeholder")') {
  await cdp('Page.navigate', { url: origin + route }); await sleep(160); await until(`Boolean(${ready})`);
}
