import React, {useEffect, useId, useRef} from 'react';

export interface ModalProps {
  open: boolean;
  /** Already translated. Names the dialog for assistive technology. */
  title: string;
  onClose: () => void;
  children: React.ReactNode;
  /** Action buttons. The caller supplies them so the dialog owns no verbs. */
  footer?: React.ReactNode;
  size?: 'small' | 'medium' | 'large';
}

const FOCUSABLE =
  'a[href], button:not([disabled]), textarea:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])';

/**
 * A dialog hosting arbitrary content (a table, a form) - use `AlertDialog`/
 * `ConfirmDialog` from `nhsn-react-core` for a message-and-choice instead.
 * Rendered inline, not portalled: the embed build's selector rewrite only
 * reaches nodes inside this component's own subtree.
 */
export function Modal({open, title, onClose, children, footer, size = 'medium'}: ModalProps) {
  const titleId = useId();
  const dialogRef = useRef<HTMLDivElement | null>(null);
  const restoreFocusTo = useRef<Element | null>(null);

  useEffect(() => {
    if (!open) {
      return;
    }

    restoreFocusTo.current = document.activeElement;
    const dialog = dialogRef.current;
    const first = dialog?.querySelector<HTMLElement>(FOCUSABLE);
    (first ?? dialog)?.focus();

    return () => {
      // Restore focus to the trigger, not the top of the page.
      (restoreFocusTo.current as HTMLElement | null)?.focus?.();
    };
  }, [open]);

  function handleKeyDown(event: React.KeyboardEvent<HTMLDivElement>) {
    if (event.key === 'Escape') {
      event.stopPropagation();
      onClose();
      return;
    }

    if (event.key !== 'Tab') {
      return;
    }

    const focusable = Array.from(dialogRef.current?.querySelectorAll<HTMLElement>(FOCUSABLE) ?? []);
    if (focusable.length === 0) {
      return;
    }

    const first = focusable[0];
    const last = focusable[focusable.length - 1];
    const active = document.activeElement;

    if (event.shiftKey && (active === first || active === dialogRef.current)) {
      event.preventDefault();
      last.focus();
    } else if (!event.shiftKey && active === last) {
      event.preventDefault();
      first.focus();
    }
  }

  if (!open) {
    return null;
  }

  return (
    <div className="nhsn-link__modal-overlay" onKeyDown={handleKeyDown}>
      <div
        ref={dialogRef}
        className={`nhsn-link__modal nhsn-link__modal--${size}`}
        role="dialog"
        aria-modal="true"
        aria-labelledby={titleId}
        tabIndex={-1}>
        <h2 className="nhsn-link__modal-title" id={titleId}>
          {title}
        </h2>
        <div className="nhsn-link__modal-body">{children}</div>
        {footer && <div className="nhsn-link__modal-actions">{footer}</div>}
      </div>
    </div>
  );
}
