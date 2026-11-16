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

11 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Java:Maven` | dotnet | - | - | `DotNet/Link.AspireAppHost/Extensions/JavaServiceExtensions.cs:32` |
| `MockDmrpApi` | dotnet | - | - | `DotNet/MockDmrpApi/Program.cs:28` |
| `MockFhirServer` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ClinicalPeriodEnd` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ClinicalPeriodStart` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:GenerationSeed` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:PreGeneratedPatientCount` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `MockFhirServer:ResourcesPerPatient` | dotnet | - | - | `DotNet/MockFhirServer/Program.cs:9` |
| `Validation:Engine` | dotnet | - | - | `DotNet/Link.AspireAppHost/Extensions/JavaServiceExtensions.cs:199` |
| `Validation:ExcludedResourceTypes` | dotnet | - | - | `DotNet/Link.AspireAppHost/Extensions/JavaServiceExtensions.cs:68` |
| `Validation:ShadowComparison` | dotnet | - | - | `DotNet/Link.AspireAppHost/Extensions/JavaServiceExtensions.cs:64` |

### Account

127 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:140` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:145` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:146` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `Cache:Type` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:101` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:164` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:147` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:98` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:252` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Account/Program.cs:73` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `SecretManagement:Manager` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:136` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
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
| `UserManagement` | dotnet | - | - | `DotNet/Account/Program.cs:83` |
| `UserManagement:EnableAutomaticUserActivation` | dotnet | yes | - | `DotNet/Account/Program.cs:83` |

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
| `CORS` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:204` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:204` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:204` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:204` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:204` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:204` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:204` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:204` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:204` |
| `Cache` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:93` |
| `Cache:ConnectionString` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:93` |
| `Cache:InstanceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:93` |
| `Cache:Password` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:93` |
| `Cache:Timeout` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:93` |
| `Cache:Type` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:93` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:144` |
| `DataProtection` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:90` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:90` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:90` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableIntegrationFeature` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:242` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:99` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:99` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:99` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:99` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:99` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:99` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:99` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:99` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:99` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:99` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:99` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:92` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:207` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:92` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:92` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:92` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Infrastructure/Extensions/Security/InitializeSecurity.cs:210` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:92` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:328` |
| `MonitorBackendHealthChecks` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:248` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:85` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:150` |
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
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:89` |
| `SecretManagement:Manager` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:170` |
| `SecretManagement:ManagerUri` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:89` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:91` |
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
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:167` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:172` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:173` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Audit/Program.cs:72` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:72` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Audit/Program.cs:72` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:107` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Audit/Persistance/AuditDbContext.cs:41` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Audit/Program.cs:71` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:71` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:174` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Audit/Program.cs:102` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:69` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:73` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:73` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:197` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Audit/Program.cs:65` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Audit/Program.cs:70` |
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

170 keys.

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
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `Dashboard:SeedFakeRuns` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:345` |
| `DataProtection:ApplicationName` | dotnet | yes | dev | `DotNet/Automation.UI/Program.cs:222` |
| `DataProtection:KeyCollectionName` | dotnet | yes | - | `DotNet/Automation.UI/Program.cs:224` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalBlobStorage:SuppressManifest` | dotnet | yes | test | `DotNet/Automation.UI/Services/RunExecutor.cs:115` |
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
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `Serilog:WriteTo` | dotnet | - | - | `DotNet/Automation.UI/Services/AutomationSerilog.cs:17` |
| `ServiceHealth:CheckIntervalSeconds` | dotnet | - | - | `DotNet/Automation.UI/Services/RunExecutor.cs:93` |
| `ServiceHealth:OutageThresholdSeconds` | dotnet | - | - | `DotNet/Automation.UI/Services/RunExecutor.cs:94` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:382` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:37` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
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
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:186` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:191` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:192` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Census/Program.cs:73` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Census/Program.cs:73` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:73` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Census/Program.cs:73` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:85` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Census/Program.cs:75` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:75` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:75` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:75` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:193` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Census/Program.cs:80` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:72` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:74` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:74` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Census/Program.cs:71` |
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

159 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `AcquisitionJobSettings` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:62` |
| `AcquisitionJobSettings:CronSchedule` | dotnet | yes | - | `DotNet/DataAcquisition/Program.cs:62` |
| `ApiSettings` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:144` |
| `ApiSettings:FhirListSettings:ValidStatuses:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:144` |
| `ApiSettings:FhirListSettings:ValidTimeFrames:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:144` |
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:102` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:107` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:108` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `Cache:Type` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:101` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:164` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/DataAcquisition/Program.cs:49` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:49` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:49` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:49` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:109` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:53` |
| `DataSourceAuth` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:148` |
| `DataSourceAuth:KeySource` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:148` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `FhirSearch` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:149` |
| `FhirSearch:PageSize` | dotnet | yes | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:149` |
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
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition/Program.cs:110` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:89` |
| `SecretManagement:Manager` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:136` |
| `SecretManagement:ManagerUri` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:89` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `SftpAcquisition` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:AdtMaxMessageBytes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:BaseRetryDelaySeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:EnableParallelProcessing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:JobIntervalSeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:MaxBatchSize` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:MaxConcurrency` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:MaxRetryAttempts` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:MaxRetryDelaySeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:ParallelProcessingThreshold` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpValidation` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
| `SftpValidation:Connection:MaxHostLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
| `SftpValidation:Connection:MaxRemoteDirectoryLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
| `SftpValidation:Connection:MaxTimeoutMinutes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
| `SftpValidation:FileName:AllowedExtensions:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
| `SftpValidation:FileName:MaxLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
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

