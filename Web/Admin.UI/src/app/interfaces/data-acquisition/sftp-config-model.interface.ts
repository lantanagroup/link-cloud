export interface ISftpConfigurationModel {
  id?: string;
  organizationId: string;
  host: string;
  port: number;
  remoteDirectory?: string;
  timeout: string;
  removeAfterProcessing: boolean;
  authenticationProtocol: string;
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
