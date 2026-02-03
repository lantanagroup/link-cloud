export enum SftpAcquisitionType {
  Census = 'Census',
  CernerCensus = 'CernerCensus',
  Resources = 'Resources'
}

export interface IFileParsingConfiguration {
  fileExtension: string;
  parserType: string;
  delimiter: string;
  hasHeaderRow: boolean;
  dateFormat: string;
  idSuffixToStrip?: string;
  columnMappings: { [key: string]: number };
  additionalProperties?: { [key: string]: string };
}

export interface ISftpAcquisitionTypeConfiguration {
  acquisitionType: SftpAcquisitionType;
  remoteDirectory?: string;
  processedDirectory?: string;
  fileNamePattern?: string;
  parsingConfiguration?: IFileParsingConfiguration;
}

export interface ISftpConfigurationModel {
  id?: string;
  organizationId: string;
  host: string;
  port: number;
  remoteDirectory?: string;
  timeout: string;
  removeAfterProcessing: boolean;
  authenticationProtocol: string;
  enableBenchmarking: boolean;
  acquisitionConfigurations: ISftpAcquisitionTypeConfiguration[];
}

export interface ISftpCredentialsModel {
  username: string;
  password: string;
}

export interface ISftpCredentialStatusModel {
  hasCredentials: boolean;
  lastUpdated?: Date;
}

export interface ICreateSftpConfigurationModel extends ISftpConfigurationModel {
  credentials?: ISftpCredentialsModel;
}

export interface ISftpConnectionTestResult {
  success: boolean;
  message: string;
}