160 keys.

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
| `ApiSettings` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:144` |
| `ApiSettings:FhirListSettings:ValidStatuses:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:144` |
| `ApiSettings:FhirListSettings:ValidTimeFrames:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:144` |
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:42` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:47` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:48` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:81` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Account/Program.cs:81` |
| `Cache:Type` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:101` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:164` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `ConnectionStrings:SqlServer` | dotnet | - | - | `DotNet/Account/Persistence/AccountDbContext.cs:57` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:26` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:49` |
| `DataProtection:KeyRing` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:98` |
| `DataSourceAuth` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:148` |
| `DataSourceAuth:KeySource` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:148` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `FhirSearch` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:149` |
| `FhirSearch:PageSize` | dotnet | yes | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:149` |
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
| `LinkTokenService` | dotnet | - | - | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.AcquisitionWorker/Program.cs:50` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:82` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `SecretManagement` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:89` |
| `SecretManagement:Manager` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:136` |
| `SecretManagement:ManagerUri` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Admin.BFF/Program.cs:89` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:80` |
| `SftpAcquisition` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:AdtMaxMessageBytes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:BaseRetryDelaySeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:EnableParallelProcessing` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:JobIntervalSeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:MaxBatchSize` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:MaxConcurrency` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:MaxRetryAttempts` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:MaxRetryDelaySeconds` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpAcquisition:ParallelProcessingThreshold` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:147` |
| `SftpValidation` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
| `SftpValidation:Connection:MaxHostLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
| `SftpValidation:Connection:MaxRemoteDirectoryLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
| `SftpValidation:Connection:MaxTimeoutMinutes` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
| `SftpValidation:FileName:AllowedExtensions:0` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
| `SftpValidation:FileName:MaxLength` | dotnet | - | - | `DotNet/DataAcquisition.Domain/Extensions/GeneralStartupExtensions.cs:146` |
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
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:134` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:139` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:140` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Normalization/Program.cs:78` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Normalization/Program.cs:78` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:78` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Normalization/Program.cs:78` |
| `CacheBlobStorage` | dotnet | - | - | `DotNet/Normalization/Program.cs:75` |
| `CacheBlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Normalization/Program.cs:75` |
| `CacheBlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Normalization/Program.cs:75` |
| `CacheBlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Normalization/Program.cs:75` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:154` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Normalization/Program.cs:71` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:141` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Normalization/Program.cs:149` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Normalization/Program.cs:77` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Normalization/Program.cs:77` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Normalization/Program.cs:77` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:77` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Normalization/Program.cs:79` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:79` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:79` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:79` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:79` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:79` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:79` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Normalization/Program.cs:76` |
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
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:161` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:166` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:167` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Notification/Program.cs:120` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:120` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Notification/Program.cs:120` |
| `Channels` | dotnet | - | - | `DotNet/Notification/Program.cs:119` |
| `Channels:Email` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `Channels:IncludeTestMessage` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `Channels:SubjectTestMessage` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `Channels:TestMessage` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:119` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:184` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:168` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Notification/Program.cs:180` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Notification/Program.cs:56` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:116` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:116` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:121` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:121` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Logging:HmacKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:266` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `ServiceInformation` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Notification/Program.cs:81` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:115` |
| `SmtpConnection` | dotnet | - | - | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:ClientSecret` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:EmailFrom` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Host` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Password` | dotnet | - | - | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Port` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:TenantId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:UseBasicAuth` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:UseOAuth2` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
| `SmtpConnection:Username` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Notification/Program.cs:118` |
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
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:62` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:67` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:68` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:58` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:58` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:58` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:58` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:115` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:76` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:69` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:55` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:55` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:55` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:55` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:55` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:55` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:55` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:55` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:55` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:55` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:55` |
| `LinkTokenService` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:59` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:59` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:59` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:59` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:59` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:59` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:59` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/QueryDispatch/Program.cs:57` |
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

