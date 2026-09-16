const { CONFIGURATION_KEYS } = require('./configuration-keys');

function loadDefaultConfiguration() {
  return Object.fromEntries(
    Object.entries(CONFIGURATION_KEYS).map(([propertyName, keys]) => [propertyName, keys.defaultValue])
  );
}

function loadEnvironmentConfiguration(environment) {
  return Object.fromEntries(
    Object.entries(CONFIGURATION_KEYS)
      .map(([propertyName, keys]) => [propertyName, environment[keys.environment]])
      .filter(([, value]) => value !== undefined && value !== null)
  );
}

module.exports = { loadDefaultConfiguration, loadEnvironmentConfiguration };