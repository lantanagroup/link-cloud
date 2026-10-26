const js = require('@eslint/js');
const tseslint = require('typescript-eslint');

const SHELL_AND_JOSE = [
  {
    group: ['**/shell/**', '../shell/*', '../../shell/*', '../../../shell/*'],
    message:
      'core/ must not import shell/. Shell code handles private keys; anything core imports ships in the embed bundle inside the NHSN App.'
  },
  {
    group: ['jose'],
    message:
      'JWT signing belongs in shell/auth. The component never touches the token — an ADR requirement.'
  }
];

const DESIGN_SYSTEM = {
  group: ['@nhsn/nhsn-react-core', '@nhsn/nhsn-react-core/*', '@progress/*'],
  message:
    'Import design-system components from core/fields, not directly. A swap should be one folder, not thirteen step directories.'
};

/**
 * The boundary rule.
 *
 * Webpack bundles whatever the entry file imports, transitively, so keeping
 * shell code out of `dist/embed/nhsn-link.js` means nothing on that chain may
 * import it. This gives fast feedback; `tests/bundle-boundary.test.ts` is what
 * actually proves it, because a lint rule can be disabled inline and a
 * re-export can carry code across without tripping it.
 */
module.exports = tseslint.config(
  {ignores: ['dist/**', 'node_modules/**', 'server/**', '*.config.js', 'vitest.config.ts']},
  js.configs.recommended,
  ...tseslint.configs.recommended,
  {
    languageOptions: {parserOptions: {ecmaFeatures: {jsx: true}}},
    rules: {
      '@typescript-eslint/no-unused-vars': [
        'error',
        {argsIgnorePattern: '^_', varsIgnorePattern: '^_'}
      ],
      '@typescript-eslint/no-explicit-any': 'warn'
    }
  },
  {
    files: ['src/core/**/*.{ts,tsx}'],
    ignores: ['src/core/fields/**'],
    rules: {
      'no-restricted-imports': ['error', {patterns: [...SHELL_AND_JOSE, DESIGN_SYSTEM]}]
    }
  },
  {
    // The design-system adapter is the one place the package may be named.
    files: ['src/core/fields/**/*.{ts,tsx}'],
    rules: {
      'no-restricted-imports': ['error', {patterns: SHELL_AND_JOSE}]
    }
  },
  {
    // Tests are on neither entry point's import graph, so nothing they import
    // can reach a bundle. A core test injecting MockApiClient is the intended
    // use of the port, not a boundary violation.
    files: ['**/*.test.{ts,tsx}', 'tests/**/*.{ts,tsx}', 'src/test-setup.ts'],
    rules: {'no-restricted-imports': 'off'}
  }
);
