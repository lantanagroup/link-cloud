import React, {useId} from 'react';
import {NewTabAnnouncement} from '../fields';

export interface InstructionsDownloadProps {
  /** From `api.getJwksInstructionsUrl(vendor)` or `api.getLocationOrgResolutionUrl()`. */
  href: string;
  /** Already translated: the paragraph above the link. */
  description: string;
  /** Already translated: the link text, e.g. "Download PDF Instructions". */
  linkText: string;
  headingId?: string;
}

/** Bordered instructions box + download link, matching `FhirStep.tsx`'s JWKS pattern. Opens in a new tab. */
export function InstructionsDownload({href, description, linkText, headingId}: InstructionsDownloadProps) {
  const descriptionId = useId();
  return (
    <div className="nhsn-link__instructions">
      <p className="nhsn-link__instructions-text" id={descriptionId}>
        {description}
      </p>
      <a
        className="nhsn-link__document-link"
        href={href}
        target="_blank"
        rel="noopener"
        aria-describedby={headingId ? `${headingId} ${descriptionId}` : descriptionId}>
        <svg
          width="16"
          height="16"
          viewBox="0 0 24 24"
          fill="none"
          stroke="currentColor"
          strokeWidth="2"
          strokeLinecap="round"
          strokeLinejoin="round"
          aria-hidden="true">
          <path d="M12 3v12" />
          <path d="M7 10l5 5 5-5" />
          <path d="M5 21h14" />
        </svg>
        {linkText}
        <NewTabAnnouncement />
      </a>
    </div>
  );
}
