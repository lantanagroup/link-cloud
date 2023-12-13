@description('Location for all resources.')
param location string = resourceGroup().location
param envPrefix string

// <env>-<location>-link-<nameofresource>
var availabilitySetName = '${envPrefix}-${location}-link-as'
var virtualNetworkName = '${envPrefix}-${location}-link-vn'
var appEnvSubnetName = '${envPrefix}-${location}-link-appenvsn'
var networkInterfaceName = '${envPrefix}-${location}-link-appenvni'

var subnetRef = resourceId('Microsoft.Network/virtualNetworks/subnets', virtualNetworkName, appEnvSubnetName)
var numberOfInstances = 1


resource availabilitySet 'Microsoft.Compute/availabilitySets@2021-11-01' = {
  name: availabilitySetName
  location: location
  sku: {
    name: 'Aligned'
  }
  properties: {
    platformUpdateDomainCount: 2
    platformFaultDomainCount: 2
  }
}

resource virtualNetwork 'Microsoft.Network/virtualNetworks@2021-05-01' = {
  name: virtualNetworkName
  location: location
  properties: {
    addressSpace: {
      addressPrefixes: [
        '10.0.0.0/16'
      ]
    }
    subnets: [
      {
        name: appEnvSubnetName
        properties: {
          addressPrefix: '10.0.2.0/24'
        }
      }
    ]
  }
}

output virtualNetwork object = virtualNetwork
output vNetSubNetworkId string = first(virtualNetwork.properties.subnets).id

// resource networkInterface 'Microsoft.Network/networkInterfaces@2021-05-01' = [for i in range(0, numberOfInstances): {
//   name: '${networkInterfaceName}${i}'
//   location: location
//   properties: {
//     ipConfigurations: [
//       {
//         name: 'ipconfig1'
//         properties: {
//           privateIPAllocationMethod: 'Dynamic'
//           subnet: {
//             id: subnetRef
//           }
//           loadBalancerBackendAddressPools: [
//             {
//               id: resourceId('Microsoft.Network/loadBalancers/backendAddressPools', loadBalancerName, 'BackendPool1')
//             }
//           ]
//         }
//       }
//     ]
//   }
//   dependsOn: [
//     virtualNetwork
//   ]
// }]


