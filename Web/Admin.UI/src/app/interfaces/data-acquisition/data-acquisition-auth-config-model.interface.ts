export interface IDataAcquisitionAuthenticationConfigModel {
    authType: string;
    key: string;
    tokenUrl: string;
    audience: string;
    clientId: string;
    clientSecret?: string;
    scope?: string;
    userName: string;
    password: string;
    customHeaders?: { [key: string]: string };
}
