import React, {useId, useState} from 'react';
import {MessageContainer} from '../fields';

export interface InstructionsDownloadProps {
  /** Fetches the instructions document as a blob, e.g. `() => api.getDocument(documentKey)`. */
  onDownload: () => Promise<Blob>;
  /** Name the downloaded file is saved as. */
  fileName: string;
  /** Already translated: the paragraph above the link. */
  description: string;
  /** Already translated: the link text, e.g. "Download PDF Instructions". */
  linkText: string;
  headingId?: string;
}

/** Bordered instructions box + download button, matching `FhirStep.tsx`'s JWKS pattern. Downloads the file directly, same as the manual-upload import sheet. */
export function InstructionsDownload({onDownload, fileName, description, linkText, headingId}: InstructionsDownloadProps) {
  const descriptionId = useId();
  const [downloading, setDownloading] = useState(false);
  const [error, setError] = useState<string>();

  async function handleClick() {
    if (downloading) {
      return;
    }
    setDownloading(true);
    setError(undefined);
    try {
      const blob = await onDownload();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(url);
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setDownloading(false);
    }
  }

  return (
    <div className="nhsn-link__instructions">
      <p className="nhsn-link__instructions-text" id={descriptionId}>
        {description}
      </p>
      <button
        type="button"
        className="nhsn-link__document-link"
        disabled={downloading}
        onClick={handleClick}
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
      </button>
      {error && (
        <MessageContainer type="error" showIcon>
          <span>{error}</span>
        </MessageContainer>
      )}
    </div>
  );
}
