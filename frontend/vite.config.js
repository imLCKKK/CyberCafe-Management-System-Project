import { defineConfig, loadEnv } from 'vite';
import { fileURLToPath } from 'node:url';
import react from '@vitejs/plugin-react';
import { createStatsMiddleware } from '../Backend/integrations/opgg/statsMiddleware.js';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, fileURLToPath(new URL('.', import.meta.url)), '');
  const proxy = {
    // Keep OP.GG middleware local; forward all integrated .NET routes.
    '^/api/(auth|admin|menu|customers)(/|$)': { target: env.VITE_API_PROXY_TARGET || env.API_PROXY_TARGET || 'http://127.0.0.1:5088', changeOrigin: true },
  };
  return {
    plugins: [react(), {
      name: 'opgg-stats-backend',
      configureServer(server) { server.middlewares.use(createStatsMiddleware()); },
      configurePreviewServer(server) { server.middlewares.use(createStatsMiddleware()); },
    }],
    server: {
      proxy,
      watch: {
        ignored: ['**/.tools/**', '**/Phuc/**'],
      },
    },
    preview: { proxy },
  };
});
