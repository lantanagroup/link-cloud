// The NHSN App UI's webpack dev server when the UI runs under the Aspire AppHost. It is the UI's own
// webpack.app.config.js with three things changed, and nhsn/NHSN-App-UI is left as it is:
//
// - /api is proxied to the NHSN App BFF the AppHost started (NHSN_APP_BFF_URL), not localhost:8079.
// - /shell-config.js is answered here. The UI's index.html loads it for the test profile defaults
//   (issuer, key id, private key), and outside the AppHost only server/main.js serves it.
// - The port is the AppHost's (PORT).
//
// webpack runs with nhsn/NHSN-App-UI as its working directory, so the UI's config is found there.
const path = require('path');

const base = require(path.resolve(process.cwd(), 'webpack.app.config.js'));

const target = process.env.NHSN_APP_BFF_URL;
if (!target) {
  throw new Error('NHSN_APP_BFF_URL is not set. This webpack config is for the Aspire AppHost.');
}

// The same escaping as server/main.js: the values are written into a script.
function escapeForScript(value) {
  return JSON.stringify(value ?? '')
    .replaceAll('<', '\\u003c')
    .replaceAll('>', '\\u003e')
    .replaceAll('&', '\\u0026');
}

// Aspire passes the PEM with real line breaks; compose passes it with \n written out. Accept both.
const privateKeyPem = (process.env.NHSN_APP_UI_DEFAULT_JWT_PRIVATE_KEY_PEM ?? '').replaceAll('\\n', '\n');

const shellConfig = `window.__NHSN_APP_UI_CONFIG__ = {
  defaultJwtIssuer: ${escapeForScript(process.env.NHSN_APP_UI_DEFAULT_JWT_ISSUER)},
  defaultJwtKeyId: ${escapeForScript(process.env.NHSN_APP_UI_DEFAULT_JWT_KEY_ID)},
  defaultJwtPrivateKeyPem: ${escapeForScript(privateKeyPem)}
};`;

module.exports = {
  ...base,
  devServer: {
    ...base.devServer,
    port: Number(process.env.PORT),
    proxy: [
      {
        context: ['/api'],
        target,
        changeOrigin: true
      }
    ],
    setupMiddlewares: (middlewares, devServer) => {
      devServer.app.get('/shell-config.js', (_request, response) => {
        response.type('application/javascript').set('Cache-Control', 'no-store').send(shellConfig);
      });
      return middlewares;
    }
  }
};
