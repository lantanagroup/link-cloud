export interface IOrganizationLocationConditionModel {
  conditionId?: number;
  fhirPath: string;
  priority: number;
}

export interface IOrganizationLocationConfigurationModel {
  configId?: number;
  facilityId: string;
  description?: string;
  isActive: boolean;
  conditions: IOrganizationLocationConditionModel[];
}

export interface ICreateOrganizationLocationConditionModel {
  fhirPath: string;
  priority: number;
}

export interface ICreateOrganizationLocationConfigurationModel {
  description?: string;
  isActive: boolean;
  conditions: ICreateOrganizationLocationConditionModel[];
}

export interface IUpdateOrganizationLocationConfigurationModel {
  description?: string;
  isActive?: boolean;
  conditions?: ICreateOrganizationLocationConditionModel[];
}
