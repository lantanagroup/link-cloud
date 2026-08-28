import React, {useMemo} from 'react';
import {useTranslation} from 'react-i18next';
import {Button, PageHeader, StepActions} from '../../../fields';
import type {StepProps} from '../../flow';
import {useOnboarding} from '../../OnboardingProvider';

/**
 * Mockup measures and schedule. No reporting plan service exists in the Link
 * backend yet, so the real 6-month look-ahead is outstanding — this ramps up
 * the same way the POC does until every measure applies.
 */
const MEASURES = [
  'Adult Sepsis Bacteria & Fungemia',
  'Antimicrobial Use and Resistance (AU/AR)',
  'C. Difficile Infection',
  'Glycemic Control',
  'Respiratory Pathogens Surveillance (RPS)'
];

const REPORTING_PLAN_SCHEDULE: readonly string[][] = [
  ['Glycemic Control'],
  ['Adult Sepsis Bacteria & Fungemia', 'Glycemic Control'],
  ['Adult Sepsis Bacteria & Fungemia', 'Glycemic Control', 'Respiratory Pathogens Surveillance (RPS)'],
  MEASURES,
  MEASURES,
  MEASURES
];

const MONTH_NAMES = [
  'January',
  'February',
  'March',
  'April',
  'May',
  'June',
  'July',
  'August',
  'September',
  'October',
  'November',
  'December'
];

interface ReportingPlanRow {
  id: string;
  month: string;
  year: number;
  measures: string[];
}

/** Six rows starting from `referenceDate`'s month, rolling the year over at December. */
function buildReportingPlanRows(referenceDate: Date): ReportingPlanRow[] {
  const rows: ReportingPlanRow[] = [];
  for (let i = 0; i < REPORTING_PLAN_SCHEDULE.length; i++) {
    const monthIndex = (referenceDate.getMonth() + i) % 12;
    const year = referenceDate.getFullYear() + Math.floor((referenceDate.getMonth() + i) / 12);
    rows.push({
      id: `${year}-${String(monthIndex + 1).padStart(2, '0')}`,
      month: MONTH_NAMES[monthIndex],
      year,
      measures: [...REPORTING_PLAN_SCHEDULE[i]].sort((a, b) => a.localeCompare(b))
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
              {rows.map(row => (
                <tr key={row.id}>
                  <td>{row.month}</td>
                  <td>{row.year}</td>
                  <td>
                    <ul>
                      {row.measures.map(measure => (
                        <li key={measure}>{measure}</li>
                      ))}
                    </ul>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <p className="nhsn-link__state--error" role="alert">
          {t('onboarding:reportingPlan.scheduleUnavailable')}
        </p>
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
