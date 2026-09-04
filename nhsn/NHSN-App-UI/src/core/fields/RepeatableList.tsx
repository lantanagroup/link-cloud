import React, {useRef} from 'react';

export interface AddButtonProps {
  /** Already translated, e.g. "+ Add Code System". */
  label: string;
  onClick: () => void;
  disabled?: boolean;
}

/** The POC's `+ Add X` button, standalone - `RepeatableList` uses it internally for its own row add. */
export function AddButton({label, onClick, disabled}: AddButtonProps) {
  return (
    <button type="button" className="nhsn-link__repeatable-add" disabled={disabled} onClick={onClick}>
      {label}
    </button>
  );
}

export interface RepeatableListProps<T> {
  items: T[];
  onChange: (items: T[]) => void;
  /** Row content. Compose it from the other components in this folder. */
  renderItem: (item: T, index: number, onItemChange: (item: T) => void) => React.ReactNode;
  /** Builds a blank row for the add control. */
  newItem: () => T;
  /** Already translated: the add button's text. */
  addLabel: string;
  /** Already translated: accessible name for each row's remove control. */
  removeLabel: string;
  /** Already translated: shown in place of the rows when there are none. */
  emptyLabel?: string;
  /** Remove is disabled at this floor. */
  minItems?: number;
  /** Add is disabled at this ceiling. */
  maxItems?: number;
  disabled?: boolean;
}

/** The POC's `.repeat-list` — a growable list of rows. Owns add/remove/row-identity only, never row content. */
export function RepeatableList<T>({
  items,
  onChange,
  renderItem,
  newItem,
  addLabel,
  removeLabel,
  emptyLabel,
  minItems = 0,
  maxItems,
  disabled
}: RepeatableListProps<T>) {
  const ids = useRef<string[]>([]);
  const sequence = useRef(0);

  while (ids.current.length < items.length) {
    sequence.current += 1;
    ids.current.push(`row-${sequence.current}`);
  }
  if (ids.current.length > items.length) {
    ids.current = ids.current.slice(0, items.length);
  }

  const atCeiling = maxItems !== undefined && items.length >= maxItems;
  const atFloor = items.length <= minItems;

  function handleItemChange(index: number, item: T) {
    onChange(items.map((existing, position) => (position === index ? item : existing)));
  }

  function handleRemove(index: number) {
    ids.current.splice(index, 1);
    onChange(items.filter((_item, position) => position !== index));
  }

  function handleAdd() {
    onChange([...items, newItem()]);
  }

  return (
    <div className="nhsn-link__repeatable">
      {items.length === 0 && emptyLabel && <p className="nhsn-link__hint-text">{emptyLabel}</p>}

      {items.length > 0 && (
        <ul className="nhsn-link__repeatable-list">
          {items.map((item, index) => (
            <li className="nhsn-link__repeatable-row" key={ids.current[index]}>
              <div className="nhsn-link__repeatable-fields">
                {renderItem(item, index, next => handleItemChange(index, next))}
              </div>
              <button
                type="button"
                className="nhsn-link__repeatable-remove"
                aria-label={`${removeLabel} ${index + 1}`}
                disabled={disabled || atFloor}
                onClick={() => handleRemove(index)}>
                {removeLabel}
              </button>
            </li>
          ))}
        </ul>
      )}

      <AddButton label={addLabel} onClick={handleAdd} disabled={disabled || atCeiling} />
    </div>
  );
}
