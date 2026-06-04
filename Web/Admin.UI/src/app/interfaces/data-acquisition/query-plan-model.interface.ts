export interface IQueryPlanModel {
    id: string;
    planName: string;
    facilityId: string;
    ehrDescription: string;
    lookBack: string;
    initialQueries: Record<string, QueryConfigModel>;
    supplementalQueries: Record<string, QueryConfigModel>;
    type: string;
}

export type QueryConfigModel = IParameterQueryConfigModel | IReferenceQueryConfigModel;

export interface IQueryConfigModel {
    resourceType: string;
    operationType?: ReferenceQueryOperationType;
    queryConfigType: string;
}

export interface IParameterQueryConfigModel extends IQueryConfigModel {
    queryConfigType: 'Parameter';
    parameters: QueryParameterModel[];
}

export interface IReferenceQueryConfigModel extends IQueryConfigModel {
    queryConfigType: 'Reference';
    paged: number;
}

export type QueryParameterModel = ILiteralQueryParameterModel | IResourceIdsParameterModel | IVariableParameterModel;

export interface IQueryParameterModel {
    name: string;
}

export interface ILiteralQueryParameterModel extends IQueryParameterModel {
    parameterType: 'Literal';
    literal: string;
}

export interface IResourceIdsParameterModel extends IQueryParameterModel {
    parameterType: 'ResourceIds';
    resource: string;
    paged: string;
}

export interface IVariableParameterModel extends IQueryParameterModel {
    parameterType: 'Variable';
    format?: string;
    variable: VariableParameterType;
}

export enum VariableParameterType {
    patient = 0,
    lookbackStart = 1,
    periodStart = 2,
    periodEnd = 3
}

export enum ReferenceQueryOperationType {
    read = 0,
    search = 1,
    searchPost = 2
}
