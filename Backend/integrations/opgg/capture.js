import { randomUUID } from 'node:crypto';
import { validateStats } from './shared.js';

// The snapshot schema contains objects and JSON primitives. RFC 8785 uses
// UTF-16 key order and ECMAScript serialization for strings and numbers.
function canonical(value) {
  if (value === null || typeof value !== 'object') return JSON.stringify(value);
  return '{' + Object.keys(value).sort().map(key => JSON.stringify(key) + ':' + canonical(value[key])).join(',') + '}';
}

export function captureStats(stats, riotId, origin, snapshotId = randomUUID(), capturedAtUtc = new Date().toISOString()) {
  return canonical({ stats: validateStats(stats, riotId), provenance: { origin, snapshotId, capturedAtUtc } });
}
