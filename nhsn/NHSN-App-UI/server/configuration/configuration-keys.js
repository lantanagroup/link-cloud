const CONFIGURATION_KEYS = Object.freeze({
  bffBaseUrl: Object.freeze({
    displayName: 'BFF proxy target',
    environment: 'BFF_BASE_URL',
    external: '/nhsn_app_ui/bff/base_url',
    defaultValue: 'http://nhsn-app-bff:8079/api'
  }),
  port: Object.freeze({
    displayName: 'Server port',
    environment: 'PORT',
    external: '/nhsn_app_ui/server/port',
    defaultValue: '8080'
  }),
  defaultJwtIssuer: Object.freeze({
    displayName: 'Default JWT issuer',
    environment: 'NHSN_APP_UI_DEFAULT_JWT_ISSUER',
    external: '/nhsn_app_ui/default_jwt/issuer',
    defaultValue: ''
  }),
  defaultJwtKeyId: Object.freeze({
    displayName: 'Default JWT key id',
    environment: 'NHSN_APP_UI_DEFAULT_JWT_KEY_ID',
    external: '/nhsn_app_ui/default_jwt/key_id',
    defaultValue: ''
  }),
  defaultJwtPrivateKeyPem: Object.freeze({
    displayName: 'Default JWT private key PEM',
    environment: 'NHSN_APP_UI_DEFAULT_JWT_PRIVATE_KEY_PEM',
    external: '/nhsn_app_ui/default_jwt/private_key_pem',
    defaultValue: ''
  })
});

module.exports = { CONFIGURATION_KEYS };