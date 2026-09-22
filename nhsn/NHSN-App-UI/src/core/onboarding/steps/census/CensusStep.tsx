import React, { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import * as XLSX from "xlsx";
import { useApiClient } from "../../../api/ApiClientContext";
import type {
  CensusListKey,
  CensusListResult,
  ConnectionResult,
  SftpFile,
} from "../../../api/contracts";
import { HttpError, TimeoutError } from "../../../api/http";
import { InstructionsDownload } from "../../../documents";
import {
  acronymTitle,
  AcronymText,
  Button,
  CheckboxField,
  DownloadLinkButton,
  HeadingPause,
  InfoTooltip,
  NumberField,
  RequiredAsterisk,
  SidePanel,
  SidePanelLayout,
  StepActions,
  TableCaption,
  TextField,
} from "../../../fields";
import { useNotifications } from "../../../notifications/NotificationProvider";
import {
  buildHoursMinutesDuration,
  parseHoursMinutesDuration,
} from "../../../shared/duration";
import type { StepProps } from "../../flow";
import { useOnboarding, useStepValidator } from "../../OnboardingProvider";
import { useStableCallback, useStepChrome } from "../../StepChrome";
import { CENSUS_LIST_KEYS, validateCensus, type FieldErrors } from "./validate";
import "./CensusStep.css";

const LIST_LABEL_KEYS: Record<CensusListKey, string> = {
  "admit-lt-24": "onboarding:census.epic.lists.admitLt24",
  "admit-24-to-48": "onboarding:census.epic.lists.admit24to48",
  "admit-gt-48": "onboarding:census.epic.lists.admitGt48",
  "discharge-lt-24": "onboarding:census.epic.lists.dischargeLt24",
  "discharge-24-to-48": "onboarding:census.epic.lists.discharge24to48",
  "discharge-gt-48": "onboarding:census.epic.lists.dischargeGt48",
};

interface ListQueryState {
  querying: boolean;
  result?: CensusListResult;
  queriedAt?: string;
  error?: string;
  /** Read successfully before a later list in the same call failed - no patient data, just known-good. */
  verified?: boolean;
  /** Never attempted because an earlier list in the same call failed first. */
  untested?: boolean;
}

function buildXlsxBlob(headers: string[], rows: string[][]): Blob {
  const worksheet = XLSX.utils.aoa_to_sheet([headers, ...rows]);
  const workbook = XLSX.utils.book_new();
  XLSX.utils.book_append_sheet(workbook, worksheet, "Census Results");
  const content = XLSX.write(workbook, { bookType: "xlsx", type: "array" });
  return new Blob([content], {
    type: "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
  });
}

function ViewResultsIcon() {
  return (
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
      <rect x="3" y="4" width="18" height="16" rx="2" />
      <path d="M3 10h18" />
      <path d="M9 10v10" />
    </svg>
  );
}

/**
 * Step scaffold. The screen's own fields, validation and API calls are LEGLINK story: patients of interest.
 *
 * What is already wired and should not be rebuilt: draft access and patching
 * via useOnboarding(), navigation via onNext/onBack, gating and URL sync via
 * the provider, and every control through core/fields.
 */
export function CensusStep({ onNext, onBack }: StepProps) {
  const { t } = useTranslation(["onboarding", "common"]);
  const api = useApiClient();
  const { notifyError } = useNotifications();
  const { draft, patch, save, saving, savingDirection, user, vendorProfile } = useOnboarding();
  const census = draft.census;
  const acquisition = vendorProfile?.censusAcquisition;

  const [errors, setErrors] = useState<FieldErrors>({});
  const [validationMessage, setValidationMessage] = useState<string | null>(
    null,
  );

  function announceValidationMessage(message: string) {
    setValidationMessage(null);
    window.setTimeout(() => setValidationMessage(message), 0);
  }

  const initialFrequency = parseHoursMinutesDuration(
    census.acquisitionFrequency,
  );
  // A 0 in either part is the same as not having entered it - both are
  // optional and default to 0 on save - so show it blank rather than "0".
  const [frequencyHours, setFrequencyHours] = useState<number | undefined>(
    initialFrequency?.hours || undefined,
  );
  const [frequencyMinutes, setFrequencyMinutes] = useState<number | undefined>(
    initialFrequency?.minutes || undefined,
  );

  const [listState, setListState] = useState<
    Partial<Record<CensusListKey, ListQueryState>>
  >({});
  const [selectedListKey, setSelectedListKey] = useState<CensusListKey | null>(
    null,
  );
  const [validatingLists, setValidatingLists] = useState(false);

  const [sftpUsername, setSftpUsername] = useState("");
  const [sftpPassword, setSftpPassword] = useState("");
  const [testingConnection, setTestingConnection] = useState(false);
  const [connectionResult, setConnectionResult] =
    useState<ConnectionResult | null>(null);
  const [sftpFiles, setSftpFiles] = useState<SftpFile[] | null>(null);
  const [selectedFileName, setSelectedFileName] = useState<string | null>(
    null,
  );

  const sftpTestResultRef = useRef<HTMLParagraphElement | null>(null);
  useEffect(() => {
    if (testingConnection || connectionResult) {
      scrollNearestContainerToBottom(sftpTestResultRef.current);
    }
  }, [testingConnection, connectionResult]);

  const validationMessageRef = useRef<HTMLParagraphElement | null>(null);
  useEffect(() => {
    if (validationMessage) {
      scrollNearestContainerToBottom(validationMessageRef.current);
    }
  }, [validationMessage]);

  const persistedTestedSftpConfig =
    census.sftpConnectionTested && census.sftpHost && census.sftpPort !== undefined
      ? { host: census.sftpHost.trim(), port: census.sftpPort }
      : null;
  const [testedSftpConfig, setTestedSftpConfig] = useState<
    { host: string; port: number } | null
  >(persistedTestedSftpConfig);
  const sftpConnectionVerified =
    testedSftpConfig !== null &&
    testedSftpConfig.host === census.sftpHost?.trim() &&
    testedSftpConfig.port === census.sftpPort;

  const patientListsLive = user.capabilities?.patientListWithNames ?? false;
  const sftpListingLive = user.capabilities?.sftpFileListing ?? false;
  const validationLive =
    acquisition === "PatientList"
      ? patientListsLive
      : acquisition === "Sftp"
        ? sftpListingLive
        : false;

  const allListsQueried =
    acquisition === "PatientList" &&
    CENSUS_LIST_KEYS.every((key) => Boolean(listState[key]?.result));
  const sftpValidated = acquisition === "Sftp" && sftpFiles !== null;
  const resultsReady = allListsQueried || sftpValidated;

  function refreshFieldError(field: string) {
    const nextErrors = validateCensus(draft, acquisition);
    setErrors((prev) => {
      const next = { ...prev };
      if (nextErrors[field]) {
        next[field] = nextErrors[field];
      } else {
        delete next[field];
      }
      return next;
    });
  }

  function updateFrequency(
    hours: number | undefined,
    minutes: number | undefined,
  ) {
    setFrequencyHours(hours);
    setFrequencyMinutes(minutes);
    if (
      frequencyHoursError(hours) ||
      frequencyMinutesError(minutes, minutesFloor(hours))
    ) {
      // Leave the last valid persisted duration alone instead of silently
      // clamping a negative/out-of-range entry down to 0 behind the user's back.
      return;
    }
    patch("census", {
      acquisitionFrequency: buildHoursMinutesDuration(hours ?? 0, minutes ?? 0),
    });
  }

  function revokeAcknowledgement() {
    patch("census", { accuracyAcknowledged: false });
  }

  function updateListId(key: CensusListKey, value: string) {
    patch("census", {
      patientListIds: { ...census.patientListIds, [key]: value },
    });
    if (validationLive && census.accuracyAcknowledged) {
      revokeAcknowledgement();
      announceValidationMessage(t("onboarding:census.messages.validateBeforeAck"));
    }
    setListState((prev) => {
      if (!prev[key]) {
        return prev;
      }
      const next = { ...prev };
      delete next[key];
      return next;
    });
    setSelectedListKey((prev) => (prev === key ? null : prev));
  }

  function updateSftpField(fields: Partial<typeof census>) {
    const retested = "sftpHost" in fields || "sftpPort" in fields;
    patch("census", retested ? { ...fields, sftpConnectionTested: false } : fields);
    if (validationLive && census.accuracyAcknowledged) {
      revokeAcknowledgement();
      announceValidationMessage(
        t("onboarding:census.messages.testConnectionBeforeAck"),
      );
    }
  }

  async function handleValidateEpicLists() {
    const fieldErrors = validateCensus(draft, "PatientList");
    if (Object.keys(fieldErrors).length > 0) {
      setErrors(fieldErrors);
      announceValidationMessage(t("onboarding:census.messages.incomplete"));
      return;
    }

    setValidationMessage(null);
    setSelectedListKey(null);
    setValidatingLists(true);

    // The list-query endpoints read the facility's already-saved list ids from Data Acquisition,
    // not the draft, so the ids on screen must be persisted before querying against them.
    const saved = await save();
    if (!saved) {
      setValidatingLists(false);
      return;
    }

    // save() above just cleared the dirty flag; mark it dirty again so leaving still prompts.
    patch("census", {});

    if (census.accuracyAcknowledged) {
      revokeAcknowledgement();
    }
    setListState(
      Object.fromEntries(
        CENSUS_LIST_KEYS.map((key) => [key, { querying: true }]),
      ),
    );

    // One call for all six lists - per-key calls would each surface the same shared-fetch error.
    try {
      const results = await api.queryPatientLists();
      const queriedAt = new Date().toISOString();
      setListState(
        Object.fromEntries(
          results.map((result) => [
            result.listKey,
            { querying: false, result, queriedAt },
          ]),
        ),
      );
    } catch (cause) {
      const message =
        cause instanceof Error ? cause.message : t("onboarding:census.epic.queryError");
      const failedKey =
        cause instanceof HttpError ? asCensusListKey(cause.listKey) : undefined;

      if (failedKey) {
        // Data Acquisition reads the six lists in this same order and stops at the first
        // failure, so everything before it already came back clean and everything after it
        // was never attempted.
        const failedIndex = CENSUS_LIST_KEYS.indexOf(failedKey);
        setListState(
          Object.fromEntries(
            CENSUS_LIST_KEYS.map((key, index) => [
              key,
              index < failedIndex
                ? { querying: false, verified: true }
                : key === failedKey
                  ? { querying: false, error: message }
                  : { querying: false, untested: true },
            ]),
          ),
        );
      } else {
        setListState({});
        announceValidationMessage(message);
      }
    }
    setValidatingLists(false);
  }

  async function handleTestConnection() {
    const fieldErrors = validateCensus(draft, "Sftp");
    setErrors(fieldErrors);
    if (fieldErrors.sftpHost || fieldErrors.sftpPort) {
announceValidationMessage(t("onboarding:census.messages.incomplete"));
      return;
    }

    setValidationMessage(null);
    setTestingConnection(true);
    setConnectionResult(null);
    setTestedSftpConfig(null);
    setSftpFiles(null);
    setSelectedFileName(null);
    if (census.accuracyAcknowledged) {
      revokeAcknowledgement();
    }

    // Credentials go in the same call as the rest of the configuration, not a separate one before
    // it: the backend must create the sFTP configuration before it can attach credentials to it,
    // and saving them first would 404 against a configuration that doesn't exist yet.
    const enteringCredentials = Boolean(
      sftpUsername.trim() && sftpPassword.trim(),
    );
    const trimmedHost = census.sftpHost!.trim();
    const port = census.sftpPort!;

    try {
      const result = await api.testSftpConnection({
        host: trimmedHost,
        port,
        remoteDirectory: census.sftpRemoteDirectory?.trim() || "/",
        removeAfterProcessing: Boolean(census.sftpRemoveAfterProcessing),
        ...(enteringCredentials
          ? { username: sftpUsername.trim(), password: sftpPassword.trim() }
          : {}),
      });
      setConnectionResult(result);
      setTestedSftpConfig(result.success ? { host: trimmedHost, port } : null);
      patch("census", { sftpConnectionTested: result.success });

      if (enteringCredentials) {
        patch("census", { hasCredentials: true });
        setSftpUsername("");
        setSftpPassword("");
      }

      if (result.success && sftpListingLive) {
        setSftpFiles(await api.listSftpFiles());
      }
    } catch (cause) {
      patch("census", { sftpConnectionTested: false });
      if (cause instanceof TimeoutError) {
        setConnectionResult({
          success: false,
          messageKey: "onboarding:census.cerner.testFailure",
          detail: t("onboarding:census.cerner.testTimeout"),
        });
      } else {
        notifyError(
          cause instanceof Error
            ? cause.message
            : t("onboarding:census.cerner.testError"),
        );
      }
    } finally {
      setTestingConnection(false);
    }
  }

  function handleAckChange(checked: boolean) {
    if (checked && !resultsReady && !census.accuracyAcknowledged) {
      announceValidationMessage(
        t(
          acquisition === "Sftp"
            ? "onboarding:census.messages.testConnectionBeforeAck"
            : "onboarding:census.messages.validateBeforeAck",
        ),
      );
      return;
    }

    if (!checked) {
      revokeAcknowledgement();
      return;
    }

    patch("census", { accuracyAcknowledged: true });
  }

  function validateStep(): boolean {
    const nextErrors = validateCensus(draft, acquisition);
    const hoursError = frequencyHoursError(frequencyHours);
    const minutesError = frequencyMinutesError(
      frequencyMinutes,
      minutesFloor(frequencyHours),
    );
    if (hoursError) {
      nextErrors.frequencyHours = hoursError;
    }
    if (minutesError) {
      nextErrors.frequencyMinutes = minutesError;
    }
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      announceValidationMessage(t("onboarding:census.messages.incomplete"));
      return false;
    }
    if (acquisition === "Sftp" && !sftpConnectionVerified) {
      announceValidationMessage(t("onboarding:census.messages.connectionNotTested"));
      return false;
    }
    if (validationLive && !census.accuracyAcknowledged) {
      announceValidationMessage(t("onboarding:census.messages.notAcknowledged"));
      return false;
    }
    setValidationMessage(null);
    return true;
  }

  async function handleNext() {
    if (!validateStep()) {
      return;
    }

    if (validationLive) {
      try {
        await api.acknowledgeCensus({
          kind: "CensusAccuracy",
          accepted: true,
          statementKey: "census-accuracy",
        });
      } catch (cause) {
        notifyError(
          cause instanceof Error
            ? cause.message
            : t("onboarding:census.messages.ackError"),
        );
        return;
      }
    }

    onNext();
  }

  useStepValidator(validateStep);

  const stableOnBack = useStableCallback(onBack);
  const stableHandleNext = useStableCallback(handleNext);

  useStepChrome(
    useMemo(
      () => ({
        title: acronymTitle(<HeadingPause>{t("onboarding:census.title")}</HeadingPause>),
        footer: !vendorProfile ? (
          <StepActions>
            <Button variant="secondary" onClick={stableOnBack}>
              {t("common:actions.back")}
            </Button>
          </StepActions>
        ) : (
          <StepActions saving={saving}>
            <Button variant="secondary" onClick={stableOnBack} disabled={saving} loading={savingDirection === "back"}>
              {t("common:actions.back")}
            </Button>
            <Button
              onClick={stableHandleNext}
              disabled={saving}
              loading={savingDirection === "next"}>
              {t("common:actions.continue")}
            </Button>
          </StepActions>
        )
      }),
      [t, vendorProfile, stableOnBack, saving, savingDirection, stableHandleNext]
    )
  );

  if (!vendorProfile) {
    return (
      <div className="census-poi">
        <p className="subtitle">
          {t("onboarding:census.messages.vendorRequired")}
        </p>
      </div>
    );
  }

  const selectedListState = selectedListKey
    ? listState[selectedListKey]
    : undefined;

  const selectedFile = selectedFileName
    ? (sftpFiles ?? []).find((file) => file.fileName === selectedFileName)
    : undefined;

  async function handleExportEpicResults(): Promise<Blob> {
    const rows = CENSUS_LIST_KEYS.filter((key) => listState[key]?.result).flatMap(
      (key) => {
        const state = listState[key]!;
        return state.result!.patients.map((patient) => [
          t(LIST_LABEL_KEYS[key]),
          census.patientListIds?.[key] ?? "",
          formatDateTime(state.queriedAt),
          patient.id,
          patient.name ?? "",
        ]);
      },
    );
    return buildXlsxBlob(
      [
        t("onboarding:census.epic.summary.listName"),
        t("onboarding:census.epic.summary.listId"),
        t("onboarding:census.epic.summary.queriedAt"),
        t("onboarding:census.epic.columns.patientId"),
        t("onboarding:census.epic.columns.patientName"),
      ],
      rows,
    );
  }

  async function handleExportSftpResults(): Promise<Blob> {
    const rows = (sftpFiles ?? []).flatMap((file) =>
      file.patientIds.map((patientId) => [
        file.fileName,
        patientId,
        formatDateTime(file.queriedAt),
      ]),
    );
    return buildXlsxBlob(
      [
        t("onboarding:census.cerner.columns.fileName"),
        t("onboarding:census.cerner.columns.patientId"),
        t("onboarding:census.cerner.columns.queriedAt"),
      ],
      rows,
    );
  }

  const exportFileName = `${user.facilityId ?? "facility"}_Census_Results.xlsx`;

  const epicResultsPanel = selectedListKey && selectedListState?.result && (
    <SidePanel>
      <div className="section-title">
        {t("onboarding:census.epic.resultsTitle")}
      </div>
      <ul className="nhsn-link__summary-list">
        <li>
          <span>{t("onboarding:census.epic.summary.listName")}</span>
          <span>{t(LIST_LABEL_KEYS[selectedListKey])}</span>
        </li>
        <li>
          <span>{t("onboarding:census.epic.summary.listId")}</span>
          <span>{census.patientListIds?.[selectedListKey]}</span>
        </li>
        <li>
          <span>{t("onboarding:census.epic.summary.queriedAt")}</span>
          <span>{formatDateTime(selectedListState.queriedAt)}</span>
        </li>
        <li>
          <span>{t("onboarding:census.epic.summary.patientCount")}</span>
          <span>{selectedListState.result.patientCount}</span>
        </li>
      </ul>
      <div className="census-table-scroll" tabIndex={-1}>
        <table>
          <TableCaption>{t("onboarding:census.epic.resultsTitle")}</TableCaption>
          <thead>
            <tr>
              <th scope="col">{t("onboarding:census.epic.columns.patientId")}</th>
              <th scope="col">{t("onboarding:census.epic.columns.patientName")}</th>
            </tr>
          </thead>
          <tbody>
            {selectedListState.result.patients.map((patient) => (
              <tr key={patient.id}>
                <td>{patient.id}</td>
                <td>{patient.name ?? ""}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SidePanel>
  );

  const sftpResultsPanel = selectedFile && (
    <SidePanel>
      <div className="section-title">
        {t("onboarding:census.cerner.resultsTitle")}
      </div>
      <ul className="nhsn-link__summary-list">
        <li>
          <span>{t("onboarding:census.cerner.columns.fileName")}</span>
          <span>{selectedFile.fileName}</span>
        </li>
        <li>
          <span>{t("onboarding:census.cerner.columns.queriedAt")}</span>
          <span>{formatDateTime(selectedFile.queriedAt)}</span>
        </li>
        <li>
          <span>{t("onboarding:census.cerner.columns.patientCount")}</span>
          <span>{selectedFile.patientIds.length}</span>
        </li>
      </ul>
      <div className="census-table-scroll" tabIndex={-1}>
        <table>
          <TableCaption>{t("onboarding:census.cerner.resultsTitle")}</TableCaption>
          <thead>
            <tr>
              <th scope="col">{t("onboarding:census.cerner.columns.patientId")}</th>
            </tr>
          </thead>
          <tbody>
            {selectedFile.patientIds.map((patientId) => (
              <tr key={patientId}>
                <td>{patientId}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SidePanel>
  );

  const frequencySection = (
    <div
      className="form-group"
      role="group"
      aria-labelledby="census-frequency-label"
      aria-describedby="census-frequency-hint">
      <label className="census-field-label" id="census-frequency-label" htmlFor="census-frequency-hours">
        {t("onboarding:census.fields.frequencyLabel")}
        <RequiredAsterisk />
      </label>
      <p className="form-hint" id="census-frequency-hint">
        {t("onboarding:census.fields.frequencyTooltip")}
      </p>
      <div className="census-triplet">
        <NumberField
          id="census-frequency-hours"
          label={t("onboarding:census.fields.hoursLabel")}
          min={0}
          step={1}
          value={frequencyHours}
          error={
            frequencyHoursError(frequencyHours)
              ? t(frequencyHoursError(frequencyHours)!)
              : undefined
          }
          onChange={(value) => updateFrequency(value, frequencyMinutes)}
          onBlur={() => refreshFieldError("acquisitionFrequency")}
        />
        <NumberField
          id="census-frequency-minutes"
          label={t("onboarding:census.fields.minutesLabel")}
          min={0}
          step={1}
          value={frequencyMinutes}
          error={
            frequencyMinutesError(frequencyMinutes, minutesFloor(frequencyHours))
              ? t(frequencyMinutesError(frequencyMinutes, minutesFloor(frequencyHours))!, {
                  min: minutesFloor(frequencyHours),
                })
              : undefined
          }
          onChange={(value) => updateFrequency(frequencyHours, value)}
          onBlur={() => refreshFieldError("acquisitionFrequency")}
        />
      </div>
      <p className="nhsn-link__form-error" role="alert">
        {errors.acquisitionFrequency ? t(errors.acquisitionFrequency) : null}
      </p>
    </div>
  );

  return (
    <div className="census-poi">
      <p className="subtitle"><AcronymText>{t("onboarding:census.intro1")}</AcronymText></p>
      <p className="subtitle"><AcronymText>{t("onboarding:census.intro2")}</AcronymText></p>

      {acquisition === "PatientList" && (
        <>
          <h2 className="census-section-heading" id="census-epic-section-title">
            {t("onboarding:census.epic.sectionTitle")}
          </h2>
          <p className="subtitle" id="census-epic-subtitle">
            <AcronymText>{t("onboarding:census.epic.subtitle")}</AcronymText>
          </p>

          {vendorProfile.documentKeys.censusInstructions && (
            <InstructionsDownload
              onDownload={() => api.getCensusInstructionsPdf(vendorProfile.vendor)}
              fileName={`${vendorProfile.displayName}_Census_Instructions.pdf`}
              description={t("onboarding:census.epic.instructionsHint")}
              linkText={t("onboarding:census.epic.downloadInstructions")}
              headingId="census-epic-section-title"
            />
          )}
        </>
      )}

      {acquisition === "Sftp" && (
        <>
          <div className="section-title" id="census-cerner-section-title">
            {t("onboarding:census.cerner.sectionTitle")}
          </div>

          {vendorProfile.documentKeys.censusInstructions && (
            <InstructionsDownload
              onDownload={() => api.getCensusInstructionsPdf(vendorProfile.vendor)}
              fileName={`${vendorProfile.displayName}_Census_Instructions.pdf`}
              description={t("onboarding:census.cerner.instructionsHint")}
              linkText={t("onboarding:census.cerner.downloadInstructions")}
              headingId="census-cerner-section-title"
            />
          )}
        </>
      )}

      <SidePanelLayout>
        <div>
            {acquisition === "PatientList" && (
              <>
                {CENSUS_LIST_KEYS.map((key) => {
                  const state = listState[key];
                  const fieldError = errors[`listId.${key}`]
                    ? t(errors[`listId.${key}`])
                    : state?.error;
                  return (
                    <div
                      className={`form-group census-list-field${state?.querying ? " is-querying" : ""}`}
                      key={key}>
                      <div className="census-list-input-row">
                        <TextField
                          id={`census-list-${key}`}
                          label={t(LIST_LABEL_KEYS[key])}
                          required
                          value={census.patientListIds?.[key] ?? ""}
                          error={
                            errors[`listId.${key}`]
                              ? t(errors[`listId.${key}`], {
                                  list: t(LIST_LABEL_KEYS[key]),
                                })
                              : undefined
                          }
                          onChange={(value) => updateListId(key, value)}
                          onBlur={() => refreshFieldError(`listId.${key}`)}
                        />
                        {(state?.result || state?.verified) && (
                          <InfoTooltip
                            icon="✓"
                            variant="success"
                            label={t("onboarding:census.epic.listValidatedAria", {
                              list: t(LIST_LABEL_KEYS[key]),
                            })}
                            content={t("onboarding:census.epic.listValidatedTooltip")}
                          />
                        )}
                        {state?.untested && (
                          <InfoTooltip
                            icon="!"
                            variant="warning"
                            label={t("onboarding:census.epic.listUntestedAria", {
                              list: t(LIST_LABEL_KEYS[key]),
                            })}
                            content={t("onboarding:census.epic.listUntestedTooltip")}
                          />
                        )}
                        {patientListsLive && (
                          <button
                            type="button"
                            className={`census-view-btn${selectedListKey === key ? " active" : ""}`}
                            aria-label={t(
                              "onboarding:census.epic.viewResultsAria",
                              { list: t(LIST_LABEL_KEYS[key]) },
                            )}
                            title={t(
                              "onboarding:census.epic.viewResultsAria",
                              { list: t(LIST_LABEL_KEYS[key]) },
                            )}
                            disabled={!state?.result}
                            onClick={() =>
                              setSelectedListKey((prev) =>
                                prev === key ? null : key,
                              )
                            }>
                            <ViewResultsIcon />
                          </button>
                        )}
                      </div>
                    </div>
                  );
                })}

                {frequencySection}

                {patientListsLive && (
                  <div className="census-inline-actions">
                    <Button
                      onClick={handleValidateEpicLists}
                      disabled={validatingLists}>
                      {validatingLists
                        ? t("onboarding:census.epic.validating")
                        : t("onboarding:census.epic.validateButton")}
                    </Button>
                    {allListsQueried && (
                      <DownloadLinkButton
                        buttonText={t("onboarding:census.fields.exportResults")}
                        fileName={exportFileName}
                        onDownload={handleExportEpicResults}
                      />
                    )}
                  </div>
                )}
              </>
            )}

            {acquisition === "Sftp" && (
              <>
                <TextField
                  id="census-sftp-host"
                  label={t("onboarding:census.cerner.fields.hostLabel")}
                  required
                  maxLength={128}
                  placeholder={t("onboarding:census.cerner.fields.hostPlaceholder")}
                  value={census.sftpHost ?? ""}
                  error={errors.sftpHost ? t(errors.sftpHost) : undefined}
                  onChange={(value) => updateSftpField({ sftpHost: value })}
                  onBlur={() => refreshFieldError("sftpHost")}
                />

                <NumberField
                  id="census-sftp-port"
                  label={t("onboarding:census.cerner.fields.portLabel")}
                  required
                  min={1}
                  max={65535}
                  step={1}
                  value={census.sftpPort}
                  error={errors.sftpPort ? t(errors.sftpPort) : undefined}
                  onChange={(value) => updateSftpField({ sftpPort: value })}
                  onBlur={() => refreshFieldError("sftpPort")}
                />

                <div className="census-triplet">
                  <TextField
                    id="census-sftp-username"
                    label={t("onboarding:census.cerner.fields.usernameLabel")}
                    value={sftpUsername}
                    onChange={setSftpUsername}
                  />
                  <TextField
                    id="census-sftp-password"
                    type="password"
                    label={t("onboarding:census.cerner.fields.passwordLabel")}
                    value={sftpPassword}
                    onChange={setSftpPassword}
                  />
                </div>
                <p className="form-hint">
                  {census.hasCredentials
                    ? t("onboarding:census.cerner.fields.credentialsOnFile")
                    : t("onboarding:census.cerner.fields.credentialsHint")}
                </p>

                <TextField
                  id="census-sftp-remote-dir"
                  label={t(
                    "onboarding:census.cerner.fields.remoteDirectoryLabel",
                  )}
                  hint={t(
                    "onboarding:census.cerner.fields.remoteDirectoryHint",
                  )}
                  placeholder="/"
                  value={census.sftpRemoteDirectory ?? ""}
                  onChange={(value) =>
                    updateSftpField({ sftpRemoteDirectory: value })
                  }
                />

                <CheckboxField
                  id="census-sftp-remove"
                  label={t(
                    "onboarding:census.cerner.fields.removeAfterProcessingLabel",
                  )}
                  value={Boolean(census.sftpRemoveAfterProcessing)}
                  onChange={(value) =>
                    updateSftpField({ sftpRemoveAfterProcessing: value })
                  }
                />

                {frequencySection}

                <div className="census-inline-actions">
                  <Button
                    onClick={handleTestConnection}
                    disabled={testingConnection}>
                    {testingConnection
                      ? t("onboarding:census.cerner.testing")
                      : t("onboarding:census.cerner.testConnection")}
                  </Button>
                  {sftpValidated && (
                    <DownloadLinkButton
                      buttonText={t("onboarding:census.fields.exportResults")}
                      fileName={exportFileName}
                      onDownload={handleExportSftpResults}
                    />
                  )}
                </div>

                <p
                  ref={sftpTestResultRef}
                  role="status"
                  aria-live="polite"
                  className={`census-connection-status${
                    connectionResult
                      ? connectionResult.success
                        ? " census-test-success"
                        : " nhsn-link__form-error"
                      : ""
                  }`}>
                  {!testingConnection && connectionResult
                    ? `${
                        connectionResult.success
                          ? t("onboarding:census.cerner.testSuccess")
                          : t("onboarding:census.cerner.testFailure")
                      }${connectionResult.detail ? ` ${connectionResult.detail}` : ""}`
                    : null}
                </p>

                {sftpListingLive && sftpFiles !== null && (
                  <div className="census-results">
                    <div className="section-title">
                      {t("onboarding:census.cerner.filesTitle")}
                    </div>
                    {(sftpFiles ?? []).length === 0 ? (
                      <p className="subtitle">
                        {t("onboarding:census.cerner.noFiles")}
                      </p>
                    ) : (
                      (sftpFiles ?? []).map((file) => (
                        <div className="census-file-row" key={file.fileName}>
                          <span>{file.fileName}</span>
                          <button
                            type="button"
                            className={`census-view-btn${selectedFileName === file.fileName ? " active" : ""}`}
                            aria-label={t(
                              "onboarding:census.cerner.viewResultsAria",
                              { file: file.fileName },
                            )}
                            title={t(
                              "onboarding:census.cerner.viewResultsAria",
                              { file: file.fileName },
                            )}
                            onClick={() =>
                              setSelectedFileName((prev) =>
                                prev === file.fileName ? null : file.fileName,
                              )
                            }>
                            <ViewResultsIcon />
                          </button>
                        </div>
                      ))
                    )}
                  </div>
                )}
              </>
            )}

            {validationLive && (
              <CheckboxField
                id="census-accuracy-ack"
                label={t("onboarding:census.fields.accuracyAck")}
                value={Boolean(census.accuracyAcknowledged)}
                onChange={handleAckChange}
              />
            )}

            <div aria-live="off">
              <p ref={validationMessageRef} className="nhsn-link__form-error" role="alert">
                {validationMessage}
              </p>
            </div>
        </div>
        {acquisition === "PatientList" && epicResultsPanel}
        {acquisition === "Sftp" && sftpResultsPanel}
      </SidePanelLayout>
    </div>
  );
}

export default CensusStep;

function scrollNearestContainerToBottom(element: HTMLElement | null): void {
  let node = element?.parentElement ?? null;
  while (node) {
    const { overflowY } = window.getComputedStyle(node);
    if (overflowY === "auto" || overflowY === "scroll") {
      node.scrollTo({ top: node.scrollHeight, behavior: "smooth" });
      return;
    }
    node = node.parentElement;
  }
}

function frequencyHoursError(value: number | undefined): string | undefined {

  if (value === undefined || value === null) {
    return undefined;
  }
  return Number.isInteger(value) && value >= 0 && value <= 23
    ? undefined
    : "onboarding:census.errors.frequencyHoursInvalid";
}

/**
 * Minutes alone must clear the overall 5-minute floor since hours defaults to
 * 0 when left blank; once hours has a value, that floor is already met, so
 * minutes reverts to its plain 0-59 range.
 */
function minutesFloor(hours: number | undefined): number {
  return hours === undefined || hours === null ? 5 : 0;
}

function frequencyMinutesError(
  value: number | undefined,
  floor: number,
): string | undefined {
  if (value === undefined || value === null) {
    return undefined;
  }
  return Number.isInteger(value) && value >= floor && value <= 59
    ? undefined
    : "onboarding:census.errors.frequencyMinutesInvalid";
}

function asCensusListKey(value: string | undefined): CensusListKey | undefined {
  return value && (CENSUS_LIST_KEYS as readonly string[]).includes(value)
    ? (value as CensusListKey)
    : undefined;
}

function formatDateTime(iso?: string): string {
  if (!iso) {
    return "";
  }
  const parsed = new Date(iso);
  return Number.isNaN(parsed.getTime()) ? "" : parsed.toLocaleString();
}