138 keys.

| Key | Runtime | Catalog | Stores | Source |
|---|---|---|---|---|
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:190` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:195` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:196` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Report/Program.cs:103` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:103` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:103` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:103` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:103` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:103` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:103` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:103` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:103` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Report/Program.cs:103` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:103` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Report/Program.cs:103` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:108` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Report/Program.cs:102` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:102` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:102` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:102` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:197` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `EnhancedQueryLoggingSettings` | dotnet | - | - | `DotNet/Report/Program.cs:112` |
| `EnhancedQueryLoggingSettings:EnableEnhancedQueryLogging` | dotnet | yes | test | `DotNet/Report/Program.cs:112` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `InternalBlobStorage` | dotnet | - | - | `DotNet/Report/Program.cs:105` |
| `InternalBlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:105` |
| `InternalBlobStorage:BlobRoot` | dotnet | yes | - | `DotNet/Report/Program.cs:105` |
| `InternalBlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:105` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Report/Program.cs:100` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:100` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:100` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:100` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Report/Program.cs:100` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:100` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:100` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Report/Program.cs:100` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:100` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:100` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:100` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Report/Program.cs:104` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:104` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:104` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:104` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:104` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:104` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:104` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `PatientAggregator` | dotnet | - | - | `DotNet/Report/Program.cs:106` |
| `PatientAggregator:IncludeOrganizationResource` | dotnet | yes | dev, test | `DotNet/Report/Program.cs:106` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Report/Program.cs:96` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `Retention:MaxReportsPerCleanup` | dotnet | yes | - | `DotNet/Report/Application/Configs/RetentionCheckSettings.cs:90` |
| `Retention:Participants` | dotnet | yes | - | `DotNet/Report/Application/Configs/RetentionParticipants.cs:73` |
| `Retention:Schedule` | dotnet | - | - | `DotNet/Report/Application/Extensions/RetentionScheduleRegistration.cs:33` |
| `Retention:Schedule:Cron` | dotnet | yes | - | `DotNet/Report/Application/Extensions/RetentionScheduleRegistration.cs:33` |
| `Retention:Schedule:Enabled` | dotnet | yes | - | `DotNet/Report/Application/Extensions/RetentionScheduleRegistration.cs:33` |
| `Retention:Window` | dotnet | yes | - | `DotNet/Report/Application/Configs/RetentionCheckSettings.cs:89` |
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Report/Program.cs:99` |
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
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:87` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:92` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:93` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Submission/Program.cs:78` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:78` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Submission/Program.cs:78` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:144` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `ConsumerSettings` | dotnet | - | - | `DotNet/Submission/Program.cs:77` |
| `ConsumerSettings:ConsumerRetryDuration:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `ConsumerSettings:DisableConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `ConsumerSettings:DisableRetryConsumer` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:77` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:94` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalBlobStorage` | dotnet | - | - | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:FlattenHierarchy` | dotnet | yes | test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:MeasurePrefixesByReportType` | dotnet | yes | test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:MeasurePrefixesByReportType:{Placeholder}` | dotnet | - | - | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:SuppressManifest` | dotnet | yes | test | `DotNet/Submission/Program.cs:81` |
| `ExternalBlobStorage:UseMeasurePrefix` | dotnet | yes | test | `DotNet/Submission/Program.cs:81` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `Features:DownloadReportEnabled` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Application/Middleware/ConditionalEndpoint.cs:32` |
| `InternalBlobStorage` | dotnet | - | - | `DotNet/Submission/Program.cs:80` |
| `InternalBlobStorage:BlobContainerName` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:80` |
| `InternalBlobStorage:BlobRoot` | dotnet | yes | - | `DotNet/Submission/Program.cs:80` |
| `InternalBlobStorage:ConnectionString` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:80` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:75` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:79` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:79` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:74` |
| `SubmissionServiceConfig` | dotnet | - | - | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:MeasureNames:0:MeasureId` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:MeasureNames:0:ShortName` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:MeasureNames:0:Url` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:PatientBundleBatchSize` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:76` |
| `SubmissionServiceConfig:SubmissionDirectory` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Submission/Program.cs:76` |
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
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:73` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:78` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:79` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `CORS` | dotnet | - | - | `DotNet/Tenant/Program.cs:99` |
| `CORS:AllowAllHeaders` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `CORS:AllowAllMethods` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `CORS:AllowAllOrigins` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `CORS:AllowCredentials` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `CORS:AllowedExposedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `CORS:AllowedHeaders:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `CORS:AllowedMethods:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `CORS:AllowedOrigins:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `CORS:EnableCors` | dotnet | - | - | `DotNet/Tenant/Program.cs:99` |
| `CORS:MaxAge` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:99` |
| `CORS:PolicyName` | dotnet | - | - | `DotNet/Tenant/Program.cs:99` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:DatabaseConnection` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:123` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `DMRP` | dotnet | - | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Api` | dotnet | - | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Scheduling:CatchUpNights` | dotnet | yes | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Scheduling:Concurrency` | dotnet | yes | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Scheduling:NightlyCron` | dotnet | yes | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DMRP:Scheduling:ScheduledFireTimeOverride` | dotnet | yes | - | `DotNet/DMRP/DependencyInjection/DmrpModuleExtensions.cs:110` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:80` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Tenant/Program.cs:118` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `FacilityIdSettings` | dotnet | - | - | `DotNet/Tenant/Program.cs:91` |
| `FacilityIdSettings:NumericOnlyFacilityId` | dotnet | yes | - | `DotNet/Tenant/Program.cs:91` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Tenant/Program.cs:97` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:97` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:97` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:97` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Tenant/Program.cs:97` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:97` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:97` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Tenant/Program.cs:97` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:97` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:97` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:97` |
| `LinkTokenService` | dotnet | - | - | `DotNet/Tenant/Program.cs:100` |
| `LinkTokenService:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:100` |
| `LinkTokenService:EnableTokenGenerationEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:100` |
| `LinkTokenService:LinkAdminEmail` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:100` |
| `LinkTokenService:LogToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:100` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:100` |
| `LinkTokenService:TokenLifespan` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:100` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `MeasureConfig` | dotnet | - | - | `DotNet/Tenant/Program.cs:95` |
| `MeasureConfig:CheckIfMeasureExists` | dotnet | - | - | `DotNet/Tenant/Program.cs:95` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceRegistry` | dotnet | - | - | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:AccountServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:AdminBffServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:AuditServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:CensusServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:DataAcquisitionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:MeasureServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:MockDmrpApiUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:NormalizationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:NotificationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicAccountServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicAdminBffServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicAuditServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicCensusServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicDataAcquisitionServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicMeasureServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicNormalizationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicNotificationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicQueryDispatchServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicReportServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicSubmissionServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicTerminologyServiceUrl` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:PublicValidationServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:QueryDispatchServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:ReportServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:SubmissionServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:TenantService:CheckIfTenantExists` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:TenantService:GetTenantRelativeEndpoint` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:TenantService:PublicTenantServiceUrl` | dotnet | - | dev | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:TenantService:TenantServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:TerminologyServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
| `ServiceRegistry:ValidationServiceUrl` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Tenant/Program.cs:96` |
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
| `Authentication:EnableAnonymousAccess` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:42` |
| `Authentication:Schemas:LinkBearer:Authority` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:47` |
| `Authentication:Schemas:LinkBearer:ValidateToken` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:48` |
| `AutoMigrate` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Shared/Application/Extensions/EFMigrations.cs:13` |
| `BlobStorage` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobContainerName` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:BlobRoot` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `BlobStorage:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ResourceCacheExtensions.cs:68` |
| `ConnectionStrings:AzureAppConfiguration` | dotnet | - | - | `DotNet/Notification/Program.cs:64` |
| `ConnectionStrings:AzureMonitor` | dotnet | - | - | `DotNet/Shared/Application/Extensions/Telemetry/TelemetryServiceExtension.cs:63` |
| `ConnectionStrings:Redis` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:114` |
| `DataProtection:Enabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:49` |
| `DatabaseProvider` | dotnet | yes | - | `DotNet/Account/Program.cs:159` |
| `DistributedLockSettings` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:ConnectionString` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Expiration` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:MaxRetryCount` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:Password` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:PoolSize` | dotnet | yes | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `DistributedLockSettings:RetryDelay` | dotnet | - | - | `DotNet/Shared/Application/Models/Configs/DistributedLockSettings.cs:44` |
| `EnableSwagger` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/MockFhirServer/Program.cs:24` |
| `ExternalConfigurationSource` | dotnet | - | - | `DotNet/Automation.UI/Program.cs:31` |
| `KafkaConnection` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ApiVersionRequest` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:BootstrapServers:0` | dotnet | - | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ClientId` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:GroupId` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:Mechanism` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:Protocol` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:ReceiveMessageMaxBytes` | dotnet | - | - | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslPassword` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslProtocolEnabled` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `KafkaConnection:SaslUsername` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:77` |
| `LinkTokenService:SigningKey` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:50` |
| `Logging` | dotnet | - | - | `DotNet/Shared/Application/Extensions/ExternalConfigurationExtension.cs:23` |
| `ProblemDetails:IncludeExceptionDetails` | dotnet | - | - | `DotNet/Terminology/Program.cs:62` |
| `Redis:Password` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Account/Program.cs:120` |
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
| `ServiceInformation` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Build` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Commit` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ProductVersion` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceConfigName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:ServiceName` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:SwaggerUrl` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
| `ServiceInformation:Version` | dotnet | - | - | `DotNet/Admin.BFF/Program.cs:77` |
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
| `Terminology` | dotnet | - | - | `DotNet/Terminology/Program.cs:133` |
| `Terminology:DefaultExpansionPageSize` | dotnet | yes | - | `DotNet/Terminology/Program.cs:133` |
| `Terminology:EnableCodeUploadEndpoint` | dotnet | yes | dev, qa, qa2, qa3, test | `DotNet/Terminology/Program.cs:133` |
| `Terminology:MaxExpansionPageSize` | dotnet | yes | - | `DotNet/Terminology/Program.cs:133` |
| `Terminology:Path` | dotnet | yes | - | `DotNet/Terminology/Program.cs:133` |

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
