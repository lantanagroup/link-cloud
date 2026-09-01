import React, {useMemo} from 'react';
import {useTranslation} from 'react-i18next';
import {Button, MessageContainer, PageHeader, StepActions} from '../../../fields';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';

/**
 * Mockup measures and schedule. No reporting plan service exists in the Link
 * backend yet, so the real 6-month look-ahead is outstanding — this ramps up
 * the same way the POC does until every measure applies.
 */
const MEASURE_KEYS = [
  'onboarding:reportingPlan.measures.adultSepsis',
  'onboarding:reportingPlan.measures.antimicrobialUseResistance',
  'onboarding:reportingPlan.measures.cDifficile',
  'onboarding:reportingPlan.measures.glycemicControl',
  'onboarding:reportingPlan.measures.respiratoryPathogens'
] as const;

const REPORTING_PLAN_SCHEDULE: readonly (readonly string[])[] = [
  [MEASURE_KEYS[3]],
  [MEASURE_KEYS[0], MEASURE_KEYS[3]],
  [MEASURE_KEYS[0], MEASURE_KEYS[3], MEASURE_KEYS[4]],
  MEASURE_KEYS,
  MEASURE_KEYS,
  MEASURE_KEYS
];

const MONTH_KEYS = [
  'onboarding:reportingPlan.months.january',
  'onboarding:reportingPlan.months.february',
  'onboarding:reportingPlan.months.march',
  'onboarding:reportingPlan.months.april',
  'onboarding:reportingPlan.months.may',
  'onboarding:reportingPlan.months.june',
  'onboarding:reportingPlan.months.july',
  'onboarding:reportingPlan.months.august',
  'onboarding:reportingPlan.months.september',
  'onboarding:reportingPlan.months.october',
  'onboarding:reportingPlan.months.november',
  'onboarding:reportingPlan.months.december'
];

interface ReportingPlanRow {
  id: string;
  monthKey: string;
  year: number;
  measureKeys: readonly string[];
}

/** Six rows starting from `referenceDate`'s month, rolling the year over at December. */
function buildReportingPlanRows(referenceDate: Date): ReportingPlanRow[] {
  const rows: ReportingPlanRow[] = [];
  for (let i = 0; i < REPORTING_PLAN_SCHEDULE.length; i++) {
    const monthIndex = (referenceDate.getMonth() + i) % 12;
    const year = referenceDate.getFullYear() + Math.floor((referenceDate.getMonth() + i) / 12);
    rows.push({
      id: `${year}-${String(monthIndex + 1).padStart(2, '0')}`,
      monthKey: MONTH_KEYS[monthIndex],
      year,
      measureKeys: REPORTING_PLAN_SCHEDULE[i]
    });
  }
  return rows;
}

export function ReportingPlanStep({onNext, onBack}: StepProps) {
  const {t} = useTranslation(['onboarding', 'common']);
  const {saving} = useOnboarding();

  // Built locally on every visit — deterministic in the current month, so
  // nothing needs to be persisted for it to survive a reload.
  const rows = useMemo(() => {
    try {
      return buildReportingPlanRows(new Date());
    } catch {
      return [];
    }
  }, []);
  const hasSchedule = rows.length > 0;

  return (
    <div className="nhsn-link__content nhsn-link__reporting-plan">
      <PageHeader title={t('onboarding:reportingPlan.title')} />
      <p className="nhsn-link__subtitle">{t('onboarding:reportingPlan.subtitle')}</p>

      {hasSchedule ? (
        <div className="nhsn-link__reporting-plan-table-scroll">
          <table className="nhsn-link__reporting-plan-table">
            <thead>
              <tr>
                <th>{t('onboarding:reportingPlan.columns.month')}</th>
                <th>{t('onboarding:reportingPlan.columns.year')}</th>
                <th>{t('onboarding:reportingPlan.columns.measures')}</th>
              </tr>
            </thead>
            <tbody>
              {rows.map(row => {
                const measures = row.measureKeys.map(key => t(key)).sort((a, b) => a.localeCompare(b));
                return (
                  <tr key={row.id}>
                    <td>{t(row.monthKey)}</td>
                    <td>{row.year}</td>
                    <td>
                      <ul>
                        {measures.map(measure => (
                          <li key={measure}>{measure}</li>
                        ))}
                      </ul>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      ) : (
        <MessageContainer type="error" showIcon>
          <span role="alert">{t('onboarding:reportingPlan.scheduleUnavailable')}</span>
        </MessageContainer>
      )}

      <StepActions>
        <Button variant="secondary" onClick={onBack}>
          {t('common:actions.back')}
        </Button>
        <Button onClick={onNext} disabled={saving || !hasSchedule}>
          {t('common:actions.continue')}
        </Button>
      </StepActions>
    </div>
  );
}

export default ReportingPlanStep;
