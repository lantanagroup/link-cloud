const assert = require('node:assert/strict');
const test = require('node:test');
const { LabelFilter } = require('@azure/app-configuration-provider');
const {
  CONNECTION_STRING_ENVIRONMENT_VARIABLE,
  KEY_FILTER,
  SERVICE_LABEL,
  loadAzureAppConfiguration
} = require('../configuration/azure-app-configuration-provider');

test('requires the Azure App Configuration connection string', async () => {
  await assert.rejects(
    loadAzureAppConfiguration({}, {
      loadConfiguration: async () => new Map(),
      credential: {}
    }),
    new RegExp(CONNECTION_STRING_ENVIRONMENT_VARIABLE)
  );
});

test('loads the organized keys and enables Key Vault reference resolution', async () => {
  const credential = { name: 'test-credential' };
  const messages = [];
  let receivedConnectionString;
  let receivedOptions;

  const configuration = await loadAzureAppConfiguration(
    { [CONNECTION_STRING_ENVIRONMENT_VARIABLE]: 'Endpoint=https://example.test;Id=id;Secret=secret' },
    {
      credential,
      logger: { info: message => messages.push(message) },
      loadConfiguration: async (connectionString, options) => {
        receivedConnectionString = connectionString;
        receivedOptions = options;
        return new Map([
          ['/nhsn_app_ui/bff/base_url', 'https://azure-bff.example.org/api'],
          ['/nhsn_app_ui/server/port', '9200'],
          ['/nhsn_app_ui/default_jwt/issuer', 'https://azure.example.org'],
          ['/nhsn_app_ui/default_jwt/key_id', 'azure-key'],
          ['/nhsn_app_ui/default_jwt/private_key_pem', 'azure-private-key']
        ]);
      }
    }
  );

  assert.equal(receivedConnectionString, 'Endpoint=https://example.test;Id=id;Secret=secret');
  assert.equal(SERVICE_LABEL, 'NhsnAppUI');
  assert.equal(receivedOptions.selectors.length, 2);
  assert.deepEqual(receivedOptions.selectors, [
    { keyFilter: KEY_FILTER, labelFilter: LabelFilter.Null },
    { keyFilter: KEY_FILTER, labelFilter: SERVICE_LABEL }
  ]);
  assert.equal(receivedOptions.keyVaultOptions.credential, credential);
  assert.deepEqual(configuration, {
    bffBaseUrl: 'https://azure-bff.example.org/api',
    port: '9200',
    defaultJwtIssuer: 'https://azure.example.org',
    defaultJwtKeyId: 'azure-key',
    defaultJwtPrivateKeyPem: 'azure-private-key'
  });
  assert.ok(messages.some(message => message.includes('is present (value redacted)')));
  assert.ok(messages.some(message => message.includes(`Loading ${KEY_FILTER}`)));
  assert.ok(messages.some(message => message.includes('Connected successfully')));
  assert.ok(messages.some(message => message.includes('/nhsn_app_ui/default_jwt/issuer: loaded')));
  assert.ok(messages.some(message => message.includes('/nhsn_app_ui/default_jwt/private_key_pem: loaded')));
  assert.ok(messages.some(message => message.includes('/nhsn_app_ui/bff/base_url: loaded')));
  assert.ok(messages.some(message => message.includes('Load summary: 5/5 expected keys loaded')));
  assert.ok(messages.every(message => !message.includes('Secret=secret')));
  assert.ok(messages.every(message => !message.includes('azure-key')));
  assert.ok(messages.every(message => !message.includes('azure-private-key')));
});

test('logs when an expected AAC key is missing without logging values', async () => {
  const messages = [];

  const configuration = await loadAzureAppConfiguration(
    { [CONNECTION_STRING_ENVIRONMENT_VARIABLE]: 'redacted-connection' },
    {
      credential: {},
      logger: { info: message => messages.push(message) },
      loadConfiguration: async () => new Map([
        ['/nhsn_app_ui/default_jwt/issuer', 'secret-issuer-value']
      ])
    }
  );

  assert.deepEqual(configuration, { defaultJwtIssuer: 'secret-issuer-value' });
  assert.ok(messages.some(message =>
    message.includes('/nhsn_app_ui/default_jwt/key_id: not found; lower-priority configuration will be used')));
  assert.ok(messages.some(message => message.includes('Load summary: 1/5 expected keys loaded')));
  assert.ok(messages.every(message => !message.includes('redacted-connection')));
  assert.ok(messages.every(message => !message.includes('secret-issuer-value')));
});