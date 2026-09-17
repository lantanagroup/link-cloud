import React, {useId} from 'react';

export interface InfoTooltipProps {
  /** Already translated: the "?" trigger's accessible name. */
  label: string;
  /** Already translated: the tooltip body. */
  content: string;
}

/** The POC's "?" info icon. Markup only - `NHSNLink.tsx`'s `useHintTooltips()` owns the interaction. */
export function InfoTooltip({label, content}: InfoTooltipProps) {
  const bubbleId = useId();
  return (
    <button type="button" className="info-icon" aria-label={label} aria-describedby={bubbleId}>
      ?
      <span id={bubbleId} className="tooltip-bubble" role="tooltip">
        {content}
      </span>
    </button>
  );
}
