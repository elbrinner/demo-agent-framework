/// <reference types="vitest" />
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/bri-agent': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
      '/api': {
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  }
});
