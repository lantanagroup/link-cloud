# Configuration key inventory

Every configuration key the code reads, derived from source by
`Scripts/AzureAppConfig/extract_config_keys.py`. **Generated - do not edit by hand.**

Regenerate with:

```powershell
python Scripts/AzureAppConfig/extract_config_keys.py
```

This is the exhaustive reference. `/app-config.yaml` is the curated catalog of
keys that must be provisioned per environment, and is deliberately much shorter.

Columns: **Catalog** - present in app-config.yaml. **Stores** - environments
holding a row for it. **Source** - where the code reads it.

## Declared but not bindable

`ConfigurationBinder` binds public *properties*. These are public **fields**, so a
value set in a store can never take effect.

| Key | Declaring type | In stores |
|---|---|---|
| `Telemetry:EnableOtelCollector` | `TelemetrySettings` | dev, qa, test |

## Keys by service

### (unattributed)

14 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `MockDmrpApi` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:23` |
| `MockDmrpApi:Audience` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:23` |
| `MockDmrpApi:AuthClientId` | dotnet | yes | - | `DotNet/MockDmrpApi/Program.cs:23` |
| `MockDmrpApi:AuthClientSecret` | dotnet | yes | - | `DotNet/MockDmrpApi/Program.cs:23` |
| `MockDmrpApi:Enabled` | dotnet | yes | - | `DotNet/MockDmrpApi/Program.cs:23` |
| `MockDmrpApi:Issuer` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:23` |
| `MockDmrpApi:SigningKey` | dotnet | yes | - | `DotNet/MockDmrpApi/Program.cs:23` |
| `MockDmrpApi:TokenLifetimeSeconds` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:23` |
| `MockFhirServer` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ClinicalPeriodEnd` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ClinicalPeriodStart` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:GenerationSeed` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:PreGeneratedPatientCount` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ResourcesPerPatient` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |

### Account

122 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:140` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:145` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:146` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `Cache:Type` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:101` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:164` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:114` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:147` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:98` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:77` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:252` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Account/Program.cs:73` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `SecretManagement:Manager` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:136` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `UserManagement` | dotnet | - | - | `DotNet/Account/Program.cs:83` |
| `UserManagement:EnableAutomaticUserActivation` | dotnet | yes | - | `DotNet/Account/Program.cs:83` |

### AdminBFF

148 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:DefaultChallengeScheme` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:26` |
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:36` |
| `Authentication:Schemas:Cookie:Domain` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:83` |
| `Authentication:Schemas:Cookie:HttpOnly` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:76` |
| `Authentication:Schemas:Cookie:Path` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:78` |
| `Authentication:Schemas:Jwt:Audience` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:189` |
| `Authentication:Schemas:Jwt:Authority` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:188` |
| `Authentication:Schemas:Jwt:Enabled` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:179` |
| `Authentication:Schemas:Jwt:NameClaimType` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:191` |
| `Authentication:Schemas:Jwt:RequireHttpsMetadata` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:190` |
| `Authentication:Schemas:Jwt:RoleClaimType` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:192` |
| `Authentication:Schemas:Oauth2:CallbackPath` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:144` |
| `Authentication:Schemas:Oauth2:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:139` |
| `Authentication:Schemas:Oauth2:ClientSecret` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:140` |
| `Authentication:Schemas:Oauth2:Enabled` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:130` |
| `Authentication:Schemas:Oauth2:Endpoints:Authorization` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:141` |
| `Authentication:Schemas:Oauth2:Endpoints:Token` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:142` |
| `Authentication:Schemas:Oauth2:Endpoints:UserInformation` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:143` |
| `Authentication:Schemas:OpenIdConnect:Authority` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:165` |
| `Authentication:Schemas:OpenIdConnect:CallbackPath` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:168` |
| `Authentication:Schemas:OpenIdConnect:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:166` |
| `Authentication:Schemas:OpenIdConnect:ClientSecret` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:167` |
| `Authentication:Schemas:OpenIdConnect:Enabled` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:156` |
| `Authentication:Schemas:OpenIdConnect:NameClaimType` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:169` |
| `Authentication:Schemas:OpenIdConnect:RoleClaimType` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:170` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:199` |
| `Cache` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:ConnectionString` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:InstanceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:Password` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:Timeout` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:Type` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:90` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:141` |
| `DataProtection` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:87` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:87` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:87` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableIntegrationFeature` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:237` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:96` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:207` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:210` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:89` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:323` |
| `MonitorBackendHealthChecks` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:243` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:82` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:147` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ReverseProxy` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/YarpProxyExtensioncs.cs:15` |
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:86` |
| `SecretManagement:Manager` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:165` |
| `SecretManagement:ManagerUri` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:86` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:88` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Audit

120 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:167` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:172` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:173` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:72` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Audit/Program.cs:72` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:72` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Audit/Program.cs:72` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:107` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Audit/Persistance/AuditDbContext.cs:41` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Audit/Program.cs:71` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:71` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:71` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:71` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:174` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Audit/Program.cs:102` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:69` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:73` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:197` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Audit/Program.cs:65` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Audit/Program.cs:70` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### AutomationUI

138 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `ApiHealth:EnableAdminBffAuthSuite` | dotnet | yes | test | `DotNet/Automation.UI/Program.cs:250` |
| `Authentication:ApiBearer:Audience` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:102` |
| `Authentication:ApiBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:101` |
| `Authentication:ApiBearer:Enabled` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:100` |
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:94` |
| `Authentication:UseBearerForServiceCalls` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:75` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `Automation` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:DownloadPath` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FacilityFhirServerBase` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirGeneration:IncludeLowValueOptionalReferences` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirGeneration:ResourceDistribution` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirGeneration:ResourceDistribution:{Placeholder}` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirQuery:MaxAcquisitionPullTime` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirQuery:MaxConcurrentRequests` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirQuery:MinAcquisitionPullTime` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirQuery:TimeZone` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirServerBase` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirServerBasicAuth:Password` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirServerBasicAuth:ShouldAuthenticate` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirServerBasicAuth:Username` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirServerOAuth:ClientId` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirServerOAuth:ClientSecret` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirServerOAuth:Password` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirServerOAuth:Scope` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirServerOAuth:ShouldAuthenticate` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirServerOAuth:TokenEndpoint` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:FhirServerOAuth:Username` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:Kafka:RestProxyBaseUrl` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:LokiAppLabel` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `Automation:LokiBaseUrl` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:39` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `Dashboard:SeedFakeRuns` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:295` |
| `DataProtection:ApplicationName` | dotnet | yes | dev | `DotNet/Automation.UI/Program.cs:210` |
| `DataProtection:KeyCollectionName` | dotnet | yes | - | `DotNet/Automation.UI/Program.cs:212` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalBlobStorage:SuppressManifest` | dotnet | yes | test | `DotNet/Automation.UI/Services/RunExecutor.cs:71` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `InternalBlobStorage` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `InternalBlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:40` |
| `InternalBlobStorage:BlobRoot` | dotnet | yes | - | `DotNet/Automation.UI/Program.cs:40` |
| `InternalBlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:40` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:60` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/Automation.UI/Program.cs:60` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/Automation.UI/Program.cs:60` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:60` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:60` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:60` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:60` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:60` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:60` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:60` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:60` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:69` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:69` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Automation.UI/Program.cs:69` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Automation.UI/Program.cs:69` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Automation.UI/Program.cs:69` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:69` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Automation.UI/Program.cs:69` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Loki:App` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:48` |
| `Loki:Url` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:42` |
| `MongoDB:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:180` |
| `MongoDB:DatabaseName` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:181` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:32` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Automation.UI/Program.cs:68` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Census

