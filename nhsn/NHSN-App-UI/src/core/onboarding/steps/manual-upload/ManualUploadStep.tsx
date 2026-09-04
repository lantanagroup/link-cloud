import React, {useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import {Button, DownloadLinkButton, FileUploadField, MessageContainer, PageHeader, StepActions} from '../../../fields';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';

/** Manual Upload Option: download the import sheet, complete it offline, upload it back. */
export function ManualUploadStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const {saving, patch} = useOnboarding();
  const api = useApiClient();
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string>();
  const [importSummary, setImportSummary] = useState<{fileName: string; imported: number; total: number}>();

  async function handleSelect(file: File) {
    setUploading(true);
    setError(undefined);
    setImportSummary(undefined);
    try {
      const result = await api.importDraft(file);
      const isUnreadable =
        !result.accepted &&
        result.cellErrors.length === 1 &&
        result.cellErrors[0].messageKey === 'onboarding:manualUpload.errors.invalidFormat';

      if (result.accepted) {
        // The file itself is never persisted - only the fact that a facility
        // uploaded one, and when, is recorded on the draft.
        patch('manualUpload', {uploadedFileName: file.name, uploadedOn: new Date().toISOString()});
        setImportSummary({fileName: file.name, imported: result.fieldsImported, total: result.totalFields});
      } else if (isUnreadable) {
        setError(t('onboarding:manualUpload.readError'));
      } else {
        setError(
          result.cellErrors.length > 0
            ? result.cellErrors
                .map(cellError => `${cellError.sheet} ${cellError.cell}: ${t(cellError.messageKey)}`)
                .join('; ')
            : t('onboarding:manualUpload.uploadRejected')
        );
      }
    } catch (cause) {
      setError(cause instanceof Error ? cause.message : String(cause));
    } finally {
      setUploading(false);
    }
  }

  return (
    <div className="nhsn-link__content nhsn-link__manual-upload">
      <PageHeader title={t('onboarding:manualUpload.title')} />
      <p className="nhsn-link__subtitle">{t('onboarding:manualUpload.intro')}</p>

      <DownloadLinkButton
        buttonText={t('onboarding:manualUpload.downloadTemplate')}
        hint={t('onboarding:manualUpload.downloadHint')}
        fileName="manual-upload-import-sheet.xlsx"
        onDownload={() => api.exportDraft()}
        disabled={uploading}
      />

      <FileUploadField
        id="manual-upload-file-input"
        label={t('onboarding:manualUpload.uploadFile')}
        accept=".xlsx,.xls"
        onSelect={handleSelect}
        disabled={uploading || saving}
      />

      {uploading && <p className="nhsn-link__status-message">{t('onboarding:manualUpload.reading')}</p>}
      {!uploading && importSummary && (
        <p className="nhsn-link__status-message">
          {t('onboarding:manualUpload.imported', {
            imported: importSummary.imported,
            total: importSummary.total,
            fileName: importSummary.fileName
          })}
        </p>
      )}
      {error && (
        <MessageContainer type="error" showIcon>
          <span role="alert">{error}</span>
        </MessageContainer>
      )}

      <StepActions saving={saving}>
        <Button variant="secondary" onClick={onBack} disabled={saving}>
          {t('common:actions.back')}
        </Button>
        <Button onClick={onNext} disabled={saving} loading={saving}>
          {t('common:actions.continue')}
        </Button>
      </StepActions>
    </div>
  );
}

export default ManualUploadStep;
