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
| `Telemetry:EnableOtelCollector` | `TelemetrySettings` | dev, qa, qa2, qa3, test |

## Keys by service

### (unattributed)

7 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `MockDmrpApi` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:28` |
| `MockFhirServer` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ClinicalPeriodEnd` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ClinicalPeriodStart` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:GenerationSeed` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:PreGeneratedPatientCount` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ResourcesPerPatient` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |

### Account

127 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:139` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:144` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:145` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `Cache:Type` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:100` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:163` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:146` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:97` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:158` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:251` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Account/Program.cs:72` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `SecretManagement:Manager` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:135` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `UserManagement` | dotnet | - | - | `DotNet/Account/Program.cs:82` |
| `UserManagement:EnableAutomaticUserActivation` | dotnet | yes | - | `DotNet/Account/Program.cs:82` |

### AdminBFF

153 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:DefaultChallengeScheme` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:26` |
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:36` |
| `Authentication:Schemas:Cookie:Domain` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:83` |
| `Authentication:Schemas:Cookie:HttpOnly` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:76` |
| `Authentication:Schemas:Cookie:Path` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:78` |
| `Authentication:Schemas:Jwt:Audience` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:189` |
| `Authentication:Schemas:Jwt:Authority` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:188` |
| `Authentication:Schemas:Jwt:Enabled` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:179` |
| `Authentication:Schemas:Jwt:NameClaimType` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:191` |
| `Authentication:Schemas:Jwt:RequireHttpsMetadata` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:190` |
| `Authentication:Schemas:Jwt:RoleClaimType` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:192` |
| `Authentication:Schemas:Oauth2:CallbackPath` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:144` |
| `Authentication:Schemas:Oauth2:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:139` |
| `Authentication:Schemas:Oauth2:ClientSecret` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:140` |
| `Authentication:Schemas:Oauth2:Enabled` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:130` |
| `Authentication:Schemas:Oauth2:Endpoints:Authorization` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:141` |
| `Authentication:Schemas:Oauth2:Endpoints:Token` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:142` |
| `Authentication:Schemas:Oauth2:Endpoints:UserInformation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:143` |
| `Authentication:Schemas:OpenIdConnect:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:165` |
| `Authentication:Schemas:OpenIdConnect:CallbackPath` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:168` |
| `Authentication:Schemas:OpenIdConnect:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:166` |
| `Authentication:Schemas:OpenIdConnect:ClientSecret` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:167` |
| `Authentication:Schemas:OpenIdConnect:Enabled` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:156` |
| `Authentication:Schemas:OpenIdConnect:NameClaimType` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:169` |
| `Authentication:Schemas:OpenIdConnect:RoleClaimType` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:170` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:201` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:201` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:201` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:201` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:201` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:201` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:201` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:201` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:201` |
| `Cache` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:ConnectionString` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:InstanceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:Password` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:Timeout` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `Cache:Type` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:90` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:141` |
| `DataProtection` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:87` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:87` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:87` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:158` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableIntegrationFeature` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:239` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:96` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:96` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:207` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:89` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:210` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:89` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:325` |
| `MonitorBackendHealthChecks` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:245` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:82` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:147` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ReverseProxy` | dotnet | - | - | `DotNet/Admin.BFF/Infrastructure/Extensions/YarpProxyExtensioncs.cs:15` |
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:86` |
| `SecretManagement:Manager` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:167` |
| `SecretManagement:ManagerUri` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:86` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:88` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### Audit

