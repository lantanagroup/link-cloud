[← Back Home](../README.md)

## Admin UI

> ⚠️ **Note:** This service is currently called "demo app" and is planned to be renamed.

See [Admin UI Functionality](../functionality/admin_ui.md) for more information on the role of the Admin UI service in the Link Cloud ecosystem.

## Admin UI Overview

- **Technology**: JavaScript (TypeScript) & Angular
- **Image Name**: link-admin-ui
- **Port**: 80
- **Database**: NONE
- **Scale**: 0-5

## Volumes

| Volume                        | Mount Path                                           | Sub-path                |
|-------------------------------|------------------------------------------------------|-------------------------|
| Azure Storage Account         | `/usr/share/nginx/html/assets/app.config.local.json` | `app.config.local.json` |

## app.config.local.json

```json
{
  "baseApiUrl": "<DEMO-API-GATEWAY-BASE-URL>/api",
  "idpIssuer": "https://oauth.nhsnlink.org/realms/NHSNLink",
  "idpClientId": "link-botw"
}