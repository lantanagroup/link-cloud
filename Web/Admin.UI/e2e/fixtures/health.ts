import { ILinkServiceHealthSummary } from '../../src/app/components/monitor/link-health-check/link-service-health-summary.interface';
import { IServiceInfoModel } from '../../src/app/components/monitor/service-info.service';

/**
 * The service keys the health screen hardcodes in initHealthSummary(), in its order. The
 * screen builds its own rows and asks each service for its own health, so a key left
 * unmocked renders as Unknown *and* makes the component console.error — which the tripwire
 * fails the test for. Keep this list in step with the component.
 */
export const HEALTH_SERVICE_KEYS = [
  'account',
  'adminbff',
  'audit',
  'census',
  'dataacquisition',
  'normalization',
  'querydispatch',
  'report',
  'submission',
  'tenant',
  'measureeval',
  'validation',
  'terminology',
];

/** How each service names itself in its response; this is what the row shows. */
const DISPLAY_NAMES: Record<string, string> = {
  account: 'Account',
  adminbff: 'AdminBFF',
  audit: 'Audit',
  census: 'Census',
  dataacquisition: 'DataAcquisition',
  normalization: 'Normalization',
  querydispatch: 'QueryDispatch',
  report: 'Report',
  submission: 'Submission',
  tenant: 'Tenant',
  measureeval: 'MeasureEval',
  validation: 'Validation',
  terminology: 'Terminology',
};

const healthy = (service: string): ILinkServiceHealthSummary => ({
  service,
  status: 'Healthy',
  totalDuration: '00:00:00.0421',
  entries: {
    kafka: { status: 'Healthy', duration: '00:00:00.0180', description: null },
    database: { status: 'Healthy', duration: '00:00:00.0240', description: null },
  },
});

/** One summary per key. Validation is the unhealthy one the spec asserts on. */
export const healthByService: Record<string, ILinkServiceHealthSummary> = Object.fromEntries(
  HEALTH_SERVICE_KEYS.map((key) => [key, healthy(DISPLAY_NAMES[key])]),
);

healthByService['validation'] = {
  service: 'Validation',
  status: 'Unhealthy',
  totalDuration: '00:00:02.5140',
  entries: {
    kafka: { status: 'Unhealthy', duration: '00:00:02.4600', description: 'No connection could be made to the broker.' },
    database: { status: 'Healthy', duration: '00:00:00.0310', description: null },
  },
};

export const serviceInfos: IServiceInfoModel[] = [
  { serviceName: 'Tenant', version: '1.0.0', productVersion: '1.0.0-e2e', commit: 'abc1234', build: '42' },
  { serviceName: 'Validation', version: '1.0.0', productVersion: '1.0.0-e2e', commit: 'def5678', build: '42' },
];
