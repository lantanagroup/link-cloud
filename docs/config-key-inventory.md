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
| `Telemetry:EnableOtelCollector` | `TelemetrySettings` | dev, qa, qa2, test |

## Keys by service

### (unattributed)

14 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `MockDmrpApi` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:26` |
| `MockDmrpApi:Audience` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:26` |
| `MockDmrpApi:AuthClientId` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:26` |
| `MockDmrpApi:AuthClientSecret` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:26` |
| `MockDmrpApi:Enabled` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:26` |
| `MockDmrpApi:Issuer` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:26` |
| `MockDmrpApi:SigningKey` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:26` |
| `MockDmrpApi:TokenLifetimeSeconds` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:26` |
| `MockFhirServer` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ClinicalPeriodEnd` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ClinicalPeriodStart` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:GenerationSeed` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:PreGeneratedPatientCount` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ResourcesPerPatient` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |

### Account

125 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:140` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:145` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:146` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllHeaders` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllMethods` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `Cache:Type` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:101` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:164` |
| `ConnectionStrings:Redis` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:114` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:147` |
| `DataProtection:KeyRing` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:98` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:77` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Logging:HmacKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:252` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Account/Program.cs:73` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `SecretManagement:Manager` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:136` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
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
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `UserManagement` | dotnet | - | - | `DotNet/Account/Program.cs:83` |
| `UserManagement:EnableAutomaticUserActivation` | dotnet | - | - | `DotNet/Account/Program.cs:83` |

### AdminBFF

151 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:DefaultChallengeScheme` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:26` |
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:36` |
| `Authentication:Schemas:Cookie:Domain` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:83` |
| `Authentication:Schemas:Cookie:HttpOnly` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:76` |
| `Authentication:Schemas:Cookie:Path` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:78` |
| `Authentication:Schemas:Jwt:Audience` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:189` |
| `Authentication:Schemas:Jwt:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:188` |
| `Authentication:Schemas:Jwt:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:179` |
| `Authentication:Schemas:Jwt:NameClaimType` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:191` |
| `Authentication:Schemas:Jwt:RequireHttpsMetadata` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:190` |
| `Authentication:Schemas:Jwt:RoleClaimType` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:192` |
| `Authentication:Schemas:Oauth2:CallbackPath` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:144` |
| `Authentication:Schemas:Oauth2:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:139` |
| `Authentication:Schemas:Oauth2:ClientSecret` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:140` |
| `Authentication:Schemas:Oauth2:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:130` |
| `Authentication:Schemas:Oauth2:Endpoints:Authorization` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:141` |
| `Authentication:Schemas:Oauth2:Endpoints:Token` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:142` |
| `Authentication:Schemas:Oauth2:Endpoints:UserInformation` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:143` |
| `Authentication:Schemas:OpenIdConnect:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:165` |
| `Authentication:Schemas:OpenIdConnect:CallbackPath` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:168` |
| `Authentication:Schemas:OpenIdConnect:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:166` |
| `Authentication:Schemas:OpenIdConnect:ClientSecret` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:167` |
| `Authentication:Schemas:OpenIdConnect:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:156` |
| `Authentication:Schemas:OpenIdConnect:NameClaimType` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:169` |
| `Authentication:Schemas:OpenIdConnect:RoleClaimType` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:170` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:199` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:199` |
| `Cache` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:ConnectionString` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:InstanceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:Password` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:Timeout` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:Type` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:90` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:Redis` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:141` |
| `DataProtection` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:87` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:87` |
| `DataProtection:KeyRing` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:87` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableIntegrationFeature` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:237` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:96` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:207` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:210` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:89` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Logging:HmacKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:323` |
| `MonitorBackendHealthChecks` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:243` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:82` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:147` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ReverseProxy` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/YarpProxyExtensioncs.cs:15` |
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:86` |
| `SecretManagement:Manager` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:165` |
| `SecretManagement:ManagerUri` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:86` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
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
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:88` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Audit

