import { defineConfig } from 'vitest/config'
import { resolve } from 'node:path'

export default defineConfig({
  test: {
    environment: 'happy-dom',
    globals: true,
    include: ['tests/**/*.test.ts']
  },
  resolve: {
    alias: {
      '~': resolve(import.meta.dirname, './app'),
      '@': resolve(import.meta.dirname, './app'),
      '~~': resolve(import.meta.dirname, '.'),
      '@@': resolve(import.meta.dirname, '.')
    }
  }
})
