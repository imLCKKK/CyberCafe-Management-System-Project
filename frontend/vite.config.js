import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { createStatsMiddleware } from '../Backend/integrations/opgg/statsMiddleware.js';

const apiTarget = process.env.API_URL || 'http://127.0.0.1:5088';

export default defineConfig({
  plugins: [react(), {
    name: 'opgg-stats-backend',
    configureServer(server) { server.middlewares.use(createStatsMiddleware()); },
    configurePreviewServer(server) { server.middlewares.use(createStatsMiddleware()); },
  }],
  server: {
    watch: {
      ignored: ['**/.tools/**', '**/Phuc/**'],
    },
    // Chuyển API .NET về cùng origin với web. Chỉ proxy các đường dẫn API nghiệp vụ;
    // /api/valorant/opgg/* vẫn do adapter OP.GG xử lý.
    proxy: {
      '/api/auth': apiTarget,
      '/api/admin': apiTarget,
      '/api/client': apiTarget,
    },
  },
});
