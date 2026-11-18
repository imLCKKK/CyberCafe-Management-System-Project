import { createServer } from 'node:http';
import { createProviderMiddleware } from './statsMiddleware.js';

const middleware = createProviderMiddleware();
const port = Number(process.env.STATS_PORT || 8787);
createServer((req, res) => middleware(req, res, () => { res.statusCode = 404; res.end('Not found'); }))
  .listen(port, '127.0.0.1', () => console.log(`OP.GG stats backend: http://127.0.0.1:${port}`));
