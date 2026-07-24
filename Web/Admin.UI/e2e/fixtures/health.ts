import { ILinkServiceHealthSummary } from '../../src/app/components/monitor/link-health-check/link-service-health-summary.interface';
import { IServiceInfoModel } from '../../src/app/components/monitor/service-info.service';

export const healthSummaries: ILinkServiceHealthSummary[] = [
  { service: 'Tenant', status: 'Healthy', kafkaConnection: 'Healthy', databaseConnection: 'Healthy', cacheConnection: 'NotApplicable' },
  { service: 'Report', status: 'Healthy', kafkaConnection: 'Healthy', databaseConnection: 'Healthy', cacheConnection: 'Healthy' },
  { service: 'Validation', status: 'Unhealthy', kafkaConnection: 'Unhealthy', databaseConnection: 'Healthy', cacheConnection: 'Healthy' },
] as ILinkServiceHealthSummary[];

export const serviceInfos: IServiceInfoModel[] = [
  { serviceName: 'Tenant', version: '1.0.0', productVersion: '1.0.0-e2e', commit: 'abc1234', build: '42' },
  { serviceName: 'Validation', version: '1.0.0', productVersion: '1.0.0-e2e', commit: 'def5678', build: '42' },
];