117 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:182` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:187` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:188` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Census/Program.cs:73` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:73` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Census/Program.cs:73` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:73` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Census/Program.cs:73` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:85` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Census/Program.cs:75` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:75` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:75` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:75` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:189` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Census/Program.cs:80` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:72` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:74` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Census/Program.cs:71` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### DataAcquisition

151 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `AcquisitionJobSettings` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:61` |
| `AcquisitionJobSettings:CronSchedule` | dotnet | yes | - | `DotNet/DataAcquisition/Program.cs:61` |
| `ApiSettings` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:135` |
| `ApiSettings:FhirListSettings:ValidStatuses:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:135` |
| `ApiSettings:FhirListSettings:ValidTimeFrames:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:135` |
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:101` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:106` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition/Program.cs:107` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `Cache:Type` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:101` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:164` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:114` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:48` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition/Program.cs:48` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:48` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:48` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:108` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:52` |
| `DataSourceAuth` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `DataSourceAuth:KeySource` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `KafkaConnection` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:158` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition/Program.cs:109` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:86` |
| `SecretManagement:Manager` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:136` |
| `SecretManagement:ManagerUri` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:86` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `SftpAcquisition` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:BaseRetryDelaySeconds` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:EnableParallelProcessing` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:JobIntervalSeconds` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:MaxBatchSize` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:MaxConcurrency` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:MaxRetryAttempts` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:MaxRetryDelaySeconds` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:ParallelProcessingThreshold` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpValidation` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `SftpValidation:Connection:MaxHostLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `SftpValidation:Connection:MaxRemoteDirectoryLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `SftpValidation:Connection:MaxTimeoutMinutes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `SftpValidation:FileName:AllowedExtensions:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `SftpValidation:FileName:MaxLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `TailMessageRecoveryJobSettings` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `TailMessageRecoveryJobSettings:CronSchedule` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `TailMessageRecoveryJobSettings:MaxGroupsPerRun` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `TailMessageRecoveryJobSettings:MinAgeMinutes` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `TailMessageRecoveryJobSettings:TimeBudgetPerRunSeconds` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### DataAcquisitionWorker

149 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `AcquisitionWorkerProcessorSettings` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:MaxBatchesFailStalledPerRun` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:MaxBatchesPerFacilityPerRun` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:MaxConcurrentAcquisitions` | dotnet | yes | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:StalledProcessingThresholdMinutes` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:StalledQueuedThresholdMinutes` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:TimeBudgetPerRunSeconds` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:WorkChannelCapacity` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `ApiSettings` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:135` |
| `ApiSettings:FhirListSettings:ValidStatuses:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:135` |
| `ApiSettings:FhirListSettings:ValidTimeFrames:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:135` |
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:140` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:81` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `Cache:Type` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:101` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:164` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:114` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:98` |
| `DataSourceAuth` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `DataSourceAuth:KeySource` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `KafkaConnection` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:82` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:86` |
| `SecretManagement:Manager` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:136` |
| `SecretManagement:ManagerUri` | dotnet | yes | dev, qa, test | `DotNet/Admin.BFF/Program.cs:86` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:80` |
| `SftpAcquisition` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:BaseRetryDelaySeconds` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:EnableParallelProcessing` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:JobIntervalSeconds` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:MaxBatchSize` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:MaxConcurrency` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:MaxRetryAttempts` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:MaxRetryDelaySeconds` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpAcquisition:ParallelProcessingThreshold` | dotnet | - | dev, qa, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:138` |
| `SftpValidation` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `SftpValidation:Connection:MaxHostLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `SftpValidation:Connection:MaxRemoteDirectoryLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `SftpValidation:Connection:MaxTimeoutMinutes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `SftpValidation:FileName:AllowedExtensions:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `SftpValidation:FileName:MaxLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### MeasureEval

22 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `authentication.admin-email` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.anonymous` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.authority` | java | yes | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.signing-key` | java | yes | dev, qa, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `internal-blob-storage.blob-container-name` | java | - | dev, qa, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `internal-blob-storage.connection-string` | java | yes | dev, qa, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `link.cql-debug` | java | yes | - | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:24` |
| `link.info-route` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/BaseSpringConfig.java:23` |
| `link.report.base-url` | java | yes | dev, qa, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:42` |
| `link.reportability-predicate` | java | yes | - | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:24` |
| `loki.app` | java | yes | dev, qa, test | `Java/measureeval/src/main/resources/logback-spring.xml:3` |
| `loki.enabled` | java | - | dev, qa, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `loki.url` | java | - | dev, qa, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `management.health.resource-cache.timeout-ms` | java | - | - | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/health/ResourceCacheHealthIndicator.java:67` |
| `resource-cache.blob-storage.blob-container-name` | java | yes | dev, qa, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/CacheBlobStorageConfig.java:19` |
| `resource-cache.blob-storage.blob-root` | java | yes | dev, qa, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/CacheBlobStorageConfig.java:19` |
| `resource-cache.blob-storage.connection-string` | java | yes | dev, qa, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/CacheBlobStorageConfig.java:19` |
| `secret-management.key-vault-uri` | java | - | dev, qa, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/SecretManagementConfig.java:7` |
| `service-information.service-name` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/ServiceInformationConfig.java:12` |
| `spring.kafka.retry.max-attempts` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `spring.kafka.retry.retry-backoff-ms` | java | - | dev, qa, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `telemetry.exporter-endpoint` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/TelemetryConfig.java:7` |

### Normalization

118 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:118` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:123` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:124` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Normalization/Program.cs:75` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:75` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:75` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:75` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:75` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:75` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:75` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:75` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:75` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Normalization/Program.cs:75` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:75` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Normalization/Program.cs:75` |
| `CacheBlobStorage` | dotnet | - | - | `DotNet/Normalization/Program.cs:72` |
| `CacheBlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Normalization/Program.cs:72` |
| `CacheBlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Normalization/Program.cs:72` |
| `CacheBlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Normalization/Program.cs:72` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:138` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Normalization/Program.cs:68` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:125` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Normalization/Program.cs:133` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Normalization/Program.cs:74` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:74` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:74` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:74` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Normalization/Program.cs:74` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:74` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:74` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Normalization/Program.cs:74` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:74` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:74` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:74` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Normalization/Program.cs:76` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:76` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:76` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:76` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:76` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:76` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:76` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Normalization/Program.cs:73` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Notification

130 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:161` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:166` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:167` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:120` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Notification/Program.cs:120` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:120` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Notification/Program.cs:120` |
| `Channels` | dotnet | - | - | `DotNet/Notification/Program.cs:119` |
| `Channels:Email` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:119` |
| `Channels:IncludeTestMessage` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:119` |
| `Channels:SubjectTestMessage` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:119` |
| `Channels:TestMessage` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:119` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:184` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:168` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Notification/Program.cs:180` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Notification/Program.cs:56` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:116` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:121` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:266` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:115` |
| `SmtpConnection` | dotnet | - | - | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:ClientSecret` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:EmailFrom` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Host` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Password` | dotnet | - | - | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Port` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:TenantId` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:UseBasicAuth` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:UseOAuth2` | dotnet | - | dev, qa, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Username` | dotnet | yes | dev, qa, test | `DotNet/Notification/Program.cs:118` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### QueryDispatch

114 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:61` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:66` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:67` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:57` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:114` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:75` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:68` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `KafkaConnection` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:54` |
| `LinkTokenService` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:58` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/QueryDispatch/Program.cs:56` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Report

129 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `/pre-qualification/write-pre-qual-operation-outcome` | dotnet | - | dev, qa, test | `DotNet/Report/Application/Configs/PreQualificationFlagConsistency.cs:41` |
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:164` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:169` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:170` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Report/Program.cs:95` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:95` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Report/Program.cs:95` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:95` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Report/Program.cs:95` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:103` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Report/Program.cs:94` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:94` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:94` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:94` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:171` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `EnhancedQueryLoggingSettings` | dotnet | - | - | `DotNet/Report/Program.cs:107` |
| `EnhancedQueryLoggingSettings:EnableEnhancedQueryLogging` | dotnet | yes | test | `DotNet/Report/Program.cs:107` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `InternalBlobStorage` | dotnet | - | - | `DotNet/Report/Program.cs:97` |
| `InternalBlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:97` |
| `InternalBlobStorage:BlobRoot` | dotnet | yes | - | `DotNet/Report/Program.cs:97` |
| `InternalBlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:97` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:92` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:96` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `PatientAggregator` | dotnet | - | - | `DotNet/Report/Program.cs:98` |
| `PatientAggregator:IncludeOrganizationResource` | dotnet | yes | dev, test | `DotNet/Report/Program.cs:98` |
| `PreQualification` | dotnet | - | - | `DotNet/Report/Program.cs:99` |
| `PreQualification:WritePreQualOperationOutcome` | dotnet | yes | test | `DotNet/Report/Program.cs:99` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Report/Program.cs:88` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Report/Program.cs:91` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Submission

137 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:87` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:92` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:93` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:78` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Submission/Program.cs:78` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:78` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Submission/Program.cs:78` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:144` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Submission/Program.cs:77` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:77` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:77` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:77` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:94` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalBlobStorage` | dotnet | - | - | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:FlattenHierarchy` | dotnet | yes | test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:MeasurePrefixesByReportType` | dotnet | yes | test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:MeasurePrefixesByReportType:{Placeholder}` | dotnet | - | - | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:SuppressManifest` | dotnet | yes | test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:UseMeasurePrefix` | dotnet | yes | test | `DotNet/Submission/Program.cs:81` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `Features:DownloadReportEnabled` | dotnet | - | dev, qa, test | `DotNet/Submission/Application/Middleware/ConditionalEndpoint.cs:32` |
| `InternalBlobStorage` | dotnet | - | - | `DotNet/Submission/Program.cs:80` |
| `InternalBlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:80` |
| `InternalBlobStorage:BlobRoot` | dotnet | yes | - | `DotNet/Submission/Program.cs:80` |
| `InternalBlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:80` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:75` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:79` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:74` |
| `SubmissionServiceConfig` | dotnet | - | - | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:MeasureNames:0:MeasureId` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:MeasureNames:0:ShortName` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:MeasureNames:0:Url` | dotnet | - | dev, qa, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:PatientBundleBatchSize` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:SubmissionDirectory` | dotnet | yes | dev, qa, test | `DotNet/Submission/Program.cs:76` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Tenant

