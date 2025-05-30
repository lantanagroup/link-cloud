export interface IQueryPlanModel {
    PlanName: string;
    FacilityId: string;
    EHRDescription: string;
    LookBack: string;
    InitialQueries: string;
    SupplementalQueries: string;
    Type: string;
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
