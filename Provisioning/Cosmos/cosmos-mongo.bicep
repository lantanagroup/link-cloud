targetScope = 'resourceGroup'

param envPrefix string
@description('Location for the Cosmos DB account.')
param location string = resourceGroup().location

@description('The primary replica region for the Cosmos DB account.')
param primaryRegion string = location

@description('The secondary replica region for the Cosmos DB account.')
param secondaryRegion string = 'westus'

@allowed([
  'Eventual'
  'ConsistentPrefix'
  'Session'
  'BoundedStaleness'
  'Strong'
])
@description('The default consistency level of the Cosmos DB account.')
param defaultConsistencyLevel string = 'Eventual'

@allowed([
  '3.2'
  '3.6'
  '4.0'
  '4.2'
  '5.0'
])
@description('Specifies the MongoDB server version to use.')
param serverVersion string = '5.0'

@minValue(10)
@maxValue(2147483647)
@description('Max stale requests. Required for BoundedStaleness. Valid ranges, Single Region: 10 to 2147483647. Multi Region: 100000 to 2147483647.')
param maxStalenessPrefix int = 100000

@minValue(5)
@maxValue(86400)
@description('Max lag time (seconds). Required for BoundedStaleness. Valid ranges, Single Region: 5 to 84600. Multi Region: 300 to 86400.')
param maxIntervalInSeconds int = 300

@minValue(400)
@maxValue(1000000)
@description('The shared throughput for the Mongo DB database, up to 25 collections')
param sharedThroughput int = 400

//<env>-<location>-link-<nameofresource>
var cmName = '${envPrefix}-${location}-link-cosmosmongo'
var accountName = 'cosmos-${uniqueString(resourceGroup().id)}'

var consistencyPolicy = {
  Eventual: {
    defaultConsistencyLevel: 'Eventual'
  }
  ConsistentPrefix: {
    defaultConsistencyLevel: 'ConsistentPrefix'
  }
  Session: {
    defaultConsistencyLevel: 'Session'
  }
  BoundedStaleness: {
    defaultConsistencyLevel: 'BoundedStaleness'
    maxStalenessPrefix: maxStalenessPrefix
    maxIntervalInSeconds: maxIntervalInSeconds
  }
  Strong: {
    defaultConsistencyLevel: 'Strong'
  }
}

var locations = [
  {
    locationName: primaryRegion
    failoverPriority: 0
    isZoneRedundant: false
  }
  {
    locationName: secondaryRegion
    failoverPriority: 1
    isZoneRedundant: false
  }
]

resource account 'Microsoft.DocumentDB/databaseAccounts@2023-09-15' = {
  name: toLower(accountName)
  location: location
  kind: 'MongoDB'
  properties: {
    consistencyPolicy: consistencyPolicy[defaultConsistencyLevel]
    locations: locations
    databaseAccountOfferType: 'Standard'
    enableAutomaticFailover: true
    apiProperties: {
      serverVersion: serverVersion
    }
    capabilities: [
      {
        name: 'DisableRateLimitingResponses'
      }
    ]
  }
}

resource apidb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-api'
  properties: {
    resource: {
      id: 'botw-api'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource auditdb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-audit'
  properties: {
    resource: {
      id: 'botw-audit'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource censusdb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-census'
  properties: {
    resource: {
      id: 'botw-census'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource dataacqdb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-data'
  properties: {
    resource: {
      id: 'botw-data'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource measureevaldb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-measureEval'
  properties: {
    resource: {
      id: 'botw-measureEval'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource normalizationdb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-normalization'
  properties: {
    resource: {
      id: 'botw-normalization'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource notificationdb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-notification'
  properties: {
    resource: {
      id: 'botw-notification'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource patientstoquerydb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-patientsToQuery'
  properties: {
    resource: {
      id: 'botw-patientsToQuery'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource querydispatchdb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-query'
  properties: {
    resource: {
      id: 'botw-query'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource reportdb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-report'
  properties: {
    resource: {
      id: 'botw-report'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource submissiondb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-submission'
  properties: {
    resource: {
      id: 'botw-submission'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

resource tenantdb 'Microsoft.DocumentDB/databaseAccounts/mongodbDatabases@2023-09-15' = {
  parent: account
  name: 'botw-tenant'
  properties: {
    resource: {
      id: 'botw-measureEval'
    }
    options: {
      throughput: sharedThroughput
    }
  }
}

output databaseAccount object = account
