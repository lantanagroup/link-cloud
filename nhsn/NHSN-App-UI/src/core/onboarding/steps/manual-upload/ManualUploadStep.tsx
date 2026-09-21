import React, {useMemo, useState} from 'react';
import {useTranslation} from 'react-i18next';
import {useApiClient} from '../../../api/ApiClientContext';
import type {ImportedFields} from '../../../api/contracts';
import {
  acronymTitle,
  AcronymText,
  Button,
  DownloadLinkButton,
  FileUploadField,
  HeadingPause,
  MessageContainer,
  StepActions
} from '../../../fields';
import {isStepId, type StepId} from '../../types';
import type {DraftSections} from '../../reducer';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';
import {useStableCallback, useStepChrome} from '../../StepChrome';

/** Manual Upload Option: download the import sheet, complete it offline, upload it back. */
export function ManualUploadStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const {saving, savingDirection, patch, user, setErrorStepIds} = useOnboarding();
  const api = useApiClient();
  const [uploading, setUploading] = useState(false);
  const [error, setError] = useState<string[]>();
  const [importSummary, setImportSummary] = useState<{fileName: string; imported: number; total: number}>();

  // Patches every draft section the sheet had values for, reflecting what the BFF already saved to
  // the owning services (including SFTP credentials, which it forwards straight to Data
  // Acquisition - see ImportedFields.HasCredentials) - never touches unlockedStepIds, so a step
  // still only unlocks when the user actually clicks Continue through it (gating.ts).
  function applyImportedFields(fields: ImportedFields | undefined) {
    if (!fields) {
      return;
    }
    if (fields.fhir) {
      patch('fhir', fields.fhir);
    }
    if (fields.locationOrg) {
      patch('locationOrg', fields.locationOrg);
    }
    if (fields.hsloc?.mappings) {
      patch('hsloc', {mappings: fields.hsloc.mappings});
    }
    if (fields.encounter?.mappings) {
      // encounter.codeSystems is a separate list driving which "Encounter.type Code System"
      // sections EncounterStep renders (see buildGroups) - it's not derived from mappings there,
      // so patching mappings alone leaves a stale system from a previous session/import rendering
      // as an empty group forever. The sheet is the source of truth for this step on import, so
      // codeSystems is replaced with exactly the systems the sheet named, same as mappings.
      const codeSystems = [...new Set(fields.encounter.mappings.map(mapping => mapping.system))];
      patch('encounter', {codeSystems, mappings: fields.encounter.mappings});
    }
    if (fields.census) {
      const censusPatch: Partial<DraftSections['census']> = fields.census;
      patch('census', censusPatch);
    }
  }

  async function handleSelect(file: File) {
    setUploading(true);
    setError(undefined);
    setImportSummary(undefined);
    // Each attempt reflects only its own outcome - a fixed-and-reuploaded file must clear
    // whatever the previous attempt flagged, not accumulate on top of it.
    setErrorStepIds([]);
    try {
      const result = await api.importDraft(file);
      const isUnreadable =
        !result.accepted &&
        result.cellErrors.length === 1 &&
        result.cellErrors[0].messageKey === 'onboarding:manualUpload.errors.invalidFormat';

      // Flags the affected steps in the nav with a red exclamation mark. Cell errors never carry
      // the offending value (see ManualUploadTemplateService), so this can't leak one.
      const affectedSteps = result.cellErrors
        .map(cellError => cellError.section)
        .filter((section): section is StepId => Boolean(section) && isStepId(section!));
      setErrorStepIds(affectedSteps);

      // `sheet`/`cell`/`label` are literal text pulled straight from the uploaded spreadsheet (the
      // sheet's real tab name, its own cell address, its own column-B field label) - never
      // translated, since they're identifying data about the file, not UI copy. Only the sentence
      // structure around them (`errors.lineFormat`) and the instruction itself (`messageKey`) are
      // localized.
      const errorLines = result.cellErrors.length
        ? result.cellErrors.map(cellError => {
            const location = cellError.label ? `${cellError.cell} (${cellError.label})` : cellError.cell;
            const message = t(cellError.messageKey, {detail: cellError.detail});
            return t('onboarding:manualUpload.errors.lineFormat', {sheet: cellError.sheet, location, message});
          })
        : undefined;

      if (result.accepted) {
        // The file itself is never persisted - only the fact that a facility
        // uploaded one, and when, is recorded on the draft.
        patch('manualUpload', {uploadedFileName: file.name, uploadedOn: new Date().toISOString()});
        applyImportedFields(result.fields);
        setImportSummary({fileName: file.name, imported: result.fieldsImported, total: result.totalFields});
        // A well-formed sheet can still have sections that failed to actually save (a downstream
        // precondition or validator) - accepted stays true so whatever DID save still applies, but
        // the facility still needs to see why some of it came back blank.
        setError(errorLines);
      } else if (isUnreadable) {
        setError([t('onboarding:manualUpload.readError')]);
      } else {
        setError(errorLines ?? [t('onboarding:manualUpload.uploadRejected')]);
      }
    } catch (cause) {
      setError([cause instanceof Error ? cause.message : String(cause)]);
    } finally {
      setUploading(false);
    }
  }

  const stableOnBack = useStableCallback(onBack);
  const stableOnNext = useStableCallback(onNext);

  useStepChrome(
    useMemo(
      () => ({
        title: acronymTitle(<HeadingPause>{t('onboarding:manualUpload.title')}</HeadingPause>),
        footer: (
          <StepActions saving={saving}>
            <Button variant="secondary" onClick={stableOnBack} disabled={saving} loading={savingDirection === 'back'}>
              {t('common:actions.back')}
            </Button>
            <Button onClick={stableOnNext} disabled={saving} loading={savingDirection === 'next'}>
              {t('common:actions.continue')}
            </Button>
          </StepActions>
        )
      }),
      [t, saving, savingDirection, stableOnBack, stableOnNext]
    )
  );

  return (
    <div className="nhsn-link__manual-upload">
      <p className="nhsn-link__subtitle">{t('onboarding:manualUpload.intro')}</p>

      <DownloadLinkButton
        buttonText={t('onboarding:manualUpload.downloadTemplate')}
        hint={t('onboarding:manualUpload.downloadHint')}
        // The BFF names the file too, on Content-Disposition; a programmatic <a download> uses
        // this one, so the two are kept the same shape deliberately.
        fileName={`${user.facilityId ?? 'facility'}_import_sheet.zip`}
        onDownload={() => {
          setError(undefined);
          return api.exportDraft();
        }}
        onError={cause => setError([cause instanceof Error ? cause.message : String(cause)])}
        disabled={uploading}
      />

      <FileUploadField
        id="manual-upload-file-input"
        label={t('onboarding:manualUpload.uploadFile')}
        accept=".xlsx,.xls"
        onSelect={handleSelect}
        disabled={uploading || saving}
      />

      <p className="nhsn-link__status-message" role="status" aria-live="polite">
        {uploading
          ? t('onboarding:manualUpload.reading')
          : importSummary &&
            t('onboarding:manualUpload.imported', {
              imported: importSummary.imported,
              total: importSummary.total,
              fileName: importSummary.fileName
            })}
      </p>
      <p className="nhsn-link__visually-hidden" role="alert">
        {error && <AcronymText>{error.join(' ')}</AcronymText>}
      </p>
      {error && error.length > 0 && (
        <MessageContainer type="error" showIcon>
          <ul className="nhsn-link__error-list">
            {error.map((line, index) => (
              <li key={index}><AcronymText>{line}</AcronymText></li>
            ))}
          </ul>
        </MessageContainer>
      )}
    </div>
  );
}

export default ManualUploadStep;
