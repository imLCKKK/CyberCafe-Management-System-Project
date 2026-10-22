import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import { createStatsMiddleware } from './server/statsMiddleware.js';

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
  },
});
