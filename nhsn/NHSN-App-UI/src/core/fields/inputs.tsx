import React, {useMemo, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {
  FormCheckbox,
  FormDatePicker,
  FormInput,
  FormNumericTextBox,
  FormRadioGroup,
  FormSwitch,
  FormTextArea,
  MistFormLabel
} from '@nhsn/nhsn-react-core';
import {Calendar, type CalendarChangeEvent, type CalendarProps} from '@progress/kendo-react-dateinputs';
import {FieldWrapper} from '@progress/kendo-react-form';
import {Input} from '@progress/kendo-react-inputs';
import {Error as KendoError, Hint} from '@progress/kendo-react-labels';
import {FaEye, FaEyeSlash} from 'react-icons/fa';
import {toRenderProps, useFieldId, valueOf, type BaseFieldProps} from './fieldProps';

function trimOnBlur(base: BaseFieldProps<string>, skip?: boolean) {
  return () => {
    if (!skip && typeof base.value === 'string') {
      const trimmed = base.value.trim();
      if (trimmed !== base.value) {
        base.onChange(trimmed);
      }
    }
    base.onBlur?.();
  };
}

export interface TextFieldProps extends BaseFieldProps<string> {
  placeholder?: string;
  maxLength?: number;
  type?: 'text' | 'url' | 'email' | 'password';
}

export function TextField({placeholder, maxLength, type = 'text', ...base}: TextFieldProps) {
  const id = useFieldId(base.id);
  const isPassword = type === 'password';

  if (!isPassword) {
    return FormInput(
      toRenderProps({...base, id}, {
        type,
        placeholder,
        maxLength,
        // These values are never worth the browser re-suggesting - onboarding
        // data, not something like a saved address - and its suggestion list
        // overlaps a repeatable list's rows when one is open.
        autoComplete: 'off',
        onChange: (event: unknown) => base.onChange(valueOf<string>(event) ?? ''),
        onBlur: trimOnBlur(base)
      })
    );
  }

  return (
    <PasswordField
      {...base}
      id={id}
      placeholder={placeholder}
      maxLength={maxLength}
    />
  );
}

/**
 * Mirrors FormInput's own markup (label, input row, hint/error) instead of
 * wrapping its output, so the reveal toggle can sit inside the same
 * position-relative row as the input and stay vertically centered on it no
 * matter how tall the label or hint text is.
 */
function PasswordField({placeholder, maxLength, ...base}: Omit<TextFieldProps, 'type'>) {
  const id = useFieldId(base.id);
  const [revealed, setRevealed] = useState(false);
  const showValidationMessage = Boolean(base.error);
  const showHint = !showValidationMessage && base.hint;
  const hintId = showHint ? `${id}_hint` : '';
  const errorId = showValidationMessage ? `${id}_error` : '';

  return (
    <FieldWrapper>
      <MistFormLabel editorId={id} editorValid={!base.error} editorDisabled={base.disabled} required={base.required ? 1 : 0}>
        {base.label}
      </MistFormLabel>
      <div className="vertical-flex nhsn-link__password-row">
        <Input
          valid={!base.error}
          type={revealed ? 'text' : 'password'}
          id={id}
          name={id}
          disabled={base.disabled}
          ariaDescribedBy={`${hintId} ${errorId}`}
          value={base.value ?? ''}
          placeholder={placeholder}
          maxLength={maxLength}
          autoComplete="off"
          required={Boolean(base.required)}
          formNoValidate
          onInvalid={() => undefined}
          onChange={(event: unknown) => base.onChange(valueOf<string>(event) ?? '')}
          onBlur={trimOnBlur(base, true)}
        />
        <button
          type="button"
          className="nhsn-link__password-toggle"
          onClick={() => setRevealed(prev => !prev)}
          aria-label={revealed ? 'Hide password' : 'Show password'}
          aria-pressed={revealed}>
          {revealed ? <FaEye /> : <FaEyeSlash />}
        </button>
      </div>
      {showHint && <Hint id={hintId}>{base.hint}</Hint>}
      {showValidationMessage && <KendoError id={errorId}>{base.error}</KendoError>}
    </FieldWrapper>
  );
}

export interface NumberFieldProps extends BaseFieldProps<number> {
  min?: number;
  max?: number;
  step?: number;
}

export function NumberField({min, max, step, ...base}: NumberFieldProps) {
  const id = useFieldId(base.id);
  const blockMinus = min !== undefined && min >= 0;

  return FormNumericTextBox(
    toRenderProps({...base, id}, {
      min,
      max,
      step,
      // Every caller of this field wants a whole number - 'n0' keeps Kendo
      // from formatting/accepting fractional digits.
      format: 'n0',
      // The package destructures customProp and reads customProp?.onBlur.
      customProp: {},
      onChange: (event: unknown) => {
        const value = valueOf<number>(event);
        base.onChange(
          value === null || value === undefined ? value : Math.trunc(value)
        );
      },
      onKeyDown: (event: React.KeyboardEvent) => {
        if (blockMinus && event.key === '-') {
          event.preventDefault();
        }
        // Blocks both '.' and locale decimal separators (e.g. ',') - typing a
        // fraction into a whole-number field should do nothing, not round later.
        if (event.key === '.' || event.key === ',') {
          event.preventDefault();
        }
      }
    })
  );
}

export interface TextAreaFieldProps extends BaseFieldProps<string> {
  rows?: number;
  placeholder?: string;
}

export function TextAreaField({rows = 4, placeholder, ...base}: TextAreaFieldProps) {
  const id = useFieldId(base.id);
  return FormTextArea(
    toRenderProps({...base, id}, {
      rows,
      placeholder,
      onChange: (event: unknown) => base.onChange(valueOf<string>(event) ?? ''),
      onBlur: trimOnBlur(base)
    })
  );
}

export type CheckboxFieldProps = BaseFieldProps<boolean>;

export function CheckboxField(props: CheckboxFieldProps) {
  const id = useFieldId(props.id);
  return FormCheckbox(
    toRenderProps({...props, id}, {
      onChange: (event: unknown) => props.onChange(Boolean(valueOf<boolean>(event)))
    })
  );
}

export type SwitchFieldProps = BaseFieldProps<boolean>;

export function SwitchField(props: SwitchFieldProps) {
  const id = useFieldId(props.id);
  return FormSwitch(
    toRenderProps({...props, id}, {
      onChange: (event: unknown) => props.onChange(Boolean(valueOf<boolean>(event)))
    })
  );
}

export interface RadioOption<T extends string> {
  value: T;
  label: string;
}

export interface RadioGroupFieldProps<T extends string> extends BaseFieldProps<T> {
  options: Array<RadioOption<T>>;
  layout?: 'horizontal' | 'vertical';
}

export function RadioGroupField<T extends string>({
  options,
  layout = 'vertical',
  ...base
}: RadioGroupFieldProps<T>) {
  const id = useFieldId(base.id);
  const data = useMemo(
    () => options.map(option => ({label: option.label, value: option.value})),
    [options]
  );
  return FormRadioGroup(
    toRenderProps({...base, id}, {
      data,
      layout,
      onChange: (event: unknown) => base.onChange(valueOf<T>(event))
    })
  );
}

/** Yes/No is common enough in the MRN step to be worth naming. */
export interface YesNoFieldProps extends Omit<BaseFieldProps<boolean>, 'value' | 'onChange'> {
  value?: boolean;
  onChange: (value: boolean) => void;
  yesLabel: string;
  noLabel: string;
}

export function YesNoField({yesLabel, noLabel, value, onChange, ...base}: YesNoFieldProps) {
  return (
    <RadioGroupField<'yes' | 'no'>
      {...base}
      layout="horizontal"
      options={[
        {value: 'yes', label: yesLabel},
        {value: 'no', label: noLabel}
      ]}
      value={value === undefined ? undefined : value ? 'yes' : 'no'}
      onChange={next => onChange(next === 'yes')}
    />
  );
}

export interface DateFieldProps extends BaseFieldProps<string> {
  min?: Date;
  max?: Date;
}

const DATE_DISPLAY_FORMAT = 'dd-MM-yyyy';
const DATE_MASK = {year: 'yyyy', month: 'mm', day: 'dd'};

function markCalendarAsApplication(instance: {element: HTMLDivElement | null} | null) {
  instance?.element?.setAttribute('role', 'application');
}

function DateCalendarWithFooter(props: CalendarProps) {
  const {t} = useTranslation('common');
  const emitChange = (value: Date | null) =>
    props.onChange?.({value} as unknown as CalendarChangeEvent);

  return (
    <>
      <Calendar
        {...props}
        _ref={instance => {
          props._ref?.(instance);
          markCalendarAsApplication(instance);
        }}
      />
      <div className="nhsn-link__date-popup-footer">
        <button type="button" className="nhsn-link__date-popup-footer-link" onClick={() => emitChange(null)}>
          {t('actions.clear')}
        </button>
        <button
          type="button"
          className="nhsn-link__date-popup-footer-link"
          onClick={() => emitChange(new Date())}>
          {t('actions.today')}
        </button>
      </div>
    </>
  );
}

/**
 * Exchanges ISO date strings, not Date objects — the draft is serialized to
 * JSON and round-tripped through the BFF, and a Date would not survive it.
 */
export function DateField({min, max, ...base}: DateFieldProps) {
  const id = useFieldId(base.id);
  const [popupContainer, setPopupContainer] = useState<HTMLDivElement | null>(null);

  return (
    <div className="nhsn-link__date-field" ref={setPopupContainer} aria-live="polite">
      {FormDatePicker(
        toRenderProps({...base, id, value: toPickerDate(base.value)}, {
          min,
          max,
          format: DATE_DISPLAY_FORMAT,
          formatPlaceholder: DATE_MASK,
          popupSettings: {
            popupClass: 'nhsn-link__date-popup',
            ...(popupContainer ? {appendTo: popupContainer} : {})
          },
          customProps: {calendar: DateCalendarWithFooter},
          onFocus: () => undefined,
          onChange: (event: unknown) => base.onChange(toIsoDate(valueOf<string | null>(event)))
        })
      )}
    </div>
  );
}

function toPickerDate(value?: string): string | undefined {
  const match = value?.match(/^(\d{4})-(\d{2})-(\d{2})$/);
  return match ? `${match[2]}/${match[3]}/${match[1]}` : undefined;
}

function toIsoDate(value: string | null): string {
  const match = value?.match(/^(\d{2})\/(\d{2})\/(\d{4})$/);
  return match ? `${match[3]}-${match[1]}-${match[2]}` : '';
}