117 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:65` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:70` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:71` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Tenant/Program.cs:92` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:92` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:92` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:92` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:92` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:92` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:92` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:92` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:92` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Tenant/Program.cs:92` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:92` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Tenant/Program.cs:92` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:113` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:72` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Tenant/Program.cs:108` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `FacilityIdSettings` | dotnet | - | - | `DotNet/Tenant/Program.cs:84` |
| `FacilityIdSettings:NumericOnlyFacilityId` | dotnet | yes | - | `DotNet/Tenant/Program.cs:84` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Tenant/Program.cs:90` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:90` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:90` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:90` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Tenant/Program.cs:90` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:90` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:90` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Tenant/Program.cs:90` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:90` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:90` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:90` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Tenant/Program.cs:93` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:93` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:93` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:93` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:93` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:93` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:93` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `MeasureConfig` | dotnet | - | - | `DotNet/Tenant/Program.cs:88` |
| `MeasureConfig:CheckIfMeasureExists` | dotnet | - | - | `DotNet/Tenant/Program.cs:88` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, test | `DotNet/Tenant/Program.cs:89` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Terminology

55 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, test | `DotNet/Terminology/Program.cs:40` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, test | `DotNet/Terminology/Program.cs:45` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, test | `DotNet/Terminology/Program.cs:46` |
| `AutoMigrate` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, test | `DotNet/Terminology/Program.cs:47` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:41` |
| `EnableSwagger` | dotnet | - | dev, qa, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:26` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, test | `DotNet/Terminology/Program.cs:48` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Terminology/Program.cs:59` |
| `Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:53` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Terminology` | dotnet | - | - | `DotNet/Terminology/Program.cs:128` |
| `Terminology:Path` | dotnet | yes | - | `DotNet/Terminology/Program.cs:128` |

