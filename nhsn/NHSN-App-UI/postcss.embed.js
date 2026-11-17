/**
 * Scopes every rule we ship to the `nhsn-link` custom element, for the embed
 * build only.
 *
 * The component renders directly into `<nhsn-link>` with no shadow DOM,
 * because the ADR asks it to "support React.js and NHSN App shared UI
 * styles… so the NHSNLink experience is consistent with the NHSN App". Host
 * CSS reaching our subtree is therefore intended. This pass stops the traffic
 * in the other direction: `.btn` becomes `nhsn-link .btn` and no longer
 * collides with the host's own `.btn`.
 *
 * The prefix is the element, not a `.nhsn-link` class — the element is the
 * component's root in this build, so it scopes the whole subtree with no
 * markup change.
 */
const ROOT = 'nhsn-link';

/** Selectors that must not be prefixed, because prefixing changes their meaning. */
const PASSTHROUGH = /^(from|to|\d+%|:root|html|body|\*)$/;

module.exports = () => ({
  postcssPlugin: 'nhsn-link-scope',
  Rule(rule) {
    // Keyframe steps are not selectors.
    if (rule.parent && rule.parent.type === 'atrule' && /keyframes$/.test(rule.parent.name)) {
      return;
    }

    rule.selectors = rule.selectors
      .map(selector => {
        const trimmed = selector.trim();

        // `:root` variables must land on our element or the custom properties
        // resolve against the host document instead.
        if (trimmed === ':root' || trimmed === 'html' || trimmed === 'body') {
          return ROOT;
        }

        // A bare element selector would restyle the host's own markup.
        if (/^[a-zA-Z][a-zA-Z0-9]*$/.test(trimmed) && !PASSTHROUGH.test(trimmed)) {
          return `${ROOT} ${trimmed}`;
        }

        if (trimmed.startsWith(ROOT)) {
          return trimmed;
        }

        return `${ROOT} ${trimmed}`;
      })
      .filter(Boolean);
  }
});

module.exports.postcss = true;
