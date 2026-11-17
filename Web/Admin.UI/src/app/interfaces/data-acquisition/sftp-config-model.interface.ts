export enum SftpAcquisitionType {
  Census = 'Census',
  Resources = 'Resources'
}

export enum SftpAcquisitionSubType {
  None = 'None',
  CernerCCLExtract = 'CernerCCLExtract',
  HL7ADT = 'HL7ADT'
}

/**
 * How a facility's HL7 v2 ADT feed identifies patients. Exactly one of patientIdLocation and
 * mrnIdentifierSystem is set.
 */
export interface IAdtConfiguration {
  patientIdLocation?: string;
  mrnIdentifierSystem?: string;
  mrnLocation?: string;
  encounterIdLocation?: string;
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
  subType: SftpAcquisitionSubType;
  remoteDirectory?: string;
  processedDirectory?: string;
  fileNamePattern?: string;
  parsingConfiguration?: IFileParsingConfiguration;
  adtConfiguration?: IAdtConfiguration;
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
