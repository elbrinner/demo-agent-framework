/// <reference types="vitest" />
import { defineConfig } from 'vitest/config'; // usar vitest/config para soportar la clave test

export default defineConfig({
  test: {
    globals: true,
    environment: 'happy-dom', // entorno ligero para pruebas de componentes
    setupFiles: ['./src/test/setup.ts'],
  },
});