import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: './src/test/setup.ts',
    // MSW's setupServer patches process-wide network interception, and
    // mocks/handlers.ts holds mutable module state (mockProducts/mockCategories) -
    // running test files in parallel let one file's resetMockProducts()/in-flight
    // request interfere with another's, causing sporadic timeouts under the full
    // suite despite every file passing reliably in isolation.
    fileParallelism: false,
  },
})
