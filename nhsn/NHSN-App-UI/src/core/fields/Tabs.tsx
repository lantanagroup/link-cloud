import React, {useId, useRef} from 'react';

export interface TabDefinition<T extends string> {
  id: T;
  /** Already translated by the caller — components in this folder never call `t`. */
  label: string;
  disabled?: boolean;
}

export interface TabsProps<T extends string> {
  tabs: Array<TabDefinition<T>>;
  /** Undefined while nothing is chosen yet — the list stays keyboard-reachable. */
  activeTab?: T;
  onTabChange: (id: T) => void;
  /** Accessible name for the tab list, e.g. the label of the field it selects. */
  label: string;
  /** The panel for the active tab. Rendered inside a `tabpanel` bound to it. */
  children?: React.ReactNode;
}

/** Segmented tab row (the POC's `.btn-group`), controlled, with the ARIA tabs pattern. */
export function Tabs<T extends string>({tabs, activeTab, onTabChange, label, children}: TabsProps<T>) {
  const prefix = useId();
  const listRef = useRef<HTMLDivElement | null>(null);

  const tabId = (id: T) => `${prefix}-tab-${id}`;
  const panelId = `${prefix}-panel`;

  // One tab always holds the roving tabindex, even before anything is selected.
  const selectable = tabs.filter(tab => !tab.disabled);
  const rovingId = selectable.some(tab => tab.id === activeTab) ? activeTab : selectable[0]?.id;

  function focusTab(index: number) {
    const enabled = tabs.filter(tab => !tab.disabled);
    if (enabled.length === 0) {
      return;
    }
    const next = enabled[(index + enabled.length) % enabled.length];
    onTabChange(next.id);
    // useId ids contain colons, invalid in a CSS selector - match by id instead.
    const targetId = tabId(next.id);
    window.requestAnimationFrame(() => {
      const buttons = listRef.current?.querySelectorAll<HTMLButtonElement>('[role="tab"]') ?? [];
      Array.from(buttons)
        .find(button => button.id === targetId)
        ?.focus();
    });
  }

  function handleKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    const enabled = tabs.filter(tab => !tab.disabled);
    // -1 while nothing is selected, so the first arrow press lands on an end
    // rather than two places away from one.
    const current = enabled.findIndex(tab => tab.id === activeTab);

    switch (event.key) {
      case 'ArrowRight':
      case 'ArrowDown':
        event.preventDefault();
        focusTab(current < 0 ? 0 : current + 1);
        break;
      case 'ArrowLeft':
      case 'ArrowUp':
        event.preventDefault();
        focusTab(current < 0 ? enabled.length - 1 : current - 1);
        break;
      case 'Home':
        event.preventDefault();
        focusTab(0);
        break;
      case 'End':
        event.preventDefault();
        focusTab(enabled.length - 1);
        break;
      default:
        break;
    }
  }

  return (
    <>
      <div
        ref={listRef}
        className="nhsn-link__tabs"
        role="tablist"
        aria-label={label}
        onKeyDown={handleKeyDown}>
        {tabs.map(tab => {
          const selected = tab.id === activeTab;
          return (
            <button
              key={tab.id}
              id={tabId(tab.id)}
              type="button"
              role="tab"
              className={`nhsn-link__tab${selected ? ' nhsn-link__tab--selected' : ''}`}
              aria-selected={selected}
              aria-controls={children === undefined ? undefined : panelId}
              // Roving tabindex: one stop for the whole list, arrows move within it.
              tabIndex={tab.id === rovingId ? 0 : -1}
              disabled={tab.disabled}
              onClick={() => onTabChange(tab.id)}>
              {tab.label}
            </button>
          );
        })}
      </div>

      {children !== undefined && (
        <div
          id={panelId}
          role="tabpanel"
          aria-labelledby={activeTab && tabs.some(tab => tab.id === activeTab) ? tabId(activeTab) : undefined}
          tabIndex={-1}>
          {children}
        </div>
      )}
    </>
  );
}
