import {defineConfig} from 'vitest/config';

/**
 * The bundle-boundary suite, separated because it builds the embed artifact
 * and takes roughly twenty seconds. Run it in CI on every change to src/ —
 * it is the check that actually proves shell code stays out of the bundle.
 */
export default defineConfig({
  test: {
    environment: 'node',
    include: ['tests/**/*.test.ts'],
    testTimeout: 300_000,
    hookTimeout: 300_000
  }
});
