import React, {useRef, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {MessageContainer} from './layout';

export interface DownloadLinkButtonProps {
  buttonText: string;
  fileName: string;
  onDownload: () => Promise<Blob>;
  /** Called instead of rejecting, so a failed download reports itself on the page. */
  onError?: (cause: unknown) => void;
  hint?: string;
  disabled?: boolean;
}

/** The outlined, icon-prefixed "Download ..." action used on the manual-upload page. */
export function DownloadLinkButton({
  buttonText,
  fileName,
  onDownload,
  onError,
  hint,
  disabled
}: DownloadLinkButtonProps) {
 // const {t} = useTranslation('onboarding');
  const [downloading, setDownloading] = useState(false);
  const [error, setError] = useState<string>();
  const isBlocked = Boolean(disabled || downloading);

  async function handleClick() {
    if (isBlocked) {
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
      if (!onError) {
        throw cause;
      }
      onError(cause);
    } finally {
      setDownloading(false);
    }
  }

  return (
    <div className="nhsn-link__download-field">
      <button
        type="button"
        className={`nhsn-link__download-button${isBlocked ? ' nhsn-link__download-button--busy' : ''}`}
        onClick={handleClick}>
        <DownloadIcon />
        {buttonText}
      </button>
      {hint && <p className="nhsn-link__hint-text">{hint}</p>}
      <p className="nhsn-link__visually-hidden" role="alert">
        {error}
      </p>
      {error && (
        <MessageContainer type="error" showIcon>
          <span>{error}</span>
        </MessageContainer>
      )}
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

/**
 * A labeled file upload control — the "Upload Completed Import Sheet" control.
 *
 * A native `<input type="file">` is one clickable widget end to end: there is
 * no way, via CSS alone, to make only its button portion open the file dialog
 * and leave the "No file chosen" text inert. So the native input here is
 * visually hidden and triggered from a real button instead; the filename is
 * rendered as plain text that can't trigger anything.
 */
export function FileUploadField({id, label, accept, onSelect, disabled}: FileUploadFieldProps) {
  const {t} = useTranslation('common');
  const inputRef = useRef<HTMLInputElement>(null);
  const chooseButtonRef = useRef<HTMLButtonElement>(null);
  const [fileName, setFileName] = useState<string | null>(null);

  function handleChange(event: React.ChangeEvent<HTMLInputElement>) {
    const file = event.target.files?.[0];
    if (file) {
      setFileName(file.name);
      onSelect(file);
    }
  }

  function handleChooseClick() {
    if (disabled) {
      return;
    }
    inputRef.current?.click();
  }

  return (
    <div className="nhsn-link__upload-field">
      <label className="nhsn-link__upload-label" htmlFor={id}>
        {label}
      </label>
      <div className="nhsn-link__upload-control">
        <button
          ref={chooseButtonRef}
          type="button"
          className={`nhsn-link__upload-choose-button${disabled ? ' nhsn-link__upload-choose-button--busy' : ''}`}
          onClick={handleChooseClick}>
          {t('common:actions.chooseFile')}
        </button>
        <span className="nhsn-link__upload-filename">{fileName ?? t('common:actions.noFileChosen')}</span>
        <input
          ref={inputRef}
          id={id}
          className="nhsn-link__upload-native-input"
          type="file"
          accept={accept}
          tabIndex={-1}
          onFocus={() => chooseButtonRef.current?.focus()}
          onChange={handleChange}
          disabled={disabled}
        />
      </div>
    </div>
  );
}
