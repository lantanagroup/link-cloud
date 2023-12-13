param envPrefix string
param location string
param lawClientId string
param lawClientSecret string
param subNetResourceId string

//<env>-<location>-link-<nameofresource>
var appEnvName = '${envPrefix}-${location}-link-conappenv'

resource appEnv 'Microsoft.Web/kubeEnvironments@2022-09-01' = {
  name: appEnvName
  location: location
  properties: {
    type: 'managed'
    internalLoadBalancerEnabled: false
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: lawClientId
        sharedKey: lawClientSecret
      }
    }
    containerAppsConfiguration: {
      appSubnetResourceId: subNetResourceId
    }
  }
}

output appEnv object = appEnv
output appEnvId string = appEnv.id