123 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:167` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:172` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:173` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowAllHeaders` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowAllMethods` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:72` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Audit/Program.cs:72` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:72` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Audit/Program.cs:72` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:107` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Audit/Persistance/AuditDbContext.cs:41` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Audit/Program.cs:71` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:71` |
| `ConsumerSettings:DisableConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:71` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:71` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:174` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Audit/Program.cs:102` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:69` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:73` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Logging:HmacKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:197` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Audit/Program.cs:65` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
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
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Audit/Program.cs:70` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### AutomationUI

146 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `ApiHealth:EnableAdminBffAuthSuite` | dotnet | - | test | `DotNet/Automation.UI/Program.cs:261` |
| `Authentication:ApiBearer:Audience` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:103` |
| `Authentication:ApiBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:102` |
| `Authentication:ApiBearer:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:101` |
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:95` |
| `Authentication:UseBearerForServiceCalls` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:76` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `Automation` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:DownloadPath` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FacilityFhirServerBase` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirGeneration:IncludeLowValueOptionalReferences` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirGeneration:MaxConcurrentPatients` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirGeneration:ResourceDistribution` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirGeneration:ResourceDistribution:{Placeholder}` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirQuery:MaxAcquisitionPullTime` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirQuery:MaxConcurrentRequests` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirQuery:MinAcquisitionPullTime` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirQuery:TimeZone` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirServerBase` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirServerBasicAuth:Password` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirServerBasicAuth:ShouldAuthenticate` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirServerBasicAuth:Username` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirServerOAuth:ClientId` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirServerOAuth:ClientSecret` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirServerOAuth:Password` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirServerOAuth:Scope` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirServerOAuth:ShouldAuthenticate` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirServerOAuth:TokenEndpoint` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:FhirServerOAuth:Username` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:Kafka:RestProxyBaseUrl` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:LokiAppLabel` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `Automation:LokiBaseUrl` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:40` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `Dashboard:SeedFakeRuns` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:306` |
| `DataProtection:ApplicationName` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:211` |
| `DataProtection:KeyCollectionName` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:213` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalBlobStorage:SuppressManifest` | dotnet | - | test | `DotNet/Automation.UI/Services/RunExecutor.cs:90` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `InternalBlobStorage` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:41` |
| `InternalBlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:41` |
| `InternalBlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:41` |
| `InternalBlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:41` |
| `InternalBlobStorage:GeneratedTemplateBlobRoot` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:41` |
| `InternalBlobStorage:SnapshotPayloadBlobRoot` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:41` |
| `InternalBlobStorage:SnapshotPayloadExternalizedDomains:0` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:41` |
| `InternalBlobStorage:SnapshotPayloadInlineMaxBytes` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:41` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:61` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:61` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:61` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:61` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:61` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:61` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:61` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:61` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:61` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:61` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:61` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:70` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:70` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:70` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:70` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:70` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:70` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:70` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Loki:App` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:49` |
| `Loki:Url` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:43` |
| `MongoDB:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:181` |
| `MongoDB:DatabaseName` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:182` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:33` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Automation.UI/Program.cs:69` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Census

120 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:182` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:187` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:188` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Census/Program.cs:73` |
| `CORS:AllowAllHeaders` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowAllMethods` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:73` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Census/Program.cs:73` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:73` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Census/Program.cs:73` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:85` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Census/Program.cs:75` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:75` |
| `ConsumerSettings:DisableConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:75` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:75` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:189` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Census/Program.cs:80` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:72` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:74` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
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
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Census/Program.cs:71` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### DataAcquisition

154 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `AcquisitionJobSettings` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:61` |
| `AcquisitionJobSettings:CronSchedule` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:61` |
| `ApiSettings` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `ApiSettings:FhirListSettings:ValidStatuses:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `ApiSettings:FhirListSettings:ValidTimeFrames:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:101` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:106` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:107` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllHeaders` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllMethods` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `Cache:Type` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:101` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:164` |
| `ConnectionStrings:Redis` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:114` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:48` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:48` |
| `ConsumerSettings:DisableConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:48` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:48` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:108` |
| `DataProtection:KeyRing` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:52` |
| `DataSourceAuth` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:141` |
| `DataSourceAuth:KeySource` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:141` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `KafkaConnection` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:158` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:158` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition/Program.cs:109` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:86` |
| `SecretManagement:Manager` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:136` |
| `SecretManagement:ManagerUri` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:86` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
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
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `SftpAcquisition` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:BaseRetryDelaySeconds` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:EnableParallelProcessing` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:JobIntervalSeconds` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:MaxBatchSize` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:MaxConcurrency` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:MaxRetryAttempts` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:MaxRetryDelaySeconds` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:ParallelProcessingThreshold` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpValidation` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `SftpValidation:Connection:MaxHostLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `SftpValidation:Connection:MaxRemoteDirectoryLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `SftpValidation:Connection:MaxTimeoutMinutes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `SftpValidation:FileName:AllowedExtensions:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `SftpValidation:FileName:MaxLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `TailMessageRecoveryJobSettings` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `TailMessageRecoveryJobSettings:CronSchedule` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `TailMessageRecoveryJobSettings:MaxGroupsPerRun` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `TailMessageRecoveryJobSettings:MinAgeMinutes` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `TailMessageRecoveryJobSettings:TimeBudgetPerRunSeconds` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### DataAcquisitionWorker

