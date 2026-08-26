import React, {useMemo} from 'react';
import {
  FormCheckbox,
  FormDatePicker,
  FormInput,
  FormNumericTextBox,
  FormRadioGroup,
  FormSwitch,
  FormTextArea
} from '@nhsn/nhsn-react-core';
import {toRenderProps, useFieldId, valueOf, type BaseFieldProps} from './fieldProps';

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
      onChange: (event: unknown) => base.onChange(valueOf<string>(event) ?? '')
    })
  );
}

export interface NumberFieldProps extends BaseFieldProps<number> {
  min?: number;
  max?: number;
  step?: number;
}

export function NumberField({min, max, step, ...base}: NumberFieldProps) {
  const id = useFieldId(base.id);
  return FormNumericTextBox(
    toRenderProps({...base, id}, {
      min,
      max,
      step,
      // The package destructures customProp and reads customProp?.onBlur.
      customProp: {},
      onChange: (event: unknown) => base.onChange(valueOf<number>(event))
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
      onChange: (event: unknown) => base.onChange(valueOf<string>(event) ?? '')
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

/**
 * Exchanges ISO date strings, not Date objects — the draft is serialized to
 * JSON and round-tripped through the BFF, and a Date would not survive it.
 */
export function DateField({min, max, ...base}: DateFieldProps) {
  const id = useFieldId(base.id);
  const value = base.value ? new Date(base.value) : null;
  return FormDatePicker(
    toRenderProps({...base, id, value: undefined}, {
      value,
      min,
      max,
      customProps: {},
      onFocus: () => undefined,
      onChange: (event: unknown) => {
        const next = valueOf<Date | null>(event);
        base.onChange(next ? next.toISOString().slice(0, 10) : '');
      }
    })
  );
}
