import React, {useEffect, useRef} from 'react';
import {Button} from './layout';

export interface AddButtonProps {
  /** Already translated, e.g. "+ Add Code System". */
  label: string;
  onClick: () => void;
  disabled?: boolean;
}

/** The POC's `+ Add X` button, standalone - `RepeatableList` uses it internally for its own row add. */
export function AddButton({label, onClick, disabled}: AddButtonProps) {
  return (
    <Button variant="secondary" size="sm" disabled={disabled} onClick={onClick}>
      {label}
    </Button>
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
  /** Remove is hidden at this floor. The caller seeds rows up to it - this list never pads itself. */
  minItems?: number;
  /** Add is disabled at this ceiling. */
  maxItems?: number;
  disabled?: boolean;

  columnHeadings?: React.ReactNode[];
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
  disabled,
  columnHeadings
}: RepeatableListProps<T>) {
  const ids = useRef<string[]>([]);
  const sequence = useRef(0);
  const scrollRef = useRef<HTMLDivElement>(null);
  const scrollToBottomRef = useRef(false);

  while (ids.current.length < items.length) {
    sequence.current += 1;
    ids.current.push(`row-${sequence.current}`);
  }
  if (ids.current.length > items.length) {
    ids.current = ids.current.slice(0, items.length);
  }

  const atCeiling = maxItems !== undefined && items.length >= maxItems;
  const atFloor = items.length <= minItems;

  // Runs after the newly added row's own render commits, so scrollHeight already reflects it -
  // an add made from further up a long list would otherwise leave the new (blank, unfinished) row
  // out of view below the fold.
  useEffect(() => {
    if (scrollToBottomRef.current && scrollRef.current) {
      scrollRef.current.scrollTop = scrollRef.current.scrollHeight;
      scrollToBottomRef.current = false;
    }
  }, [items.length]);

  function handleItemChange(index: number, item: T) {
    onChange(items.map((existing, position) => (position === index ? item : existing)));
  }

  function handleRemove(index: number) {
    ids.current.splice(index, 1);
    onChange(items.filter((_item, position) => position !== index));
  }

  function handleAdd() {
    scrollToBottomRef.current = true;
    onChange([...items, newItem()]);
  }

  return (
    <div className="nhsn-link__repeatable">
      {columnHeadings && items.length > 0 && (
        <div className="nhsn-link__repeatable-row nhsn-link__repeatable-heading-row" aria-hidden="true">
          <div className="nhsn-link__repeatable-fields">
            {columnHeadings.map((heading, index) => (
              <span className="nhsn-link__repeatable-heading" key={index}>
                {heading}
              </span>
            ))}
          </div>
          <span className="nhsn-link__repeatable-heading-spacer" />
        </div>
      )}

      {items.length === 0 && emptyLabel && <p className="nhsn-link__hint-text">{emptyLabel}</p>}

      {items.length > 0 && (
        <div className="nhsn-link__repeatable-scroll" ref={scrollRef}>
          <ul className="nhsn-link__repeatable-list">
            {items.map((item, index) => (
              <li className="nhsn-link__repeatable-row" key={ids.current[index]}>
                <div className="nhsn-link__repeatable-fields">
                  {renderItem(item, index, next => handleItemChange(index, next))}
                </div>
                {/* At a non-zero floor, Remove is hidden rather than disabled - the facility can't
                    drop below it anyway, so a dead button is just noise. The spacer keeps the row's
                    columns the same width as when Remove comes back. */}
                {minItems > 0 && atFloor ? (
                  <span className="nhsn-link__repeatable-heading-spacer" aria-hidden="true" />
                ) : (
                  <Button
                    variant="secondary"
                    size="sm"
                    aria-label={`${removeLabel} ${index + 1}`}
                    disabled={disabled || atFloor}
                    onClick={() => handleRemove(index)}>
                    {removeLabel}
                  </Button>
                )}
              </li>
            ))}
          </ul>
        </div>
      )}

      <AddButton label={addLabel} onClick={handleAdd} disabled={disabled || atCeiling} />
    </div>
  );
}