155 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `AcquisitionWorkerProcessorSettings` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:MaxBatchesFailStalledPerRun` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:MaxBatchesPerFacilityPerRun` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:MaxConcurrentAcquisitions` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:StalledProcessingThresholdMinutes` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:StalledQueuedThresholdMinutes` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:TimeBudgetPerRunSeconds` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:WorkChannelCapacity` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `ApiSettings` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `ApiSettings:FhirListSettings:ValidStatuses:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `ApiSettings:FhirListSettings:ValidTimeFrames:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:137` |
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:42` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:47` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:48` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllHeaders` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllMethods` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:81` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `Cache:Type` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:101` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:164` |
| `ConnectionStrings:Redis` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:114` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:DisableConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `DataProtection:KeyRing` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:98` |
| `DataSourceAuth` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:141` |
| `DataSourceAuth:KeySource` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:141` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `KafkaConnection` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:50` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:82` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:86` |
| `SecretManagement:Manager` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:136` |
| `SecretManagement:ManagerUri` | dotnet | - | dev, qa, qa2, test | `DotNet/Admin.BFF/Program.cs:86` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
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
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:80` |
| `SftpAcquisition` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:BaseRetryDelaySeconds` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:EnableParallelProcessing` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:JobIntervalSeconds` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:MaxBatchSize` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:MaxConcurrency` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:MaxRetryAttempts` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:MaxRetryDelaySeconds` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:ParallelProcessingThreshold` | dotnet | - | dev, qa, qa2, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpValidation` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `SftpValidation:Connection:MaxHostLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `SftpValidation:Connection:MaxRemoteDirectoryLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `SftpValidation:Connection:MaxTimeoutMinutes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `SftpValidation:FileName:AllowedExtensions:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `SftpValidation:FileName:MaxLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:139` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### MeasureEval

22 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `authentication.admin-email` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.anonymous` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.authority` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.signing-key` | java | - | dev, qa, qa2, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `internal-blob-storage.blob-container-name` | java | - | dev, qa, qa2, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `internal-blob-storage.connection-string` | java | - | dev, qa, qa2, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `link.cql-debug` | java | - | - | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:24` |
| `link.info-route` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/BaseSpringConfig.java:23` |
| `link.report.base-url` | java | - | dev, qa, qa2, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:42` |
| `link.reportability-predicate` | java | - | - | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:24` |
| `loki.app` | java | - | dev, qa, qa2, test | `Java/measureeval/src/main/resources/logback-spring.xml:3` |
| `loki.enabled` | java | - | dev, qa, qa2, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `loki.url` | java | - | dev, qa, qa2, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `management.health.resource-cache.timeout-ms` | java | - | - | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/health/ResourceCacheHealthIndicator.java:67` |
| `resource-cache.blob-storage.blob-container-name` | java | - | dev, qa, qa2, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/CacheBlobStorageConfig.java:19` |
| `resource-cache.blob-storage.blob-root` | java | - | dev, qa, qa2, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/CacheBlobStorageConfig.java:19` |
| `resource-cache.blob-storage.connection-string` | java | - | dev, qa, qa2, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/CacheBlobStorageConfig.java:19` |
| `secret-management.key-vault-uri` | java | - | dev, qa, qa2, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/SecretManagementConfig.java:7` |
| `service-information.service-name` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/ServiceInformationConfig.java:12` |
| `spring.kafka.retry.max-attempts` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `spring.kafka.retry.retry-backoff-ms` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `telemetry.exporter-endpoint` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/TelemetryConfig.java:7` |

### Normalization

121 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:127` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:132` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:133` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Normalization/Program.cs:76` |
| `CORS:AllowAllHeaders` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:76` |
| `CORS:AllowAllMethods` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:76` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:76` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:76` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:76` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:76` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:76` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:76` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Normalization/Program.cs:76` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:76` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Normalization/Program.cs:76` |
| `CacheBlobStorage` | dotnet | - | - | `DotNet/Normalization/Program.cs:73` |
| `CacheBlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Normalization/Program.cs:73` |
| `CacheBlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Normalization/Program.cs:73` |
| `CacheBlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Normalization/Program.cs:73` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:147` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Normalization/Program.cs:69` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:134` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Normalization/Program.cs:142` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Normalization/Program.cs:75` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:75` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:75` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:75` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Normalization/Program.cs:75` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:75` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:75` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Normalization/Program.cs:75` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:75` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:75` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:75` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Normalization/Program.cs:77` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:77` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:77` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:77` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:77` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:77` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:77` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Normalization/Program.cs:74` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Notification

