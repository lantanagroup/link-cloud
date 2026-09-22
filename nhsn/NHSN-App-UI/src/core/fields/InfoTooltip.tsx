import React, {useId} from 'react';

export interface InfoTooltipProps {
  /** Already translated: the trigger's accessible name. */
  label: string;
  /** Already translated: the tooltip body. */
  content: string;
  /** The trigger's visible glyph. Defaults to "?". */
  icon?: React.ReactNode;
  /** Recolors the trigger for a status other than a plain info hint. Defaults to 'info'. */
  variant?: 'info' | 'success' | 'warning';
}

/** The POC's "?" info icon. Markup only - `NHSNLink.tsx`'s `useHintTooltips()` owns the interaction. */
export function InfoTooltip({label, content, icon = '?', variant = 'info'}: InfoTooltipProps) {
  const bubbleId = useId();
  return (
    <button
      type="button"
      className={`info-icon${variant !== 'info' ? ` info-icon--${variant}` : ''}`}
      aria-label={label}
      aria-describedby={bubbleId}>
      {icon}
      <span id={bubbleId} className="tooltip-bubble" role="tooltip" aria-hidden="true">
        {content}
      </span>
    </button>
  );
}
