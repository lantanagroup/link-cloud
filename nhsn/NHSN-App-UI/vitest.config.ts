import {defineConfig} from 'vitest/config';

export default defineConfig({
  test: {
    environment: 'jsdom',
    globals: false,
    setupFiles: ['src/test-setup.ts'],
    // Node 25 and later define their own global localStorage, which hides jsdom's and is
    // undefined without a backing file.
    pool: 'forks',
    poolOptions: {
      forks: {
        execArgv: ['--no-experimental-webstorage']
      }
    },
    // The bundle-boundary suite builds the artifact and takes ~20s, so it is
    // excluded from the default run and invoked explicitly by `npm run
    // test:boundary` and in CI.
    include: ['src/**/*.test.{ts,tsx}'],
    exclude: ['node_modules/**', 'dist/**']
  }
});
