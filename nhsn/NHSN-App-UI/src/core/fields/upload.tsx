import React, {useState} from 'react';

export interface DownloadLinkButtonProps {
  buttonText: string;
  fileName: string;
  onDownload: () => Promise<Blob>;
  hint?: string;
  disabled?: boolean;
}

/** The outlined, icon-prefixed "Download ..." action used on the manual-upload page. */
export function DownloadLinkButton({
  buttonText,
  fileName,
  onDownload,
  hint,
  disabled
}: DownloadLinkButtonProps) {
  const [downloading, setDownloading] = useState(false);

  async function handleClick() {
    setDownloading(true);
    try {
      const blob = await onDownload();
      const url = URL.createObjectURL(blob);
      const link = document.createElement('a');
      link.href = url;
      link.download = fileName;
      link.click();
      URL.revokeObjectURL(url);
    } finally {
      setDownloading(false);
    }
  }

  return (
    <div className="nhsn-link__download-field">
      <button
        type="button"
        className="nhsn-link__download-button"
        onClick={handleClick}
        disabled={disabled || downloading}>
        <DownloadIcon />
        {buttonText}
      </button>
      {hint && <p className="nhsn-link__hint-text">{hint}</p>}
    </div>
  );
}

function DownloadIcon() {
  return (
    <svg
      className="nhsn-link__download-icon"
      width="16"
      height="16"
      viewBox="0 0 16 16"
      fill="none"
      aria-hidden="true">
      <path
        d="M8 1v8.5M8 9.5 4.5 6M8 9.5 11.5 6M2 12.5v1a1 1 0 0 0 1 1h10a1 1 0 0 0 1-1v-1"
        stroke="currentColor"
        strokeWidth="1.3"
        strokeLinecap="round"
        strokeLinejoin="round"
      />
    </svg>
  );
}

export interface FileUploadFieldProps {
  id: string;
  label: string;
  accept?: string;
  onSelect: (file: File) => void;
  disabled?: boolean;
}

/** A labeled, native file input — the "Upload Completed Import Sheet" control. */
export function FileUploadField({id, label, accept, onSelect, disabled}: FileUploadFieldProps) {
  function handleChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (file) {
      onSelect(file);
    }
  }

  return (
    <div className="nhsn-link__upload-field">
      <label className="nhsn-link__upload-label" htmlFor={id}>
        {label}
      </label>
      <input id={id} type="file" accept={accept} onChange={handleChange} disabled={disabled} />
    </div>
  );
}
