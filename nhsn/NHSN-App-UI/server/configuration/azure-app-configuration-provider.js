const { load, LabelFilter } = require('@azure/app-configuration-provider');
const { DefaultAzureCredential } = require('@azure/identity');
const { CONFIGURATION_KEYS } = require('./configuration-keys');

const CONNECTION_STRING_ENVIRONMENT_VARIABLE = 'ConnectionStrings__AzureAppConfiguration';
const KEY_FILTER = '/nhsn_app_ui/*';
const SERVICE_LABEL = 'NHSNAppUIHarness';
const LOG_PREFIX = '[NHSN-App-UI configuration] [AzureAppConfiguration]';

function logInfo(logger, message) {
  const log = logger.info || logger.log;
  log.call(logger, `${LOG_PREFIX} ${message}`);
}

async function loadAzureAppConfiguration(environment, dependencies = {}) {
  const connectionString = environment[CONNECTION_STRING_ENVIRONMENT_VARIABLE];
  const logger = dependencies.logger || console;

  if (!connectionString) {
    throw new Error(
      `ExternalConfigurationSource is AzureAppConfiguration, but ${CONNECTION_STRING_ENVIRONMENT_VARIABLE} is missing or empty.`
    );
  }

  const loadConfiguration = dependencies.loadConfiguration || load;
  const credential = dependencies.credential || new DefaultAzureCredential();

  logInfo(logger, `${CONNECTION_STRING_ENVIRONMENT_VARIABLE} is present (value redacted).`);
  logInfo(
    logger,
    `Loading ${KEY_FILTER} using labels <unlabeled> followed by ${SERVICE_LABEL}; the service label takes precedence.`
  );
  logInfo(logger, 'Azure Key Vault reference resolution is enabled with DefaultAzureCredential.');

  const settings = await loadConfiguration(connectionString, {
    selectors: [
      { keyFilter: KEY_FILTER, labelFilter: LabelFilter.Null },
      { keyFilter: KEY_FILTER, labelFilter: SERVICE_LABEL }
    ],
    keyVaultOptions: { credential }
  });

  logInfo(logger, 'Connected successfully and loaded configuration from Azure App Configuration.');

  const configuration = {};
  let loadedKeyCount = 0;
  for (const [propertyName, keys] of Object.entries(CONFIGURATION_KEYS)) {
    const value = settings.get(keys.external);
    const loaded = value !== undefined && value !== null;

    logInfo(
      logger,
      `${keys.external}: ${loaded ? 'loaded' : 'not found; lower-priority configuration will be used'} (value redacted).`
    );

    if (loaded) {
      loadedKeyCount += 1;
      configuration[propertyName] = value;
    }
  }

  logInfo(
    logger,
    `Load summary: ${loadedKeyCount}/${Object.keys(CONFIGURATION_KEYS).length} expected keys loaded.`
  );

  return configuration;
}

module.exports = {
  CONNECTION_STRING_ENVIRONMENT_VARIABLE,
  KEY_FILTER,
  SERVICE_LABEL,
  loadAzureAppConfiguration
};