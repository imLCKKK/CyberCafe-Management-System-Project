import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { createStatsMiddleware } from '../backend/integrations/opgg/statsMiddleware.js';

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
    // Chuyển API .NET về cùng origin với web để cookie đăng nhập hoạt động.
    // Chỉ proxy các đường dẫn của API nghiệp vụ; /api/valorant/opgg/* vẫn do adapter OP.GG xử lý.
    proxy: {
      '/api/admin': apiTarget,
      '/api/client': apiTarget,
      '/dev': apiTarget,
    },
  },
});