126 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:166` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:171` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:172` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Audit/Program.cs:71` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Audit/Program.cs:71` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Audit/Program.cs:71` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:106` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Audit/Persistance/AuditDbContext.cs:41` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Audit/Program.cs:70` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:173` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Audit/Program.cs:101` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Audit/Program.cs:68` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:68` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:68` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:68` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Audit/Program.cs:68` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:68` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:68` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Audit/Program.cs:68` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:68` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:68` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:68` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Audit/Program.cs:72` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:196` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Audit/Program.cs:64` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### AutomationUI

169 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `ApiHealth:EnableAdminBffAuthSuite` | dotnet | yes | test | `DotNet/Automation.UI/Program.cs:282` |
| `Authentication:ApiBearer:Audience` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:114` |
| `Authentication:ApiBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:113` |
| `Authentication:ApiBearer:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:112` |
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:106` |
| `Authentication:UseBearerForServiceCalls` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:87` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `Automation` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:DownloadPath` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FacilityFhirServerBase` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirGeneration:IncludeLowValueOptionalReferences` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirGeneration:MaxConcurrentPatients` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirGeneration:ResourceDistribution` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirGeneration:ResourceDistribution:{Placeholder}` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirQuery:MaxAcquisitionPullTime` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirQuery:MaxConcurrentRequests` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirQuery:MinAcquisitionPullTime` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirQuery:TimeZone` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirServerBase` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirServerBasicAuth:Password` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirServerBasicAuth:ShouldAuthenticate` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirServerBasicAuth:Username` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirServerOAuth:ClientId` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirServerOAuth:ClientSecret` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirServerOAuth:Password` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirServerOAuth:Scope` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirServerOAuth:ShouldAuthenticate` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirServerOAuth:TokenEndpoint` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:FhirServerOAuth:Username` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:Kafka:RestProxyBaseUrl` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:LokiAppLabel` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:LokiBaseUrl` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:49` |
| `Automation:TestRail` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:300` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `Dashboard:SeedFakeRuns` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:345` |
| `DataProtection:ApplicationName` | dotnet | yes | dev | `DotNet/Automation.UI/Program.cs:222` |
| `DataProtection:KeyCollectionName` | dotnet | yes | - | `DotNet/Automation.UI/Program.cs:224` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:158` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalBlobStorage:SuppressManifest` | dotnet | yes | test | `DotNet/Automation.UI/Services/RunExecutor.cs:112` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `FacilityIdSettings` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:50` |
| `FacilityIdSettings:NumericOnlyFacilityId` | dotnet | yes | - | `DotNet/Automation.UI/Program.cs:50` |
| `InternalBlobStorage` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:52` |
| `InternalBlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:52` |
| `InternalBlobStorage:BlobRoot` | dotnet | yes | - | `DotNet/Automation.UI/Program.cs:52` |
| `InternalBlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:52` |
| `InternalBlobStorage:GeneratedTemplateBlobRoot` | dotnet | yes | - | `DotNet/Automation.UI/Program.cs:52` |
| `InternalBlobStorage:SnapshotPayloadBlobRoot` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:52` |
| `InternalBlobStorage:SnapshotPayloadExternalizedDomains:0` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:52` |
| `InternalBlobStorage:SnapshotPayloadInlineMaxBytes` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:52` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:72` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:72` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:72` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:72` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:72` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:72` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:72` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:72` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:72` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:72` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:72` |
| `LeftoverRunCleanup` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:AbortTtl` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:CatchUpWindow` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:DailyTeardownEnabled` | dotnet | yes | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:DailyTeardownTimeUtc` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:Enabled` | dotnet | yes | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:Interval` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:MaxFacilitiesPerPass` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:QuiesceEnabled` | dotnet | yes | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:QuiesceGrace` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:StartupDelay` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:TeardownRetention` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:WeeklyHistoryPurgeDay` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:WeeklyHistoryPurgeEnabled` | dotnet | yes | - | `DotNet/Automation.UI/Program.cs:51` |
| `LeftoverRunCleanup:WeeklyHistoryPurgeTimeUtc` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:51` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:81` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:81` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:81` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:81` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:81` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:81` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:81` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Loki:App` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:60` |
| `Loki:Url` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:54` |
| `MongoDB:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:192` |
| `MongoDB:DatabaseName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:193` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceHealth:CheckIntervalSeconds` | dotnet | - | - | `DotNet/Automation.UI/Services/RunExecutor.cs:90` |
| `ServiceHealth:OutageThresholdSeconds` | dotnet | - | - | `DotNet/Automation.UI/Services/RunExecutor.cs:91` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:382` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:37` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Automation.UI/Program.cs:80` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### Census

123 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:181` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:186` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:187` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:84` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Census/Program.cs:74` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:74` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:74` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:74` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:188` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Census/Program.cs:79` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Census/Program.cs:71` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Census/Program.cs:71` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Census/Program.cs:71` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Census/Program.cs:73` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:70` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### DataAcquisition

