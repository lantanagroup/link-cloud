[← Back Home](../README.md)

## Admin UI

> ⚠️ **Note:** This service is currently called "demo app" and is planned to be renamed.

See [Admin UI Functionality](../functionality/admin_ui.md) for more information on the role of the Admin UI service in the Link Cloud ecosystem.

## Admin UI Overview

- **Technology**: JavaScript (TypeScript) & Angular
- **Image Name**: link-admin-ui
- **Port**: 80
- **Database**: N/A
- **Scale**: 0-5

## Volumes

| Volume                        | Mount Path                                           | Sub-path                |
|-------------------------------|------------------------------------------------------|-------------------------|
| Azure Storage Account         | `/usr/share/nginx/html/assets/app.config.local.json` | `app.config.local.json` |

## app.config.local.json

The `app.config.local.json` file is used to configure the Admin UI service at deployment time, overriding the defaults in `app.config.json`.

```json
{
  "baseApiUrl": "<ADMIN-BFF-BASE-URL>/api",
  "authRequired": true,
  "oauth2": {
    "enabled": true,
    "issuer": "...",
    "clientId": "...",
    "scope": "openid profile email",
    "responseType": "code"
  }
}
```

### Properties

| **Property**          | **Description**                                                | **Default Value**      | **Required**      |
|-----------------------|----------------------------------------------------------------|------------------------|-------------------|
| `baseApiUrl`          | The base URL for the Admin BFF API.                            | `http://localhost`     | Yes               |
| `authRequired`        | Indicates whether authentication is required for the Admin UI. | `true`                 | Yes               |
| `oauth2.enabled`      | Indicates whether OAuth2 authentication is enabled.            | `false`                | Yes               |
| `oauth2.issuer`       | The issuer URL for the OAuth2 provider.                        | `null`                 | If oauth2 enabled |
| `oauth2.clientId`     | The client ID for the OAuth2 application.                      | `null`                 | If oauth2 enabled |
| `oauth2.scope`        | The scope for the OAuth2 application.                          | `openid profile email` | If oauth2 enabled |
| `oauth2.responseType` | The response type for the OAuth2 application.                  | `code`                 | If oauth2 enabled |