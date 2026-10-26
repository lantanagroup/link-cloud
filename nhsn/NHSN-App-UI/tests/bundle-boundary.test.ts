import {execFileSync} from 'node:child_process';
import {existsSync, readFileSync, statSync} from 'node:fs';
import path from 'node:path';
import {beforeAll, describe, expect, it} from 'vitest';

/**
 * The check that actually holds the core/shell boundary.
 *
 * The ESLint rule gives faster feedback but can be disabled inline, and a
 * re-export can carry shell code across without tripping it. This asserts on
 * the artifact we actually ship: if any of these markers appear, we are
 * shipping a token-forging harness into the CDC NHSN App.
 *
 * Building takes ~20s, so this is deliberately a separate suite from the unit
 * tests — run it in CI on every change to src/, not on every save.
 */
const root = path.resolve(__dirname, '..');
const bundlePath = path.join(root, 'dist', 'embed', 'nhsn-link.js');

/** Strings unique to shell/auth. Minification renames identifiers but not string literals or property names. */
const FORBIDDEN = [
  'BEGIN PRIVATE KEY',
  'privateKeyPem',
  'setProtectedHeader',
  'importPKCS8',
  'TestAuthApiClient',
  'MockApiClient',
  'nhsn-app-ui.testUsers'
];

describe('embed bundle boundary', () => {
  let bundle: string;

  beforeAll(() => {
    // Run webpack's JS entry through the current Node binary rather than the
    // `npx` shim: Node refuses to spawn a .cmd on Windows without a shell,
    // and going through a shell would make the command quoting platform-
    // dependent for no gain.
    execFileSync(
      process.execPath,
      [
        require.resolve('webpack-cli/bin/cli.js'),
        '--config',
        'webpack.embed.config.js',
        '--mode',
        'production'
      ],
      {cwd: root, stdio: 'pipe'}
    );
    expect(existsSync(bundlePath), 'embed bundle was not emitted').toBe(true);
    bundle = readFileSync(bundlePath, 'utf8');
  }, 300_000);

  it.each(FORBIDDEN)('does not contain shell marker %s', marker => {
    expect(bundle).not.toContain(marker);
  });

  it('does not reference the shell source tree', () => {
    expect(bundle).not.toMatch(/src[\\/]shell[\\/]/);
  });

  it('stays within the size budget', () => {
    // Not a performance nicety: this artifact loads inside someone else's
    // page. Raise it deliberately, with a reason, or split a chunk instead.
    const budgetBytes = 2_500_000;
    expect(statSync(bundlePath).size).toBeLessThan(budgetBytes);
  });
});
