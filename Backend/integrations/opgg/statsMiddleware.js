import { createOpggService } from './opgg.js';

export function createStatsMiddleware(service = createOpggService()) {
  return async (req, res, next) => {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname !== '/api/valorant/opgg/stats') return next();
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    res.setHeader('Cache-Control', 'no-store');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    if (req.method !== 'GET') { res.setHeader('Allow', 'GET'); res.statusCode = 405; res.end(JSON.stringify({ message: 'Chỉ hỗ trợ GET.' })); return; }
    try { res.end(JSON.stringify(await service.get(url.searchParams.get('riotId')))); }
    catch (error) {
      res.statusCode = error.status || 502;
      if (res.statusCode === 429) res.setHeader('Retry-After', '60');
      res.end(JSON.stringify({ message: error.status ? error.message : 'Dịch vụ OP.GG tạm thời không khả dụng.' }));
    }
  };
}
