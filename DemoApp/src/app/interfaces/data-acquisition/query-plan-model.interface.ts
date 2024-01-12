export interface IQueryPlanModel {
    id: string;
    planName: string;
    reportType: string;
    facilityId: string;
    eHRDescription: string;
    lookback: string;
    initialQueries: Map<string, QueryConfigModel>;
    supplementalQueries: Map<string, QueryConfigModel>;
}

export type QueryConfigModel = IParameterQueryConfigModel | IReferenceQueryConfigModel;

export interface IQueryConfigModel {
    resourceType: string;
}

export interface IParameterQueryConfigModel extends IQueryConfigModel {
    parameterName: string;
    parameters: QueryParameterModel[];
}

export interface IReferenceQueryConfigModel extends IQueryConfigModel {
    resourceType: string;
    operationType: ReferenceQueryOperationType;
    paged: number;
}

export type QueryParameterModel = ILiteralQueryParameterModel | IResourceIdsParameterModel | IVariableParameterModel;

export interface IQueryParameterModel {
    name: string;
}

export interface ILiteralQueryParameterModel extends IQueryParameterModel {
    literal: string;
}

export interface IResourceIdsParameterModel extends IQueryParameterModel {
    resource: string;
    paged: string;
}

export interface IVariableParameterModel extends IQueryParameterModel {
    format?: string;
    variable: VariableParameterType;
}

export enum VariableParameterType { 
    patientId = 0,
    lookbackStart = 1,
    periodStart = 2,
    periodEnd = 3
}

export enum ReferenceQueryOperationType {
    read = 0,
    search = 1
}