133 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:161` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:166` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:167` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowAllHeaders` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowAllMethods` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:120` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Notification/Program.cs:120` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:120` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Notification/Program.cs:120` |
| `Channels` | dotnet | - | - | `DotNet/Notification/Program.cs:119` |
| `Channels:Email` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:119` |
| `Channels:IncludeTestMessage` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:119` |
| `Channels:SubjectTestMessage` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:119` |
| `Channels:TestMessage` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:119` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:184` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:168` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Notification/Program.cs:180` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Notification/Program.cs:56` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:116` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:121` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Logging:HmacKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:266` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
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
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:115` |
| `SmtpConnection` | dotnet | - | - | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:ClientSecret` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:EmailFrom` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Host` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Password` | dotnet | - | - | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Port` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:TenantId` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:UseBasicAuth` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:UseOAuth2` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Username` | dotnet | - | dev, qa, qa2, test | `DotNet/Notification/Program.cs:118` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### QueryDispatch

117 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:61` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:66` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:67` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowAllHeaders` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowAllMethods` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:57` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:114` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:75` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:68` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `KafkaConnection` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:54` |
| `LinkTokenService` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:58` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
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
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/QueryDispatch/Program.cs:56` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Report

129 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:161` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:166` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:167` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Report/Program.cs:95` |
| `CORS:AllowAllHeaders` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowAllMethods` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:95` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:95` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Report/Program.cs:95` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:95` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Report/Program.cs:95` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:100` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Report/Program.cs:94` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:94` |
| `ConsumerSettings:DisableConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:94` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:94` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:168` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `EnhancedQueryLoggingSettings` | dotnet | - | - | `DotNet/Report/Program.cs:104` |
| `EnhancedQueryLoggingSettings:EnableEnhancedQueryLogging` | dotnet | - | test | `DotNet/Report/Program.cs:104` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `InternalBlobStorage` | dotnet | - | - | `DotNet/Report/Program.cs:97` |
| `InternalBlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:97` |
| `InternalBlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Report/Program.cs:97` |
| `InternalBlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:97` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:92` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:92` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:96` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:96` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `PatientAggregator` | dotnet | - | - | `DotNet/Report/Program.cs:98` |
| `PatientAggregator:IncludeOrganizationResource` | dotnet | - | dev, test | `DotNet/Report/Program.cs:98` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Report/Program.cs:88` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
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
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Report/Program.cs:91` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Submission

140 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:87` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:92` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:93` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowAllHeaders` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowAllMethods` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:78` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Submission/Program.cs:78` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:78` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Submission/Program.cs:78` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:144` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Submission/Program.cs:77` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:77` |
| `ConsumerSettings:DisableConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:77` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:77` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:94` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalBlobStorage` | dotnet | - | - | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:FlattenHierarchy` | dotnet | - | test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:MeasurePrefixesByReportType` | dotnet | - | test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:MeasurePrefixesByReportType:{Placeholder}` | dotnet | - | - | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:SuppressManifest` | dotnet | - | test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:UseMeasurePrefix` | dotnet | - | test | `DotNet/Submission/Program.cs:81` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `Features:DownloadReportEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Application/Middleware/ConditionalEndpoint.cs:32` |
| `InternalBlobStorage` | dotnet | - | - | `DotNet/Submission/Program.cs:80` |
| `InternalBlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:80` |
| `InternalBlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Submission/Program.cs:80` |
| `InternalBlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:80` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:75` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:79` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
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
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:74` |
| `SubmissionServiceConfig` | dotnet | - | - | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:MeasureNames:0:MeasureId` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:MeasureNames:0:ShortName` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:MeasureNames:0:Url` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:PatientBundleBatchSize` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:SubmissionDirectory` | dotnet | - | dev, qa, qa2, test | `DotNet/Submission/Program.cs:76` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Tenant

122 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:69` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:74` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:75` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Tenant/Program.cs:96` |
| `CORS:AllowAllHeaders` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:96` |
| `CORS:AllowAllMethods` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:96` |
| `CORS:AllowAllOrigins` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:96` |
| `CORS:AllowCredentials` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:96` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:96` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:96` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:96` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:96` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Tenant/Program.cs:96` |
| `CORS:MaxAge` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:96` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Tenant/Program.cs:96` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `ConnectionStrings:DatabaseConnection` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:117` |
| `DMRP` | dotnet | - | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:42` |
| `DMRP:Enabled` | dotnet | - | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:43` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:76` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Tenant/Program.cs:112` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `FacilityIdSettings` | dotnet | - | - | `DotNet/Tenant/Program.cs:88` |
| `FacilityIdSettings:NumericOnlyFacilityId` | dotnet | - | - | `DotNet/Tenant/Program.cs:88` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Tenant/Program.cs:94` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:94` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:94` |
| `KafkaConnection:ClientId` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:94` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Tenant/Program.cs:94` |
| `KafkaConnection:Mechanism` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:94` |
| `KafkaConnection:Protocol` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:94` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Tenant/Program.cs:94` |
| `KafkaConnection:SaslPassword` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:94` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:94` |
| `KafkaConnection:SaslUsername` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:94` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Tenant/Program.cs:97` |
| `LinkTokenService:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:97` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:97` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:97` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:97` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:97` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:97` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `MeasureConfig` | dotnet | - | - | `DotNet/Tenant/Program.cs:92` |
| `MeasureConfig:CheckIfMeasureExists` | dotnet | - | - | `DotNet/Tenant/Program.cs:92` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | - | dev, qa, qa2, test | `DotNet/Tenant/Program.cs:93` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |

