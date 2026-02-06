import { IPaginationMetadata } from '../../../../interfaces/pagination-metadata.interface';
import { SftpAcquisitionType, SftpAcquisitionSubType } from '../../../../interfaces/data-acquisition/sftp-config-model.interface';

export interface SftpAcquisitionLogSummary {
  id: number;
  externalId: string;
  facilityId: string;
  acquisitionType: SftpAcquisitionType;
  subType: SftpAcquisitionSubType;
  fileNames: string[];
  scheduledDate?: Date;
  processDate?: Date;
  status: string;
  retryAttempts?: number;
}

export interface IPagedSftpAcquisitionLogSummary {
  records: SftpAcquisitionLogSummary[];
  metadata: IPaginationMetadata;
}
