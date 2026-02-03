import { SftpAcquisitionType } from '../../../../interfaces/data-acquisition/sftp-config-model.interface';

export interface SftpAcquisitionBenchmark {
  attemptNumber: number;
  attemptStartedAt: Date;
  connectionAndRetrievalDurationMs: number;
  parseDurationMs: number;
  itemsProcessed: number;
  isSuccessful: boolean;
  overheadDurationMs: number;
  totalDurationMs: number;
}

export interface SftpAcquisitionLog {
  id: number;
  externalId: string;
  facilityId: string;
  acquisitionType: SftpAcquisitionType;
  fileNames: string[];
  scheduledDate?: Date;
  processDate?: Date;
  retryAttempts?: number;
  status: string;
  originatingTraceId?: string;
  originatingSpanId?: string;
  notes: string[];
  benchmarks?: SftpAcquisitionBenchmark[];
}
