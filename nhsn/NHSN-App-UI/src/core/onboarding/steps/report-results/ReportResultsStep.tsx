import React, { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';
import { useApiClient } from '../../../api/ApiClientContext';
import { useNotifications } from '../../../notifications/NotificationProvider';
import type { StepProps } from '../../flow';
import { useOnboarding, useStepValidator } from '../../OnboardingProvider';
import { ReportDetailView } from './ReportDetailView';
import { ReportListView } from './ReportListView';

/**
 * Decides between the two Report Results sub-views (the reports list and a single report's
 * detail, gated by `draft.currentView.view`) and owns the one thing both share: the
 * report-accuracy acknowledgement that gates Continue. That gate is registered with
 * useStepValidator here, not in ReportListView, so it stays registered with the provider
 * regardless of which sub-view is currently mounted.
 *
 * What is already wired and should not be rebuilt: draft access and patching
 * via useOnboarding(), navigation via onNext/onBack, gating and URL sync via
 * the provider, and every control through core/fields.
 */
export function ReportResultsStep({ onNext, onBack }: StepProps) {
  const { t } = useTranslation(['onboarding', 'common']);
  const api = useApiClient();
  const { notifyError } = useNotifications();
  const { draft, mirror } = useOnboarding();

  const [validationMessage, setValidationMessage] = useState<string | null>(
    null,
  );

  const { data: reportsPage } = useQuery({
    queryKey: ['reports'],
    queryFn: () => api.listReports({ page: 1, pageSize: 50 }),
  });
  const reports = reportsPage?.items ?? [];

  // The accuracy acknowledgement checkbox sits on the list view (there's no other report the
  // "these results are accurate" statement could mean there), scoped to the most recently
  // generated report -- newest first, so reports[0]. Backend-authoritative and keyed by report id,
  // so generating a new report naturally re-requires acknowledgement: the new report's id has no
  // acknowledgement row yet, with no separate "revoke" step needed. Same query key as
  // ReportListView's own copy of this query, so both share one cache entry.
  const latestReportId = reports[0]?.reportId;
  const { data: reportAccuracyAcknowledged } = useQuery({
    queryKey: ['reportAccuracyAcknowledgement', latestReportId],
    queryFn: () => api.getReportAcknowledgement(latestReportId!),
    enabled: Boolean(latestReportId),
    // The query key already carries the report id, so a different report always fetches fresh
    // regardless of staleTime -- there's no cached value to serve stale for a key seen for the
    // first time. For the same report, ReportListView's checkbox keeps this cache entry correct
    // on every write, so there's never a reason to re-fetch it from the network within the session.
    staleTime: Infinity,
  });

  useEffect(() => {
    if (latestReportId) {
      mirror('reportResults', {
        accuracyAcknowledged: Boolean(reportAccuracyAcknowledged),
      });
    }
  }, [latestReportId, reportAccuracyAcknowledged, mirror]);

  function validateStep(): boolean {
    if (!reportAccuracyAcknowledged) {
      setValidationMessage(
        t('onboarding:reportResults.messages.notAcknowledged'),
      );
      return false;
    }
    setValidationMessage(null);
    return true;
  }

  async function handleNext() {
    if (!validateStep()) {
      return;
    }
    try {
      await api.acknowledgeReport(latestReportId!, {
        kind: 'ReportAccuracy',
        accepted: true,
        statementKey: 'report-accuracy',
      });
    } catch (cause) {
      notifyError(
        cause instanceof Error
          ? cause.message
          : t('onboarding:reportResults.messages.ackError'),
      );
      return;
    }
    onNext();
  }

  useStepValidator(validateStep);

  const viewingDetail = draft.currentView?.view === 'detail';

  return viewingDetail ? (
    <ReportDetailView />
  ) : (
    <ReportListView
      onBack={onBack}
      onNext={handleNext}
      validationMessage={validationMessage}
      clearValidationMessage={() => setValidationMessage(null)}
    />
  );
}

export default ReportResultsStep;