158 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `AcquisitionJobSettings` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `AcquisitionJobSettings:CronSchedule` | dotnet | yes | - | `DotNet/DataAcquisition/Program.cs:62` |
| `ApiSettings` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `ApiSettings:FhirListSettings:ValidStatuses:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `ApiSettings:FhirListSettings:ValidTimeFrames:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:102` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:107` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:108` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `Cache:Type` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:100` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:163` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:49` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:49` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:49` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:49` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:109` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:53` |
| `DataSourceAuth` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:144` |
| `DataSourceAuth:KeySource` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:144` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:158` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `FhirSearch` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:145` |
| `FhirSearch:PageSize` | dotnet | yes | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:145` |
| `KafkaConnection` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:159` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:159` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:159` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:159` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:159` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:159` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:159` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:159` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:159` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:159` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:159` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:110` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:86` |
| `SecretManagement:Manager` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:135` |
| `SecretManagement:ManagerUri` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:86` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `SftpAcquisition` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:BaseRetryDelaySeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:AdtMaxMessageBytes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:EnableParallelProcessing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:JobIntervalSeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:MaxBatchSize` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:MaxConcurrency` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:MaxRetryAttempts` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:MaxRetryDelaySeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:ParallelProcessingThreshold` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpValidation` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `SftpValidation:Connection:MaxHostLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `SftpValidation:Connection:MaxRemoteDirectoryLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `SftpValidation:Connection:MaxTimeoutMinutes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `SftpValidation:FileName:AllowedExtensions:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `SftpValidation:FileName:MaxLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `TailMessageRecoveryJobSettings` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:63` |
| `TailMessageRecoveryJobSettings:CronSchedule` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:63` |
| `TailMessageRecoveryJobSettings:MaxGroupsPerRun` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:63` |
| `TailMessageRecoveryJobSettings:MinAgeMinutes` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:63` |
| `TailMessageRecoveryJobSettings:TimeBudgetPerRunSeconds` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:63` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### DataAcquisitionWorker

159 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `AcquisitionWorkerProcessorSettings` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:MaxBatchesFailStalledPerRun` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:MaxBatchesPerFacilityPerRun` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:MaxConcurrentAcquisitions` | dotnet | yes | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:StalledProcessingThresholdMinutes` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:StalledQueuedThresholdMinutes` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:TimeBudgetPerRunSeconds` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `AcquisitionWorkerProcessorSettings:WorkChannelCapacity` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:31` |
| `ApiSettings` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `ApiSettings:FhirListSettings:ValidStatuses:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `ApiSettings:FhirListSettings:ValidTimeFrames:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:42` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:47` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:48` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `Cache:Type` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:100` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:163` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:97` |
| `DataSourceAuth` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:144` |
| `DataSourceAuth:KeySource` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:144` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:158` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `FhirSearch` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:145` |
| `FhirSearch:PageSize` | dotnet | yes | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:145` |
| `KafkaConnection` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:61` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:50` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:86` |
| `SecretManagement:Manager` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:135` |
| `SecretManagement:ManagerUri` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:86` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:79` |
| `SftpAcquisition` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:AdtMaxMessageBytes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:140` |
| `SftpAcquisition:BaseRetryDelaySeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:EnableParallelProcessing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:JobIntervalSeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:MaxBatchSize` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:MaxConcurrency` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:MaxRetryAttempts` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:MaxRetryDelaySeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpAcquisition:ParallelProcessingThreshold` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:143` |
| `SftpValidation` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `SftpValidation:Connection:MaxHostLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `SftpValidation:Connection:MaxRemoteDirectoryLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `SftpValidation:Connection:MaxTimeoutMinutes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `SftpValidation:FileName:AllowedExtensions:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `SftpValidation:FileName:MaxLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:142` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### MeasureEval

22 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `authentication.admin-email` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.anonymous` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.authority` | java | yes | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.signing-key` | java | yes | dev, qa, qa2, qa3, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `internal-blob-storage.blob-container-name` | java | - | dev, qa, qa2, qa3, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `internal-blob-storage.connection-string` | java | yes | dev, qa, qa2, qa3, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `link.cql-debug` | java | yes | - | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:24` |
| `link.info-route` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/BaseSpringConfig.java:23` |
| `link.report.base-url` | java | yes | dev, qa, qa2, qa3, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:42` |
| `link.reportability-predicate` | java | yes | - | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:24` |
| `loki.app` | java | yes | dev, qa, qa2, qa3, test | `Java/measureeval/src/main/resources/logback-spring.xml:3` |
| `loki.enabled` | java | - | dev, qa, qa2, qa3, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `loki.url` | java | - | dev, qa, qa2, qa3, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `management.health.resource-cache.timeout-ms` | java | - | - | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/health/ResourceCacheHealthIndicator.java:67` |
| `resource-cache.blob-storage.blob-container-name` | java | yes | dev, qa, qa2, qa3, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/CacheBlobStorageConfig.java:19` |
| `resource-cache.blob-storage.blob-root` | java | yes | dev, qa, qa2, qa3, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/CacheBlobStorageConfig.java:19` |
| `resource-cache.blob-storage.connection-string` | java | yes | dev, qa, qa2, qa3, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/CacheBlobStorageConfig.java:19` |
| `secret-management.key-vault-uri` | java | - | dev, qa, qa2, qa3, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/SecretManagementConfig.java:7` |
| `service-information.service-name` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/ServiceInformationConfig.java:12` |
| `spring.kafka.retry.max-attempts` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `spring.kafka.retry.retry-backoff-ms` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `telemetry.exporter-endpoint` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/TelemetryConfig.java:7` |

### Normalization

124 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:133` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:138` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:139` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Normalization/Program.cs:77` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Normalization/Program.cs:77` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Normalization/Program.cs:77` |
| `CacheBlobStorage` | dotnet | - | - | `DotNet/Normalization/Program.cs:74` |
| `CacheBlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Normalization/Program.cs:74` |
| `CacheBlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Normalization/Program.cs:74` |
| `CacheBlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Normalization/Program.cs:74` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:153` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Normalization/Program.cs:70` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:140` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Normalization/Program.cs:148` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Normalization/Program.cs:76` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Normalization/Program.cs:76` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Normalization/Program.cs:76` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Normalization/Program.cs:78` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:75` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### Notification

