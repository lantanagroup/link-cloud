
//general params
param location string
param envPrefix string
param containerAppEnvId string
param registryUrl string
param registryPassword string
param registryUsername string
// Networking
param useExternalIngress bool = false
param containerPort int
// Container Image ref
param containerImage string

//app params
param mongoConnectionStr string
param mongoDbName string
param ehBootStrapServers string
param traceExporterEndpoint string
param metricsEndpoint string
param telemetryCollectionEndpoint string


var serviceName = 'querydispatch'

//<env>-<location>-link-<nameofresource>
var name = '${envPrefix}-${location}-link-${serviceName}-app'

resource containerApp 'Microsoft.Web/containerApps@2021-03-01' = {
  name: name
  kind: 'containerapp'
  location: location
  properties: {
    kubeEnvironmentId: containerAppEnvId
    configuration: {
      secrets: [
        {
          name: 'container-registry-password'
          value: registryPassword
        }
      ]      
      registries: [
        {
          server: registryUrl
          username: registryUsername
          passwordSecretRef: 'container-registry-password'
        }
      ]
      ingress: {
        external: useExternalIngress
        targetPort: containerPort
      }
    }
    template: {
      containers: [
        {
          image: containerImage
          name: serviceName
          env: [
            {
              name: 'KafkaConnection__BootstrapServers__0'
              value: ehBootStrapServers
            }
            {
              name: 'MongoDB__ConnectionString'
              value: mongoConnectionStr
            }
            {
              name: 'MongoDB__DatabaseName'
              value: mongoDbName
            }
          ]
        }
      ]
      scale: {
        minReplicas: 0
        maxReplicas: 10
      }
    }
  }
}
