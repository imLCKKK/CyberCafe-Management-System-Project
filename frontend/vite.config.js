import { defineConfig, loadEnv } from 'vite';
import react from '@vitejs/plugin-react';
import { createStatsMiddleware } from '../Backend/integrations/opgg/statsMiddleware.js';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '');
  return {
  plugins: [react(), {
    name: 'opgg-stats-backend',
    configureServer(server) { server.middlewares.use(createStatsMiddleware()); },
    configurePreviewServer(server) { server.middlewares.use(createStatsMiddleware()); },
  }],
  server: {
    proxy: {
      '/api': {
        target: env.VITE_API_PROXY_TARGET || 'http://127.0.0.1:5088',
        changeOrigin: true,
      },
    },
    watch: {
      ignored: ['**/.tools/**', '**/Phuc/**'],
    },
  },
  };
});