136 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:160` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:165` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:166` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Notification/Program.cs:119` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Notification/Program.cs:119` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Notification/Program.cs:119` |
| `Channels` | dotnet | - | - | `DotNet/Notification/Program.cs:118` |
| `Channels:Email` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `Channels:IncludeTestMessage` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `Channels:SubjectTestMessage` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `Channels:TestMessage` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:183` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:167` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Notification/Program.cs:179` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Notification/Program.cs:55` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Notification/Program.cs:115` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Notification/Program.cs:115` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Notification/Program.cs:115` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Notification/Program.cs:120` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:265` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Notification/Program.cs:80` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Notification/Program.cs:80` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Notification/Program.cs:80` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Notification/Program.cs:80` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Notification/Program.cs:80` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Notification/Program.cs:80` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Notification/Program.cs:80` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Notification/Program.cs:80` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:114` |
| `SmtpConnection` | dotnet | - | - | `DotNet/Notification/Program.cs:117` |
| `SmtpConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:117` |
| `SmtpConnection:ClientSecret` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:117` |
| `SmtpConnection:EmailFrom` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:117` |
| `SmtpConnection:Host` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:117` |
| `SmtpConnection:Password` | dotnet | - | - | `DotNet/Notification/Program.cs:117` |
| `SmtpConnection:Port` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:117` |
| `SmtpConnection:TenantId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:117` |
| `SmtpConnection:UseBasicAuth` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:117` |
| `SmtpConnection:UseOAuth2` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:117` |
| `SmtpConnection:Username` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:117` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### QueryDispatch

120 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:61` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:66` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:67` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:57` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:114` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:75` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:68` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:158` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:54` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:54` |
| `LinkTokenService` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
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
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:56` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### Report

