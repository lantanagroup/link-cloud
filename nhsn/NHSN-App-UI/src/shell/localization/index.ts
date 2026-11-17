import i18n from 'i18next';
import shellStrings from './shell-strings.json';

/**
 * Bundled strings for the standalone harness.
 *
 * These live in `shell/` rather than in core's bundled fallback because the
 * harness is never in the embed bundle — shipping its strings there is dead
 * weight, and one of them is a PEM placeholder that reads like a leaked
 * private key when found in the artifact.
 *
 * Merged rather than replaced, and without overwriting: whatever the BFF
 * serves still wins. This only stops the harness rendering raw keys when
 * running offline against `MockApiClient`.
 */
export function addShellStrings(): void {
  i18n.addResourceBundle('en-US', 'common', shellStrings, true, false);
}
