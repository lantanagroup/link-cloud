import React, {useMemo, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {
  FormCheckbox,
  FormDatePicker,
  FormInput,
  FormNumericTextBox,
  FormRadioGroup,
  FormSwitch,
  FormTextArea
} from '@nhsn/nhsn-react-core';
import {Calendar, type CalendarChangeEvent, type CalendarProps} from '@progress/kendo-react-dateinputs';
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
      onBlur: trimOnBlur(base, type === 'password')
    })
  );
}

export interface NumberFieldProps extends BaseFieldProps<number> {
  min?: number;
  max?: number;
  step?: number;
}

export function NumberField({min, max: _max, step, ...base}: NumberFieldProps) {
  const id = useFieldId(base.id);
  const blockMinus = min !== undefined && min >= 0;

  return FormNumericTextBox(
    toRenderProps({...base, id}, {
      step,
      // The package destructures customProp and reads customProp?.onBlur.
      customProp: {},
      onChange: (event: unknown) => base.onChange(valueOf<number>(event)),
      onKeyDown: blockMinus
        ? (event: React.KeyboardEvent) => {
            if (event.key === '-') {
              event.preventDefault();
            }
          }
        : undefined
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

function DateCalendarWithFooter(props: CalendarProps) {
  const {t} = useTranslation('common');
  const emitChange = (value: Date | null) =>
    props.onChange?.({value} as unknown as CalendarChangeEvent);

  return (
    <>
      <Calendar {...props} />
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
