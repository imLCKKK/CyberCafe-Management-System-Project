import { captureStats } from './capture.js';
import { createOpggService } from './opgg.js';

export function createStatsMiddleware(service = createOpggService()) {
  return middleware(service, false);
}

export function createProviderMiddleware() {
  return middleware(createOpggService(), true);
}

function middleware(service, allowCapture) {
  return async (req, res, next) => {
    const url = new URL(req.url, 'http://localhost');
    if (url.pathname !== '/api/valorant/opgg/stats') return next();
    res.setHeader('Content-Type', 'application/json; charset=utf-8');
    res.setHeader('Cache-Control', 'no-store');
    res.setHeader('X-Content-Type-Options', 'nosniff');
    if (req.method !== 'GET') { res.setHeader('Allow', 'GET'); res.statusCode = 405; res.end(JSON.stringify({ message: 'Chỉ hỗ trợ GET.' })); return; }
    try {
      const stats = await service.get(url.searchParams.get('riotId'));
      if (allowCapture && req.headers?.accept === 'application/vnd.opgg.capture+json') {
        res.setHeader('Content-Type', 'application/vnd.opgg.capture+json');
        res.end(captureStats(stats, stats.riotId, 'provider'));
      } else res.end(JSON.stringify(stats));
    }
    catch (error) {
      res.statusCode = error.status || 502;
      if (res.statusCode === 429) res.setHeader('Retry-After', '60');
      res.end(JSON.stringify({ message: error.status ? error.message : 'Dịch vụ OP.GG tạm thời không khả dụng.' }));
    }
  };
}