135 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:176` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:181` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:182` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Report/Program.cs:98` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:98` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:98` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:98` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:98` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:98` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:98` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:98` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:98` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Report/Program.cs:98` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:98` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Report/Program.cs:98` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:103` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Report/Program.cs:97` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:97` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:97` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:97` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:183` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:158` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `EnhancedQueryLoggingSettings` | dotnet | - | - | `DotNet/Report/Program.cs:107` |
| `EnhancedQueryLoggingSettings:EnableEnhancedQueryLogging` | dotnet | yes | test | `DotNet/Report/Program.cs:107` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `InternalBlobStorage` | dotnet | - | - | `DotNet/Report/Program.cs:100` |
| `InternalBlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:100` |
| `InternalBlobStorage:BlobRoot` | dotnet | yes | - | `DotNet/Report/Program.cs:100` |
| `InternalBlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:100` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Report/Program.cs:95` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:95` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:95` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:95` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Report/Program.cs:95` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:95` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:95` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Report/Program.cs:95` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:95` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:95` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:95` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Report/Program.cs:99` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `PatientAggregator` | dotnet | - | - | `DotNet/Report/Program.cs:101` |
| `PatientAggregator:IncludeOrganizationResource` | dotnet | yes | dev, test | `DotNet/Report/Program.cs:101` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Report/Program.cs:91` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `Retention:Schedule` | dotnet | - | - | `DotNet/Report/Application/Extensions/RetentionScheduleRegistration.cs:21` |
| `Retention:Schedule:Cron` | dotnet | yes | - | `DotNet/Report/Application/Extensions/RetentionScheduleRegistration.cs:21` |
| `Retention:Schedule:Enabled` | dotnet | yes | - | `DotNet/Report/Application/Extensions/RetentionScheduleRegistration.cs:21` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:94` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### Submission

143 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:86` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:91` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:92` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Submission/Program.cs:77` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Submission/Program.cs:77` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Submission/Program.cs:77` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:143` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Submission/Program.cs:76` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:76` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:76` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:76` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:93` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:158` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalBlobStorage` | dotnet | - | - | `DotNet/Submission/Program.cs:80` |
| `ExternalBlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:80` |
| `ExternalBlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Submission/Program.cs:80` |
| `ExternalBlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:80` |
| `ExternalBlobStorage:FlattenHierarchy` | dotnet | yes | test | `DotNet/Submission/Program.cs:80` |
| `ExternalBlobStorage:MeasurePrefixesByReportType` | dotnet | yes | test | `DotNet/Submission/Program.cs:80` |
| `ExternalBlobStorage:MeasurePrefixesByReportType:{Placeholder}` | dotnet | - | - | `DotNet/Submission/Program.cs:80` |
| `ExternalBlobStorage:SuppressManifest` | dotnet | yes | test | `DotNet/Submission/Program.cs:80` |
| `ExternalBlobStorage:UseMeasurePrefix` | dotnet | yes | test | `DotNet/Submission/Program.cs:80` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `Features:DownloadReportEnabled` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Application/Middleware/ConditionalEndpoint.cs:32` |
| `InternalBlobStorage` | dotnet | - | - | `DotNet/Submission/Program.cs:79` |
| `InternalBlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:79` |
| `InternalBlobStorage:BlobRoot` | dotnet | yes | - | `DotNet/Submission/Program.cs:79` |
| `InternalBlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:79` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Submission/Program.cs:74` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Submission/Program.cs:74` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Submission/Program.cs:74` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Submission/Program.cs:78` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:73` |
| `SubmissionServiceConfig` | dotnet | - | - | `DotNet/Submission/Program.cs:75` |
| `SubmissionServiceConfig:MeasureNames:0:MeasureId` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `SubmissionServiceConfig:MeasureNames:0:ShortName` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `SubmissionServiceConfig:MeasureNames:0:Url` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `SubmissionServiceConfig:PatientBundleBatchSize` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `SubmissionServiceConfig:SubmissionDirectory` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### Tenant

130 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:72` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:77` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:78` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Tenant/Program.cs:98` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:98` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:98` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:98` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:98` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:98` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:98` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:98` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:98` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Tenant/Program.cs:98` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:98` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Tenant/Program.cs:98` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:119` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `DMRP` | dotnet | - | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Api` | dotnet | - | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Scheduling:CatchUpNights` | dotnet | yes | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Scheduling:Concurrency` | dotnet | yes | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Scheduling:NightlyCron` | dotnet | yes | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Scheduling:ScheduledFireTimeOverride` | dotnet | yes | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:79` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Tenant/Program.cs:114` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `FacilityIdSettings` | dotnet | - | - | `DotNet/Tenant/Program.cs:90` |
| `FacilityIdSettings:NumericOnlyFacilityId` | dotnet | yes | - | `DotNet/Tenant/Program.cs:90` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Tenant/Program.cs:96` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Tenant/Program.cs:96` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Tenant/Program.cs:96` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Tenant/Program.cs:99` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `MeasureConfig` | dotnet | - | - | `DotNet/Tenant/Program.cs:94` |
| `MeasureConfig:CheckIfMeasureExists` | dotnet | - | - | `DotNet/Tenant/Program.cs:94` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:95` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |

