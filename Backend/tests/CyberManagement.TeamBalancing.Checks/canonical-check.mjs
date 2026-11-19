import { createHash } from 'node:crypto';
import { readFileSync } from 'node:fs';

function canonical(value) {
  if (value === null || typeof value !== 'object') return JSON.stringify(value);
  if (Array.isArray(value)) return '[' + value.map(canonical).join(',') + ']';
  return '{' + Object.keys(value).sort().map(key => JSON.stringify(key) + ':' + canonical(value[key])).join(',') + '}';
}

// Independent ECMAScript oracle for JCS, including exponent boundaries and Unicode.
for (const line of readFileSync(0, 'utf8').trim().split('\n')) {
  const value = JSON.parse(line);
  const hash = 'sha256:' + createHash('sha256').update(canonical(value)).digest('hex');
  process.stdout.write(hash + '\n');
}
