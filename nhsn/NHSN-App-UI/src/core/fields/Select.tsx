import {useMemo} from 'react';
import {FormDropDownList, FormMultiSelect} from '@nhsn/nhsn-react-core';
import {toRenderProps, useFieldId, valueOf, type BaseFieldProps} from './fieldProps';

export interface SelectOption<T extends string> {
  value: T;
  label: string;
}

export interface SelectProps<T extends string> extends BaseFieldProps<T> {
  options: Array<SelectOption<T>>;
  placeholder?: string;
}

/**
 * Wraps `FormDropDownList`, which expects its options through
 * `customProp.data` with `dataItemKey`/`textField` naming the fields — and
 * emits the raw key on change when `dataItemKey` is set.
 */
export function Select<T extends string>({options, placeholder, ...base}: SelectProps<T>) {
  const id = useFieldId(base.id);
  const data = useMemo(
    () => options.map(option => ({key: option.value, value: option.label})),
    [options]
  );

  return FormDropDownList(
    toRenderProps({...base, id}, {
      customProp: {data, dataItemKey: 'key', textField: 'value'},
      defaultItem: placeholder ? {key: '', value: placeholder} : undefined,
      onChange: (event: unknown) => base.onChange(valueOf<T>(event))
    })
  );
}

export interface MultiSelectProps<T extends string> extends BaseFieldProps<T[]> {
  options: Array<SelectOption<T>>;
  placeholder?: string;
}

/**
 * Wraps `FormMultiSelect`, which — unlike the single Select above — takes
 * `data` and `dataItemKey` at the top level rather than in `customProp`.
 */
export function MultiSelect<T extends string>({
  options,
  placeholder,
  ...base
}: MultiSelectProps<T>) {
  const id = useFieldId(base.id);
  const data = useMemo(
    () => options.map(option => ({key: option.value, value: option.label})),
    [options]
  );
  const selected = useMemo(
    () => data.filter(item => (base.value ?? []).includes(item.key as T)),
    [data, base.value]
  );

  return FormMultiSelect(
    toRenderProps({...base, id, value: undefined}, {
      data,
      value: selected,
      dataItemKey: 'key',
      textField: 'value',
      placeholder,
      onChange: (event: unknown) => {
        const next = valueOf<Array<{key: T}>>(event) ?? [];
        base.onChange(next.map(item => item.key));
      }
    })
  );
}