### Terminology

74 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:41` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:46` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:47` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:63` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:113` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:48` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:158` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:76` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:49` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Terminology/Program.cs:61` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:119` |
| `ResourceCache` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:54` |
| `ResourceCache:BlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:BlobRoot` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:BlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:CacheImplementation` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:CacheEntryTtlDays` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MaxMemoryBytes` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:MemoryThresholdPercent` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ResourceCache:Redis:PoolSize` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:55` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:74` |
| `Telemetry` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:AzureMonitorConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableAzureMonitor` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableMetrics` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableOtelCollector` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableRuntimeInstrumentation` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTelemetry` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:EnableTracing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:InstrumentEntityFramework` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:MeterName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:OtelCollectorEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PatientTags` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Telemetry:PrometheusQueryEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:32` |
| `Terminology` | dotnet | - | - | `DotNet/Terminology/Program.cs:132` |
| `Terminology:DefaultExpansionPageSize` | dotnet | yes | - | `DotNet/Terminology/Program.cs:132` |
| `Terminology:EnableCodeUploadEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:132` |
| `Terminology:MaxExpansionPageSize` | dotnet | yes | - | `DotNet/Terminology/Program.cs:132` |
| `Terminology:Path` | dotnet | yes | - | `DotNet/Terminology/Program.cs:132` |

### Validation

25 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `authentication.admin-email` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.anonymous` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.authority` | java | yes | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `authentication.signing-key` | java | yes | dev, qa, qa2, qa3, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/AuthenticationConfig.java:7` |
| `cache.type` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/CacheConfig.java:9` |
| `cache.validate-code.ttl` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/CacheConfig.java:9` |
| `internal-blob-storage.blob-container-name` | java | - | dev, qa, qa2, qa3, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `internal-blob-storage.connection-string` | java | yes | dev, qa, qa2, qa3, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/BlobStorageConfig.java:21` |
| `link.fhir-client-retry.backoff-millis` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/FhirConfig.java:36` |
| `link.fhir-client-retry.max-attempts` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/FhirConfig.java:35` |
| `link.fhir-terminology-service-url` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/LinkConfig.java:16` |
| `link.info-route` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/BaseSpringConfig.java:23` |
| `link.report.base-url` | java | yes | dev, qa, qa2, qa3, test | `Java/measureeval/src/main/java/com/lantanagroup/link/measureeval/configs/LinkConfig.java:42` |
| `link.terminology-service-url` | java | yes | dev, qa, qa2, qa3, test | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/LinkConfig.java:16` |
| `loki.app` | java | yes | dev, qa, qa2, qa3, test | `Java/validation/src/main/resources/logback-spring.xml:3` |
| `loki.enabled` | java | - | dev, qa, qa2, qa3, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `loki.url` | java | - | dev, qa, qa2, qa3, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/LokiConfig.java:9` |
| `management.health.redis.timeout-ms` | java | - | - | `Java/validation/src/main/java/com/lantanagroup/link/validation/health/RedisHealthIndicator.java:56` |
| `pre-qualification.write-expressions-in-operation-outcome` | java | yes | dev, qa, qa2, qa3, test | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/PreQualificationConfig.java:16` |
| `pre-qualification.write-pre-qual-operation-outcome` | java | yes | dev, qa, qa2, qa3, test | `Java/validation/src/main/java/com/lantanagroup/link/validation/configs/PreQualificationConfig.java:16` |
| `secret-management.key-vault-uri` | java | - | dev, qa, qa2, qa3, test | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/SecretManagementConfig.java:7` |
| `service-information.service-name` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/ServiceInformationConfig.java:12` |
| `spring.kafka.retry.max-attempts` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `spring.kafka.retry.retry-backoff-ms` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/KafkaRetryConfig.java:7` |
| `telemetry.exporter-endpoint` | java | - | - | `Java/shared/src/main/java/com/lantanagroup/link/shared/config/TelemetryConfig.java:7` |
