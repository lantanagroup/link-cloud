/**
 * Builds the props object the nhsn-react-core field renderers expect.
 *
 * Those renderers are typed as `(fieldRenderProps: FieldRenderProps) => JSX`,
 * which reads like they must live inside a Kendo `<Form>`. They do not — every
 * one is a plain function of its props with no context lookup, so we drive
 * them straight from the reducer and keep the draft the single writer.
 *
 * Two inconsistencies in the package this file exists to absorb:
 *  - `FormDropDownList` takes its options via `customProp.data`, while
 *    `FormMultiSelect` takes `data` at the top level.
 *  - `FormDropDownList` calls `onBlur()` unguarded, so it must always be
 *    supplied; `FormInput` guards it.
 */

import type {FieldRenderProps} from '@progress/kendo-react-form';

let sequence = 0;

/** Stable per-field id, needed for the label/error `aria-describedby` wiring. */
export function useFieldId(explicit?: string): string {
  if (explicit) {
    return explicit;
  }
  sequence += 1;
  return `nhsn-field-${sequence}`;
}

export interface BaseFieldProps<T> {
  label: string;
  value?: T;
  onChange: (value: T) => void;
  onBlur?: () => void;
  /** Message key already resolved by the caller — fields never call `t` themselves. */
  error?: string;
  hint?: string;
  required?: boolean;
  disabled?: boolean;
  id?: string;
}

export interface RenderPropsInput<T> extends BaseFieldProps<T> {
  id: string;
}

/**
 * The renderers are typed as taking Kendo's full `FieldRenderProps` — the
 * bookkeeping a Kendo `<Form>` would supply — but at runtime each destructures
 * only the dozen properties below and spreads the rest onto its Kendo control.
 *
 * The assertion is the whole point of this file: it is made once, here, where
 * the mismatch is explained, rather than at each of the fifteen call sites.
 */
export function toRenderProps<T>(
  props: RenderPropsInput<T>,
  extra: Record<string, unknown> = {}
): FieldRenderProps {
  const renderProps = {
    id: props.id,
    name: props.id,
    label: props.label,
    value: props.value,
    disabled: props.disabled,
    required: props.required ? 1 : 0,
    hint: props.hint,
    // `touched` gates the message: the package only renders validationMessage
    // when touched is true, so an error we were asked to show must set both.
    touched: Boolean(props.error),
    visited: Boolean(props.error),
    modified: false,
    valid: !props.error,
    validationMessage: props.error,
    onBlur: props.onBlur ?? noop,
    onFocus: noop,
    onChange: noop,
    ...extra
  };

  return renderProps as unknown as FieldRenderProps;
}

/** Kendo change events carry the new value on `event.value`. */
export function valueOf<T>(event: unknown): T {
  return (event as {value: T})?.value;
}

function noop() {
  // FormDropDownList calls onBlur() without a guard.
}
