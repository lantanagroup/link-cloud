import React, {useId, useMemo, useRef, useState} from 'react';
import {useFieldId, type BaseFieldProps} from './fieldProps';
import {InfoTooltip} from './InfoTooltip';
import type {SelectOption} from './Select';

const CHIP_TONE_COUNT = 8;

export interface ChipMultiSelectProps<T extends string> extends BaseFieldProps<T[]> {
  options: Array<SelectOption<T>>;
  placeholder?: string;
  emptyText: string;
  selectedLabel: string;
  removeLabel: (optionLabel: string) => string;
  maxItems?: number;
}

/**
 * A type-to-filter picker that commits selections as removable pills, with the
 * pills in their own block above the input rather than inside it.
 *
 * `MultiSelect` in `Select.tsx` wraps the package's `FormMultiSelect` and is
 * the right choice for a short selection. This exists for the long one: the
 * package renders its chips inside the input, which turns a six-measure
 * selection into a scrolling field, and it cannot be configured out of that
 * layout. The two mirror each other's props so a call site can switch.
 */
export function ChipMultiSelect<T extends string>({
  options,
  placeholder,
  emptyText,
  selectedLabel,
  removeLabel,
  maxItems,
  ...base
}: ChipMultiSelectProps<T>) {
  const id = useFieldId(base.id);
  const prefix = useId();
  const listId = `${prefix}-list`;
  const inputRef = useRef<HTMLInputElement | null>(null);

  const [filter, setFilter] = useState('');
  const [open, setOpen] = useState(false);
  const [activeIndex, setActiveIndex] = useState(-1);

  const selected = base.value ?? [];
  const atCeiling = maxItems !== undefined && selected.length >= maxItems;

  const chips = useMemo(
    () =>
      selected.map(value => ({
        value,
        label: options.find(option => option.value === value)?.label ?? value
      })),
    [selected, options]
  );

  const matches = useMemo(() => {
    if (atCeiling) {
      return [];
    }
    const needle = filter.trim().toLowerCase();
    return options.filter(
      option =>
        !selected.includes(option.value) &&
        (needle.length === 0 || option.label.toLowerCase().includes(needle))
    );
  }, [options, selected, filter, atCeiling]);

  const active = activeIndex >= 0 && activeIndex < matches.length ? matches[activeIndex] : undefined;

  function commit(value: T) {
    base.onChange([...selected, value]);
    setFilter('');
    setActiveIndex(-1);
    inputRef.current?.focus();
  }

  function remove(value: T) {
    base.onChange(selected.filter(existing => existing !== value));
  }

  function moveActive(delta: number) {
    if (matches.length === 0) {
      return;
    }
    setOpen(true);
    setActiveIndex(current => {
      const next = current + delta;
      if (next < 0) {
        return matches.length - 1;
      }
      return next >= matches.length ? 0 : next;
    });
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLInputElement>) {
    switch (event.key) {
      case 'ArrowDown':
        event.preventDefault();
        moveActive(1);
        break;
      case 'ArrowUp':
        event.preventDefault();
        moveActive(-1);
        break;
      case 'Enter':
        if (active) {
          event.preventDefault();
          commit(active.value);
        }
        break;
      case 'Escape':
        setOpen(false);
        setActiveIndex(-1);
        break;
      case 'Backspace':
        if (filter.length === 0 && chips.length > 0) {
          remove(chips[chips.length - 1].value);
        }
        break;
      default:
        break;
    }
  }

  return (
    <div className="nhsn-link__chip-select-field">
      <label className="nhsn-link__chip-select-label" htmlFor={id}>
        {base.label}
        {base.required && (
          <span className="nhsn-link__chip-select-required" aria-hidden="true">
            {' *'}
          </span>
        )}
        {base.hint && <InfoTooltip label={base.label} content={base.hint} />}
      </label>

      <div className="nhsn-link__chip-select">
        {chips.length > 0 && (
          <ul className="nhsn-link__chips" aria-label={selectedLabel}>
            {chips.map(chip => (
              <li key={chip.value} className={`nhsn-link__chip ${toneClass(chip.label)}`}>
                {chip.label}
                <button
                  type="button"
                  className="nhsn-link__chip-remove"
                  aria-label={removeLabel(chip.label)}
                  disabled={base.disabled}
                  onClick={() => remove(chip.value)}>
                  <span aria-hidden="true">&times;</span>
                </button>
              </li>
            ))}
          </ul>
        )}

        <input
          ref={inputRef}
          id={id}
          type="text"
          className="nhsn-link__chip-select-input"
          role="combobox"
          autoComplete="off"
          aria-expanded={open}
          aria-controls={listId}
          aria-autocomplete="list"
          aria-activedescendant={active ? optionId(prefix, active.value) : undefined}
          aria-describedby={base.error ? `${id}-error` : undefined}
          aria-invalid={base.error ? true : undefined}
          aria-required={base.required || undefined}
          placeholder={placeholder}
          disabled={base.disabled}
          value={filter}
          onChange={event => {
            setFilter(event.target.value);
            setOpen(true);
            setActiveIndex(-1);
          }}
          onFocus={() => setOpen(true)}
          onBlur={() => {
            setOpen(false);
            setActiveIndex(-1);
            base.onBlur?.();
          }}
          onKeyDown={handleKeyDown} />

        <ul
          id={listId}
          className="nhsn-link__chip-select-list"
          role="listbox"
          aria-label={base.label}
          hidden={!open}>
          {matches.length === 0 ? (
            <li className="nhsn-link__chip-select-empty" role="presentation">
              {emptyText}
            </li>
          ) : (
            matches.map((option, index) => (
              <li
                key={option.value}
                id={optionId(prefix, option.value)}
                className={`nhsn-link__chip-select-option${
                  index === activeIndex ? ' nhsn-link__chip-select-option--active' : ''
                }`}
                role="option"
                aria-selected={index === activeIndex}
                // mousedown, not click: blur fires first on click and would
                // close the list before the selection landed.
                onMouseDown={event => {
                  event.preventDefault();
                  commit(option.value);
                }}
                onMouseEnter={() => setActiveIndex(index)}>
                {option.label}
              </li>
            ))
          )}
        </ul>
      </div>

      {base.error && (
        <p id={`${id}-error`} className="k-form-error" role="alert">
          {base.error}
        </p>
      )}
    </div>
  );
}

function optionId(prefix: string, value: string): string {
  return `${prefix}-option-${value}`;
}

function toneClass(label: string): string {
  const key = label.replace(/\s*\((?:simulated|placeholder)\)\s*$/i, '');
  let hash = 0;
  for (let index = 0; index < key.length; index += 1) {
    hash = (hash * 31 + key.charCodeAt(index)) >>> 0;
  }
  return `nhsn-link__chip--tone-${hash % CHIP_TONE_COUNT}`;
}
