import { defineConfig, loadEnv } from 'vite';
import { fileURLToPath } from 'node:url';
import react from '@vitejs/plugin-react';
import { createStatsMiddleware } from '../Backend/integrations/opgg/statsMiddleware.js';

export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, fileURLToPath(new URL('.', import.meta.url)), '');
  const proxy = {
    // Keep OP.GG middleware local; forward only the .NET auth/admin routes.
    '^/api/(auth|admin)(/|$)': { target: env.API_PROXY_TARGET || 'http://127.0.0.1:5088', changeOrigin: true },
    '^/hubs/realtime([/?]|$)': { target: env.API_PROXY_TARGET || 'http://127.0.0.1:5088', changeOrigin: true, ws: true },
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
    build: { rollupOptions: { output: { manualChunks: { signalr: ['@microsoft/signalr'] } } } },
  };
});
