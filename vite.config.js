import { defineConfig } from 'vite';
import { createStatsMiddleware } from './server/statsMiddleware.js';

// Same-origin backend for local development and preview. No provider secrets in React.
export default defineConfig({
  plugins: [{
    name: 'opgg-stats-backend',
    configureServer(server) { server.middlewares.use(createStatsMiddleware()); },
    configurePreviewServer(server) { server.middlewares.use(createStatsMiddleware()); },
  }],
});
