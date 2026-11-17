const { loadAzureAppConfiguration } = require('./azure-app-configuration-provider');
const { CONFIGURATION_KEYS } = require('./configuration-keys');
const {
  loadDefaultConfiguration,
  loadEnvironmentConfiguration
} = require('./environment-configuration-provider');

const EXTERNAL_CONFIGURATION_SOURCE_ENVIRONMENT_VARIABLE = 'ExternalConfigurationSource';
const LOG_PREFIX = '[NHSN-App-UI configuration]';

const externalConfigurationProviders = Object.freeze({
  AzureAppConfiguration: loadAzureAppConfiguration
});

async function loadRuntimeConfiguration(
  environment = process.env,
  providers = externalConfigurationProviders,
  logger = console
) {
  const defaultConfiguration = loadDefaultConfiguration();
  const environmentConfiguration = loadEnvironmentConfiguration(environment);
  const source = environment[EXTERNAL_CONFIGURATION_SOURCE_ENVIRONMENT_VARIABLE];
  let externalConfiguration = {};

  if (!source) {
    logger.info(`${LOG_PREFIX} External configuration is disabled; using environment variables and built-in defaults.`);
  } else {
    const provider = providers[source];
    if (!provider) {
      throw new Error(`Unsupported external configuration source: ${source}.`);
    }

    logger.info(`${LOG_PREFIX} ${EXTERNAL_CONFIGURATION_SOURCE_ENVIRONMENT_VARIABLE}=${source}.`);
    externalConfiguration = await provider(environment, { logger });
  }

  const runtimeConfiguration = {
    ...defaultConfiguration,
    ...externalConfiguration,
    ...environmentConfiguration
  };

  logEffectiveConfiguration(
    logger,
    runtimeConfiguration,
    externalConfiguration,
    environmentConfiguration
  );
  logger.info(`${LOG_PREFIX} Runtime configuration is ready.`);

  return runtimeConfiguration;
}

function logEffectiveConfiguration(
  logger,
  configuration,
  externalConfiguration,
  environmentConfiguration
) {
  for (const [propertyName, keys] of Object.entries(CONFIGURATION_KEYS)) {
    const source = Object.hasOwn(environmentConfiguration, propertyName)
      ? `environment variable ${keys.environment}`
      : Object.hasOwn(externalConfiguration, propertyName)
        ? `external configuration (${keys.external})`
        : 'built-in default';
    const status = configuration[propertyName] ? 'populated' : 'empty';

    logger.info(`${LOG_PREFIX} ${keys.displayName}: ${status}; source=${source}; value redacted.`);
  }
}

module.exports = {
  EXTERNAL_CONFIGURATION_SOURCE_ENVIRONMENT_VARIABLE,
  loadRuntimeConfiguration
};