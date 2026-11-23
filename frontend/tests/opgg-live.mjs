import assert from 'node:assert/strict';
import { writeFile } from 'node:fs/promises';
import { socket, origin, outputDir } from './cdp.mjs';
import { validateStats } from '../../Backend/integrations/opgg/shared.js';

const riotId = process.env.OPGG_RIOT_ID || 'Blue#00h';
try {
  const response = await fetch(`${origin}/api/valorant/opgg/stats?${new URLSearchParams({ riotId })}`, { signal: AbortSignal.timeout(30000) });
  const body = await response.json();
  assert.equal(response.status, 200, JSON.stringify(body));
  const stats = validateStats(body, riotId);
  await writeFile(outputDir + '/opgg-live-response.json', JSON.stringify(stats, null, 2));
  console.log('PASS live public adapter', JSON.stringify(stats));
} finally { socket.close(); }
