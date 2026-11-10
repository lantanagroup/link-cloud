import { CanDeactivateFn } from '@angular/router';

/**
 * Asks before leaving the draft editor with changes that are not saved.
 *
 * It is in a file of its own so the routes can name it without loading the editor with them. After a
 * save the address moves to the saved draft, but by then nothing is unsaved, so that move is not asked
 * about.
 */
export const confirmLeavingRubricEditor: CanDeactivateFn<{ dirty: boolean }> = editor =>
  !editor.dirty || window.confirm('This draft has changes that are not saved. Leave without saving them?');
