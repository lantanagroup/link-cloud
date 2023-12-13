/*
Resources to provision:
resourceGroup (if not exists)
Event Hubs Namespace
Azure Cosmos DB for MongoDB (vCore)
Container App (for each service)
Container Apps Environment
Container registry
Storage account

resource naming patterns:
<env>-<location>-link-<nameofresource>
<env><location>link<nameofresource>
*/

metadata description = 'Creates an ACA environment for NHSN Link'

param envPrefix string
param location string = 'centralus'
param subscriptionId string

param registryUrl string
param registryUsername string
param registryPassword string

//images
param apiImage string = 'latest'
param auditImage string = 'latest'
param censusImage string = 'latest'
param dataacqImage string = 'latest'
param measureevalImage string = 'latest'
param normImage string = 'latest'
param notificationImage string = 'latest'
param patientBundleImage string = 'latest'
param patientListImage string = 'latest'
param patientsToQueryImage string = 'latest'
param queryImage string = 'latest'
param reportImage string = 'latest'
param submissionImage string = 'latest'
param tenantImage string = 'latest'

//app configs
//param mongoDbConnection string
//dbnames
param apidbname string = 'botw-api'
param auditdbName string = 'botw-audit'
param censusDbName string = 'botw-census'
param dataacqDbName string = 'botw-data'
param measureevalDbName string = 'botw-measureEval'
param normDbName string = 'botw-normalization'
param notificationDbName string = 'botw-notification'
param patientBundleDbName string
param patientListDbName string
param patienttqueryDbName string = 'botw-patientsToQuery'
param queryDbName string = 'botw-query'
param reportDbName string = 'botw-report'
param submissionDbName string = 'botw-submission'
param tenantDbName string = 'botw-tenant'

param eventboostrapserver string

//set scope to subscription for resourceGroup Check
targetScope = 'subscription'

var rgName = '${envPrefix}-${location}-link-rg'

//rg new or existing
@allowed([
  'new'
  'existing'
])
param newOrExistingResourceGroup string = 'new'

//check if resource group exists and if not, create it
resource rgNew 'Microsoft.Resources/resourceGroups@2023-07-01' = if(newOrExistingResourceGroup == 'new') {
  name: rgName
  location: location
}

resource rgExisting 'Microsoft.Resources/resourceGroups@2023-07-01' existing = if (newOrExistingResourceGroup == 'existing'){
  name: rgName
}

var resourceGroup = ((newOrExistingResourceGroup == 'new') ? rgNew : rgExisting)

module vnet 'Networking/VNet.bicep' = {
  name: 'vNetDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    location: rgNew.location
  }
}

module stgModule 'StorageAccount/storageaccount.bicep' = {
  name: 'storageDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    location: location
  }
}

module cosmosMongoModule 'Cosmos/cosmos-mongo.bicep' = {
  name: 'mongoDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    location: location
  }
}

module eventHubsNamespaceModule 'EventHubs/eventhubs.bicep' = {
  name: 'eventHubDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    location: location
  }
}

module law 'LogAnalytics/LogAnalyticsWS.bicep' = {
  name: 'LogAnalyticsDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    location: location
  }
}

module appEnv 'ContainerApps/container-app-environment.bicep' = {
  name: 'containerAppDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    lawClientId: law.outputs.clientId
    lawClientSecret: law.outputs.clientSecret
    location: rgNew.location
    subNetResourceId: vnet.outputs.vNetSubNetworkId
  }
}

module api 'ContainerApps/container-apps-api.bicep' = {
  name: 'apiDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    containerPort: 8080
    location: rgNew.location
    containerImage: 'nhsnlink.azurecr.io/link-api:${apiImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: apidbname
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}

module audit 'ContainerApps/container-apps-audit.bicep' = {
  name: 'auditDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    containerPort: 8080
    location: rgNew.location
    containerImage: 'nhsnlink.azurecr.io/link-audit:${auditImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: auditdbName
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}

module census 'ContainerApps/container-apps-census.bicep' = {
  name: 'censusDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    containerPort: 8080
    location: rgNew.location
    containerImage: 'nhsnlink.azurecr.io/link-census:${censusImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: censusDbName
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}

module dataacq 'ContainerApps/container-apps-dataacq.bicep' = {
  name: 'dataacqDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    containerPort: 8080
    location: rgNew.location
    containerImage: 'nhsnlink.azurecr.io/link-dataacq:${dataacqImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: dataacqDbName
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}

module measureeval 'ContainerApps/container-apps-measureeval.bicep' = {
  name: 'measureevalDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    containerPort: 8080
    location: rgNew.location
    containerImage: 'nhsnlink.azurecr.io/link-measureeval:${measureevalImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: measureevalDbName
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}

module normalization 'ContainerApps/container-apps-normalization.bicep' = {
  name: 'normalizationDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    containerPort: 8080
    location: rgNew.location
    containerImage: 'nhsnlink.azurecr.io/link-normalization:${normImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: normDbName
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}

module notification 'ContainerApps/container-apps-notification.bicep' = {
  name: 'notificationDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    location: rgNew.location
    containerPort: 8080
    containerImage: 'nhsnlink.azurecr.io/link-notification:${notificationImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: notificationDbName
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}

module patientstoquery 'ContainerApps/container-apps-patientstoquery.bicep' = {
  name: 'patientstoqueryDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    location: rgNew.location
    containerPort: 8080
    containerImage: 'nhsnlink.azurecr.io/link-patientstoquery:${patientsToQueryImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: patienttqueryDbName
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}

module querydispatch 'ContainerApps/container-apps-querydispatch.bicep' = {
  name: 'querydispatchDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    location: rgNew.location
    containerPort: 8080
    containerImage: 'nhsnlink.azurecr.io/link-querydispatch:${queryImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: queryDbName
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}

module report 'ContainerApps/container-apps-report.bicep' = {
  name: 'reportDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    location: rgNew.location
    containerPort: 8080
    containerImage: 'nhsnlink.azurecr.io/link-report:${reportImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: reportDbName
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}

module submission 'ContainerApps/container-apps-submission.bicep' = {
  name: 'submissionDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    location: rgNew.location
    containerPort: 8080
    containerImage: 'nhsnlink.azurecr.io/link-submission:${submissionImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: submissionDbName
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}

module tenant 'ContainerApps/container-apps-tenant.bicep' = {
  name: 'tenantDeploy'
  scope: rgNew
  params: {
    envPrefix: envPrefix
    containerAppEnvId: appEnv.outputs.appEnvId
    location: rgNew.location
    containerPort: 8080
    containerImage: 'nhsnlink.azurecr.io/link-tenant:${tenantImage}'
    ehBootStrapServers: eventboostrapserver
    mongoDbName: tenantDbName
    mongoConnectionStr: cosmosMongoModule.outputs.databaseAccount.connectionStrings[0].connectionString
    registryPassword: registryPassword
    registryUrl: registryUrl
    registryUsername: registryUsername
    metricsEndpoint: ''
    telemetryCollectionEndpoint: ''
    traceExporterEndpoint: ''
  }
}
