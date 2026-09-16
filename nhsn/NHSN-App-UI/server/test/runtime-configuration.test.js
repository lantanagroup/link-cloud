const assert = require('node:assert/strict');
const test = require('node:test');
const {
  EXTERNAL_CONFIGURATION_SOURCE_ENVIRONMENT_VARIABLE,
  loadRuntimeConfiguration
} = require('../configuration/runtime-configuration');

function createLogger() {
  const messages = [];
  return {
    messages,
    logger: { info: message => messages.push(message) }
  };
}

test('loads the existing environment variables when no external source is configured', async () => {
  const { logger, messages } = createLogger();
  const configuration = await loadRuntimeConfiguration({
    BFF_BASE_URL: 'https://environment-bff.example.org/api',
    PORT: '9000',
    NHSN_APP_UI_DEFAULT_JWT_ISSUER: 'https://environment.example.org',
    NHSN_APP_UI_DEFAULT_JWT_KEY_ID: 'environment-key',
    NHSN_APP_UI_DEFAULT_JWT_PRIVATE_KEY_PEM: 'environment-private-key'
  }, undefined, logger);

  assert.deepEqual(configuration, {
    bffBaseUrl: 'https://environment-bff.example.org/api',
    port: '9000',
    defaultJwtIssuer: 'https://environment.example.org',
    defaultJwtKeyId: 'environment-key',
    defaultJwtPrivateKeyPem: 'environment-private-key'
  });
  assert.ok(messages.some(message => message.includes('External configuration is disabled')));
  assert.ok(messages.some(message => message.includes('source=environment variable BFF_BASE_URL')));
  assert.ok(messages.some(message => message.includes('source=environment variable NHSN_APP_UI_DEFAULT_JWT_ISSUER')));
  assert.ok(messages.every(message => !message.includes('environment-private-key')));
});

test('environment values override external configuration and external values override defaults', async () => {
  const { logger, messages } = createLogger();
  const environment = {
    [EXTERNAL_CONFIGURATION_SOURCE_ENVIRONMENT_VARIABLE]: 'TestProvider',
    BFF_BASE_URL: 'https://environment-bff.example.org/api',
    NHSN_APP_UI_DEFAULT_JWT_ISSUER: 'https://environment.example.org',
    NHSN_APP_UI_DEFAULT_JWT_KEY_ID: 'environment-key'
  };

  const configuration = await loadRuntimeConfiguration(environment, {
    TestProvider: async () => ({
      bffBaseUrl: 'https://external-bff.example.org/api',
      port: '9100',
      defaultJwtIssuer: 'https://external.example.org',
      defaultJwtPrivateKeyPem: 'external-private-key'
    })
  }, logger);

  assert.deepEqual(configuration, {
    bffBaseUrl: 'https://environment-bff.example.org/api',
    port: '9100',
    defaultJwtIssuer: 'https://environment.example.org',
    defaultJwtKeyId: 'environment-key',
    defaultJwtPrivateKeyPem: 'external-private-key'
  });
  assert.ok(messages.some(message => message.includes('ExternalConfigurationSource=TestProvider')));
  assert.ok(messages.some(message => message.includes('source=environment variable BFF_BASE_URL')));
  assert.ok(messages.some(message => message.includes('source=external configuration (/nhsn_app_ui/server/port)')));
  assert.ok(messages.some(message => message.includes('source=environment variable NHSN_APP_UI_DEFAULT_JWT_ISSUER')));
  assert.ok(messages.some(message => message.includes('source=environment variable NHSN_APP_UI_DEFAULT_JWT_KEY_ID')));
  assert.ok(messages.some(message => message.includes('Runtime configuration is ready')));
  assert.ok(messages.every(message => !message.includes('https://external.example.org')));
  assert.ok(messages.every(message => !message.includes('external-private-key')));
});

test('uses hard-coded defaults when neither AAC nor environment variables provide values', async () => {
  const { logger, messages } = createLogger();

  const configuration = await loadRuntimeConfiguration({}, undefined, logger);

  assert.deepEqual(configuration, {
    bffBaseUrl: 'http://nhsn-app-bff:8079/api',
    port: '8080',
    defaultJwtIssuer: '',
    defaultJwtKeyId: '',
    defaultJwtPrivateKeyPem: ''
  });
  assert.ok(messages.some(message => message.includes('BFF proxy target: populated; source=built-in default')));
  assert.ok(messages.some(message => message.includes('Server port: populated; source=built-in default')));
  assert.ok(messages.some(message => message.includes('Default JWT issuer: empty; source=built-in default')));
});

test('rejects an unsupported external configuration source', async () => {
  await assert.rejects(
    loadRuntimeConfiguration({
      [EXTERNAL_CONFIGURATION_SOURCE_ENVIRONMENT_VARIABLE]: 'UnsupportedProvider'
    }),
    /Unsupported external configuration source: UnsupportedProvider/
  );
});