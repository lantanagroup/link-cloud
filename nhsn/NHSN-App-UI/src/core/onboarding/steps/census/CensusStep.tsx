import React, { useState } from "react";
import { useTranslation } from "react-i18next";
import { useApiClient } from "../../../api/ApiClientContext";
import type {
  CensusListKey,
  CensusListResult,
  ConnectionResult,
  SftpFile,
} from "../../../api/contracts";
import {
  Button,
  CheckboxField,
  DownloadLinkButton,
  MessageContainer,
  NumberField,
  PageHeader,
  SidePanel,
  SidePanelLayout,
  StepActions,
  TextField,
} from "../../../fields";
import { useNotifications } from "../../../notifications/NotificationProvider";
import {
  buildHoursMinutesDuration,
  parseHoursMinutesDuration,
} from "../../../shared/duration";
import type { StepProps } from "../../flow";
import { useOnboarding } from "../../OnboardingProvider";
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
}

function summarize(items: string[], limit = 8): string {
  if (items.length <= limit) {
    return items.join(", ");
  }
  return `${items.slice(0, limit).join(", ")}, +${items.length - limit}`;
}

function csvCell(value: string): string {
  return /[",\r\n]/.test(value) ? `"${value.replace(/"/g, '""')}"` : value;
}

function toCsv(rows: string[][]): string {
  return rows.map((row) => row.map(csvCell).join(",")).join("\r\n");
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
  const { draft, patch, saving, user, vendorProfile } = useOnboarding();
  const census = draft.census;
  const acquisition = vendorProfile?.censusAcquisition;

  const [errors, setErrors] = useState<FieldErrors>({});
  const [validationMessage, setValidationMessage] = useState<string | null>(
    null,
  );

  const initialFrequency = parseHoursMinutesDuration(
    census.acquisitionFrequency,
  );
  const [frequencyHours, setFrequencyHours] = useState<number | undefined>(
    initialFrequency?.hours,
  );
  const [frequencyMinutes, setFrequencyMinutes] = useState<number | undefined>(
    initialFrequency?.minutes,
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

  const patientListsLive = user.capabilities?.patientListWithNames ?? false;
  const sftpListingLive = user.capabilities?.sftpFileListing ?? false;

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
    patch("census", {
      acquisitionFrequency: buildHoursMinutesDuration(hours ?? 0, minutes ?? 0),
    });
  }

  function updateListId(key: CensusListKey, value: string) {
    patch("census", {
      patientListIds: { ...census.patientListIds, [key]: value },
    });
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

  async function handleValidateEpicLists() {
    const fieldErrors = validateCensus(draft, "PatientList");
    if (Object.keys(fieldErrors).length > 0) {
      setErrors(fieldErrors);
      setValidationMessage(t("onboarding:census.messages.incomplete"));
      return;
    }

    setValidationMessage(null);
    setSelectedListKey(null);
    setValidatingLists(true);
    setListState(
      Object.fromEntries(
        CENSUS_LIST_KEYS.map((key) => [key, { querying: true }]),
      ),
    );

    await Promise.all(
      CENSUS_LIST_KEYS.map(async (key) => {
        try {
          const result = await api.queryPatientList(key);
          setListState((prev) => ({
            ...prev,
            [key]: {
              querying: false,
              result,
              queriedAt: new Date().toISOString(),
            },
          }));
        } catch (cause) {
          setListState((prev) => ({
            ...prev,
            [key]: {
              querying: false,
              error:
                cause instanceof Error
                  ? cause.message
                  : t("onboarding:census.epic.queryError"),
            },
          }));
        }
      }),
    );
    setValidatingLists(false);
  }

  async function handleTestConnection() {
    const fieldErrors = validateCensus(draft, "Sftp");
    setErrors(fieldErrors);
    if (fieldErrors.sftpHost || fieldErrors.sftpPort) {
      return;
    }

    setTestingConnection(true);
    setConnectionResult(null);
    setSftpFiles(null);

    try {
      if (sftpUsername.trim() && sftpPassword.trim()) {
        await api.saveSftpCredentials({
          username: sftpUsername.trim(),
          password: sftpPassword.trim(),
        });
        patch("census", { hasCredentials: true });
        setSftpUsername("");
        setSftpPassword("");
      }

      const result = await api.testSftpConnection({
        host: census.sftpHost!.trim(),
        port: census.sftpPort!,
        remoteDirectory: census.sftpRemoteDirectory?.trim() || "/",
        removeAfterProcessing: Boolean(census.sftpRemoveAfterProcessing),
      });
      setConnectionResult(result);

      if (result.success) {
        setSftpFiles(await api.listSftpFiles());
      }
    } catch (cause) {
      notifyError(
        cause instanceof Error
          ? cause.message
          : t("onboarding:census.cerner.testError"),
      );
    } finally {
      setTestingConnection(false);
    }
  }

  function handleAckChange(checked: boolean) {
    patch("census", { accuracyAcknowledged: checked });
    if (checked) {
      api
        .acknowledgeCensus({
          kind: "CensusAccuracy",
          accepted: true,
          statementKey: "census-accuracy",
        })
        .catch((cause) => {
          notifyError(
            cause instanceof Error
              ? cause.message
              : t("onboarding:census.messages.ackError"),
          );
          patch("census", { accuracyAcknowledged: false });
        });
    }
  }

  function handleNext() {
    const nextErrors = validateCensus(draft, acquisition);
    setErrors(nextErrors);
    if (Object.keys(nextErrors).length > 0) {
      setValidationMessage(t("onboarding:census.messages.incomplete"));
      return;
    }
    if (!census.accuracyAcknowledged) {
      setValidationMessage(t("onboarding:census.messages.notAcknowledged"));
      return;
    }
    setValidationMessage(null);
    onNext();
  }

  if (!vendorProfile) {
    return (
      <div className="census-poi">
        <div className="card">
          <div className="card-scroll">
            <PageHeader title={t("onboarding:census.title")} />
            <p className="subtitle">
              {t("onboarding:census.messages.vendorRequired")}
            </p>
          </div>
          <StepActions>
            <Button variant="secondary" onClick={onBack}>
              {t("common:actions.back")}
            </Button>
          </StepActions>
        </div>
      </div>
    );
  }

  const selectedListState = selectedListKey
    ? listState[selectedListKey]
    : undefined;

  const fileRows = (sftpFiles ?? []).map((file) => ({
    fileName: file.fileName,
    queriedAtText: formatDateTime(file.queriedAt),
    patientCountText: String(file.patients.length),
    patientNamesText: summarize(file.patients.map((p) => p.patientName)),
  }));

  async function handleExportEpicResults(): Promise<Blob> {
    const rows = [
      [
        t("onboarding:census.epic.summary.listName"),
        t("onboarding:census.epic.summary.listId"),
        t("onboarding:census.epic.summary.queriedAt"),
        t("onboarding:census.epic.summary.patientCount"),
        t("onboarding:census.epic.columns.patientId"),
      ],
      ...CENSUS_LIST_KEYS.filter((key) => listState[key]?.result).map((key) => {
        const state = listState[key]!;
        return [
          t(LIST_LABEL_KEYS[key]),
          census.patientListIds?.[key] ?? "",
          formatDateTime(state.queriedAt),
          String(state.result!.patientCount),
          state.result!.patientIds.join("; "),
        ];
      }),
    ];
    return new Blob([toCsv(rows)], { type: "text/csv;charset=utf-8;" });
  }

  async function handleExportSftpResults(): Promise<Blob> {
    const rows = [
      [
        t("onboarding:census.cerner.columns.fileName"),
        t("onboarding:census.cerner.columns.queriedAt"),
        t("onboarding:census.cerner.columns.patientCount"),
        t("onboarding:census.cerner.columns.patients"),
      ],
      ...fileRows.map((row) => [
        row.fileName,
        row.queriedAtText,
        row.patientCountText,
        row.patientNamesText,
      ]),
    ];
    return new Blob([toCsv(rows)], { type: "text/csv;charset=utf-8;" });
  }

  const exportFileName = `${user.facilityId ?? "facility"}_Census_Results.csv`;

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
      <div className="census-table-scroll">
        <table>
          <thead>
            <tr>
              <th>{t("onboarding:census.epic.columns.patientId")}</th>
            </tr>
          </thead>
          <tbody>
            {selectedListState.result.patientIds.map((id) => (
              <tr key={id}>
                <td>{id}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </SidePanel>
  );

  const frequencySection = (
    <div className="form-group">
      <label className="census-field-label" htmlFor="census-frequency-hours">
        {t("onboarding:census.fields.frequencyLabel")}
      </label>
      <p className="form-hint">
        {t("onboarding:census.fields.frequencyTooltip")}
      </p>
      <div className="census-triplet">
        <NumberField
          id="census-frequency-hours"
          label={t("onboarding:census.fields.hoursLabel")}
          min={0}
          step={1}
          value={frequencyHours}
          onChange={(value) => updateFrequency(value, frequencyMinutes)}
          onBlur={() => refreshFieldError("acquisitionFrequency")}
        />
        <NumberField
          id="census-frequency-minutes"
          label={t("onboarding:census.fields.minutesLabel")}
          min={0}
          max={59}
          step={1}
          value={frequencyMinutes}
          onChange={(value) => updateFrequency(frequencyHours, value)}
          onBlur={() => refreshFieldError("acquisitionFrequency")}
        />
      </div>
      {errors.acquisitionFrequency && (
        <p className="nhsn-link__form-error">
          {t(errors.acquisitionFrequency)}
        </p>
      )}
    </div>
  );

  return (
    <div className="census-poi">
      <SidePanelLayout>
        <div className="card">
          <div className="card-scroll">
            <PageHeader title={t("onboarding:census.title")} />
            <p className="subtitle">{t("onboarding:census.intro1")}</p>
            <p className="subtitle">{t("onboarding:census.intro2")}</p>

            {acquisition === "PatientList" && (
              <>
                <h2 className="census-section-heading">
                  {t("onboarding:census.epic.sectionTitle")}
                </h2>
                <p className="subtitle">
                  {t("onboarding:census.epic.subtitle")}
                </p>

                {vendorProfile.documentKeys.censusInstructions && (
                  <div className="census-instructions-box">
                    <p>{t("onboarding:census.epic.instructionsHint")}</p>
                    <DownloadLinkButton
                      buttonText={t(
                        "onboarding:census.epic.downloadInstructions",
                      )}
                      fileName="Epic_Census_Instructions.pdf"
                      onDownload={() =>
                        api.getDocument(
                          vendorProfile.documentKeys.censusInstructions!,
                        )
                      }
                    />
                  </div>
                )}

                {!patientListsLive && (
                  <MessageContainer type="warning" showIcon>
                    <span>{t("onboarding:messages.notConnected")}</span>
                  </MessageContainer>
                )}

                {CENSUS_LIST_KEYS.map((key) => {
                  const state = listState[key];
                  return (
                    <div
                      className={`form-group census-list-field${state?.result ? " is-validated" : ""}${state?.querying ? " is-querying" : ""}`}
                      key={key}>
                      <div className="census-list-input-row">
                        <TextField
                          id={`census-list-${key}`}
                          label={t(LIST_LABEL_KEYS[key])}
                          required
                          value={census.patientListIds?.[key] ?? ""}
                          error={
                            errors[`listId.${key}`]
                              ? t(errors[`listId.${key}`])
                              : undefined
                          }
                          onChange={(value) => updateListId(key, value)}
                          onBlur={() => refreshFieldError(`listId.${key}`)}
                        />
                        <button
                          type="button"
                          className={`census-view-btn${selectedListKey === key ? " active" : ""}`}
                          aria-label={t(
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
                      </div>
                      {state?.error && (
                        <p className="nhsn-link__form-error">{state.error}</p>
                      )}
                    </div>
                  );
                })}

                {frequencySection}

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
              </>
            )}

            {acquisition === "Sftp" && (
              <>
                <div className="section-title">
                  {t("onboarding:census.cerner.sectionTitle")}
                </div>

                {vendorProfile.documentKeys.censusInstructions && (
                  <div className="census-instructions-box">
                    <p>{t("onboarding:census.cerner.instructionsHint")}</p>
                    <DownloadLinkButton
                      buttonText={t(
                        "onboarding:census.cerner.downloadInstructions",
                      )}
                      fileName="Cerner_Census_Instructions.pdf"
                      onDownload={() =>
                        api.getDocument(
                          vendorProfile.documentKeys.censusInstructions!,
                        )
                      }
                    />
                  </div>
                )}

                <TextField
                  id="census-sftp-host"
                  label={t("onboarding:census.cerner.fields.hostLabel")}
                  required
                  value={census.sftpHost ?? ""}
                  error={errors.sftpHost ? t(errors.sftpHost) : undefined}
                  onChange={(value) => patch("census", { sftpHost: value })}
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
                  onChange={(value) => patch("census", { sftpPort: value })}
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
                    patch("census", { sftpRemoteDirectory: value })
                  }
                />

                <CheckboxField
                  id="census-sftp-remove"
                  label={t(
                    "onboarding:census.cerner.fields.removeAfterProcessingLabel",
                  )}
                  value={Boolean(census.sftpRemoveAfterProcessing)}
                  onChange={(value) =>
                    patch("census", { sftpRemoveAfterProcessing: value })
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

                {connectionResult && (
                  <p
                    className={
                      connectionResult.success
                        ? "census-test-success"
                        : "nhsn-link__form-error"
                    }>
                    {connectionResult.success
                      ? t("onboarding:census.cerner.testSuccess")
                      : t("onboarding:census.cerner.testFailure")}
                  </p>
                )}

                {sftpFiles !== null && (
                  <>
                    {!sftpListingLive && (
                      <MessageContainer type="warning" showIcon>
                        <span>{t("onboarding:messages.notConnected")}</span>
                      </MessageContainer>
                    )}
                    <div className="census-results">
                      <div className="section-title">
                        {t("onboarding:census.cerner.filesTitle")}
                      </div>
                      {fileRows.length === 0 ? (
                        <p className="subtitle">
                          {t("onboarding:census.cerner.noFiles")}
                        </p>
                      ) : (
                        <div className="census-table-scroll">
                          <table>
                            <thead>
                              <tr>
                                <th>
                                  {t(
                                    "onboarding:census.cerner.columns.fileName",
                                  )}
                                </th>
                                <th>
                                  {t(
                                    "onboarding:census.cerner.columns.queriedAt",
                                  )}
                                </th>
                                <th>
                                  {t(
                                    "onboarding:census.cerner.columns.patientCount",
                                  )}
                                </th>
                                <th>
                                  {t(
                                    "onboarding:census.cerner.columns.patients",
                                  )}
                                </th>
                              </tr>
                            </thead>
                            <tbody>
                              {fileRows.map((row) => (
                                <tr key={row.fileName}>
                                  <td>{row.fileName}</td>
                                  <td>{row.queriedAtText}</td>
                                  <td>{row.patientCountText}</td>
                                  <td>{row.patientNamesText}</td>
                                </tr>
                              ))}
                            </tbody>
                          </table>
                        </div>
                      )}
                    </div>
                  </>
                )}
              </>
            )}

            {resultsReady && (
              <CheckboxField
                id="census-accuracy-ack"
                label={t("onboarding:census.fields.accuracyAck")}
                value={Boolean(census.accuracyAcknowledged)}
                onChange={handleAckChange}
              />
            )}

            {validationMessage && (
              <p className="nhsn-link__form-error" role="alert">
                {validationMessage}
              </p>
            )}
          </div>

          <StepActions saving={saving}>
            <Button variant="secondary" onClick={onBack} disabled={saving}>
              {t("common:actions.back")}
            </Button>
            <Button onClick={handleNext} disabled={saving}>
              {t("common:actions.continue")}
            </Button>
          </StepActions>
        </div>
        {acquisition === "PatientList" && epicResultsPanel}
      </SidePanelLayout>
    </div>
  );
}

export default CensusStep;

function formatDateTime(iso?: string): string {
  if (!iso) {
    return "";
  }
  const parsed = new Date(iso);
  return Number.isNaN(parsed.getTime()) ? "" : parsed.toLocaleString();
}