### Validation

25 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `authentication.admin-email` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.anonymous` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.authority` | java | yes | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.signing-key` | java | yes | dev, qa, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `cache.type` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/CacheConfig.java:9` |
| `cache.validate-code.ttl` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/CacheConfig.java:9` |
| `internal-blob-storage.blob-container-name` | java | - | dev, qa, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `internal-blob-storage.connection-string` | java | yes | dev, qa, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `link.fhir-client-retry.backoff-millis` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/FhirConfig.java:36` |
| `link.fhir-client-retry.max-attempts` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/FhirConfig.java:35` |
| `link.fhir-terminology-service-url` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/LinkConfig.java:16` |
| `link.info-route` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/BaseSpringConfig.java:23` |
| `link.report.base-url` | java | yes | dev, qa, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:42` |
| `link.terminology-service-url` | java | yes | dev, qa, test | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/LinkConfig.java:16` |
| `loki.app` | java | yes | dev, qa, test | `Java/validation/src/main/resources/logback-spring.xml:3` |
| `loki.enabled` | java | - | dev, qa, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `loki.url` | java | - | dev, qa, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `management.health.redis.timeout-ms` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/health/RedisHealthIndicator.java:56` |
| `pre-qualification.write-expressions-in-operation-outcome` | java | yes | dev, qa, test | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/PreQualificationConfig.java:16` |
| `pre-qualification.write-pre-qual-operation-outcome` | java | yes | dev, qa, test | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/PreQualificationConfig.java:16` |
| `secret-management.key-vault-uri` | java | - | dev, qa, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/SecretManagementConfig.java:7` |
| `service-information.service-name` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/ServiceInformationConfig.java:12` |
| `spring.kafka.retry.max-attempts` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `spring.kafka.retry.retry-backoff-ms` | java | - | dev, qa, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `telemetry.exporter-endpoint` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/TelemetryConfig.java:7` |
