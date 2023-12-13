@description('Specifies the environment name that is used to generate the Event Hub name and the Namespace name.')
param envPrefix string

@description('Specifies the Azure location for all resources.')
param location string = resourceGroup().location

@description('Specifies the messaging tier for Event Hub Namespace.')
@allowed([
  'Basic'
  'Standard'
])
param eventHubSku string = 'Standard'

@description('Upper limit of throughput units when AutoInflate is enabled, value should be within 0 to 20 throughput units. 0 if AutoInflateEnabled = true')
param throughputUnitsMax int = 3

@description('The default setting for partitions in a event hub or topic')
param defaultPartitionCount int = 2

//<env>-<location>-link-<nameofresource>
var projectName = '${envPrefix}-${location}-link-eh'
var eventHubNamespaceName = '${projectName}-ns'
//var eventHubName = projectName

resource eventHubNamespace 'Microsoft.EventHub/namespaces@2021-11-01' = {
  name: eventHubNamespaceName
  location: location
  sku: {
    name: eventHubSku
    tier: eventHubSku
    capacity: 1
  }
  properties: {
    kafkaEnabled: true
    isAutoInflateEnabled: false
    maximumThroughputUnits: throughputUnitsMax
  }
}

resource auditableEventOccurred 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'AuditableEventOccurred'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource bundleEvalRequested 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'BundleEvalRequested'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource dataAcquisitionRequested 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'DataAcquisitionRequested'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource evaluationRequested 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'EvaluationRequested'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource MeasureEvaluated 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'MeasureEvaluated'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource MeasureReportCreated 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'MeasureReportCreated'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource NotificationRequested 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'NotificationRequested'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource PatientAcquired 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'PatientAcquired'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource PatientCensusScheduled 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'PatientCensusScheduled'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource PatientEvent 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'PatientEvent'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource PatientIDsAcquired 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'PatientIDsAcquired'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource PatientNormalized 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'PatientNormalized'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource PatientsToQuery 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'PatientsToQuery'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource PatientsToQueryKSTREAM 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'PatientsToQuery-KSTREAM-AGGREGATE-STATE-STORE-0000000007-changelog'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource ReportBundled 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'ReportBundled'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource ReportRequested 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'ReportRequested'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource ReportScheduled 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'ReportScheduled'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource ReportSubmitted 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'ReportSubmitted'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource RetentionCheckScheduled 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'RetentionCheckScheduled'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}

resource SubmitReport 'Microsoft.EventHub/namespaces/eventhubs@2021-11-01' = {
  parent: eventHubNamespace
  name: 'SubmitReport'
  properties: {
    messageRetentionInDays: 7
    partitionCount: defaultPartitionCount
  }
}
var endpoint = '${eventHubNamespaceName}.servicebus.windows.net:9093'
output eventHubNamespaceObj object = eventHubNamespace
output eventHubConnectionString string = endpoint
