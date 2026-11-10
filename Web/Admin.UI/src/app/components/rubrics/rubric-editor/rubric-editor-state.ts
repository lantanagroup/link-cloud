import { IRubricDefinition } from '../../../interfaces/validation/rubric.interface';

// What is handed to the draft editor through the browser's history when a page sends someone there. It is
// in a file of its own so a page can name it without loading the editor.

/** A definition for the editor to open, unsaved, in place of a blank one or a copy. */
export const EDITOR_DEFINITION_STATE = 'rubricEditorDefinition';

/** A message for the editor to show once it has opened. */
export const EDITOR_NOTICE_STATE = 'rubricEditorNotice';

function take<T>(key: string, accept: (value: unknown) => value is T): T | null {
  const state = window.history.state as Record<string, unknown> | null;
  const value = state?.[key];
  if (!accept(value)) {
    return null;
  }
  // Removed once read, so reloading the page does not act on it a second time.
  window.history.replaceState({ ...state, [key]: undefined }, '');
  return value;
}

/** The message handed to the editor, read once. */
export function takeEditorNotice(): string | null {
  return take(EDITOR_NOTICE_STATE, (value): value is string => typeof value === 'string');
}

/** The definition handed to the editor, read once. */
export function takeEditorDefinition(): IRubricDefinition | null {
  return take(EDITOR_DEFINITION_STATE,
    (value): value is IRubricDefinition => !!value && typeof value === 'object' && 'id' in value && 'semver' in value);
}