### Terminology

59 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | - | dev, qa, qa2, test | `DotNet/Terminology/Program.cs:40` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | - | dev, qa, qa2, test | `DotNet/Terminology/Program.cs:45` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, test | `DotNet/Terminology/Program.cs:46` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:62` |
| `DataProtection:Enabled` | dotnet | - | dev, qa, qa2, test | `DotNet/Terminology/Program.cs:47` |
| `DatabaseProvider` | dotnet | - | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:27` |
| `LinkTokenService:SigningKey` | dotnet | - | dev, qa, qa2, test | `DotNet/Terminology/Program.cs:48` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:20` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Terminology/Program.cs:60` |
| `Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Account/Program.cs:120` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
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
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTelemetry` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:31` |
| `Terminology` | dotnet | - | - | `DotNet/Terminology/Program.cs:129` |
| `Terminology:EnableCodeUploadEndpoint` | dotnet | - | - | `DotNet/Terminology/Program.cs:129` |
| `Terminology:Path` | dotnet | - | - | `DotNet/Terminology/Program.cs:129` |

### Validation

25 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `authentication.admin-email` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.anonymous` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.authority` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.signing-key` | java | - | dev, qa, qa2, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `cache.type` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/CacheConfig.java:9` |
| `cache.validate-code.ttl` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/CacheConfig.java:9` |
| `internal-blob-storage.blob-container-name` | java | - | dev, qa, qa2, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `internal-blob-storage.connection-string` | java | - | dev, qa, qa2, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `link.fhir-client-retry.backoff-millis` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/FhirConfig.java:36` |
| `link.fhir-client-retry.max-attempts` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/FhirConfig.java:35` |
| `link.fhir-terminology-service-url` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/LinkConfig.java:16` |
| `link.info-route` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/BaseSpringConfig.java:23` |
| `link.report.base-url` | java | - | dev, qa, qa2, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:42` |
| `link.terminology-service-url` | java | - | dev, qa, qa2, test | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/LinkConfig.java:16` |
| `loki.app` | java | - | dev, qa, qa2, test | `Java/validation/src/main/resources/logback-spring.xml:3` |
| `loki.enabled` | java | - | dev, qa, qa2, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `loki.url` | java | - | dev, qa, qa2, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `management.health.redis.timeout-ms` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/health/RedisHealthIndicator.java:56` |
| `pre-qualification.write-expressions-in-operation-outcome` | java | - | dev, qa, qa2, test | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/PreQualificationConfig.java:16` |
| `pre-qualification.write-pre-qual-operation-outcome` | java | - | dev, qa, qa2, test | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/PreQualificationConfig.java:16` |
| `secret-management.key-vault-uri` | java | - | dev, qa, qa2, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/SecretManagementConfig.java:7` |
| `service-information.service-name` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/ServiceInformationConfig.java:12` |
| `spring.kafka.retry.max-attempts` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `spring.kafka.retry.retry-backoff-ms` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `telemetry.exporter-endpoint` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/TelemetryConfig.java:7` |
