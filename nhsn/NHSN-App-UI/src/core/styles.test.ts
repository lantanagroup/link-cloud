import {readFileSync, readdirSync, statSync} from 'node:fs';
import path from 'node:path';
import {describe, expect, it} from 'vitest';

/**
 * Every `nhsn-link__*` class the component renders must have a rule in
 * NHSNLink.css.
 *
 * Typecheck, lint and behaviour tests all pass on markup with no styling —
 * class names are just strings. This catches the case where a step adds
 * markup and forgets the stylesheet, which shows up as an unstyled screen
 * rather than a failure.
 *
 * It checks that a selector exists, not that it looks right. Appearance is a
 * human judgement; a missing rule is not.
 */
const coreDir = path.resolve(__dirname);
const stylesheet = readFileSync(path.join(coreDir, 'NHSNLink.css'), 'utf8');

function sourceFiles(dir: string): string[] {
  return readdirSync(dir).flatMap(entry => {
    const full = path.join(dir, entry);
    if (statSync(full).isDirectory()) {
      return sourceFiles(full);
    }
    return /\.tsx$/.test(full) && !/\.test\.tsx$/.test(full) ? [full] : [];
  });
}

const usedClasses = new Set<string>();
for (const file of sourceFiles(coreDir)) {
  const contents = readFileSync(file, 'utf8');
  for (const match of contents.matchAll(/nhsn-link__[a-z0-9-]+/g)) {
    usedClasses.add(match[0]);
  }
}

describe('component stylesheet', () => {
  it('finds classes to check', () => {
    // Guards the guard: a broken scan would make every assertion below vacuous.
    expect(usedClasses.size).toBeGreaterThan(10);
  });

  it.each([...usedClasses].sort())('NHSNLink.css defines .%s', className => {
    expect(stylesheet).toContain(`.${className}`);
  });

  it('resets list styling on the step rail', () => {
    // The step index is its own element, so the <ol> markers must be off or
    // each row renders its number twice.
    const rule = stylesheet.match(/\.nhsn-link__steps\s*\{[^}]*\}/)?.[0] ?? '';
    expect(rule).toMatch(/list-style:\s*none/);
  });
});
