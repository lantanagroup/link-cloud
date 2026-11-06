// The Admin UI's ng serve proxy when the UI runs under the Aspire AppHost. Web/Admin.UI/proxy.conf.json,
// which a developer uses with ng serve on its own, is left as it is.
//
// The AppHost passes two things in the environment:
// - LINK_ADMIN_BFF_URL: the Admin BFF, which /api is proxied to.
// - LINK_ADMIN_UI_CONFIG: JSON the UI merges over src/assets/app.config.json. The UI fetches
//   /assets/app.config.local.json and deep-merges it when it exists, so a small server in this
//   ng serve process answers that request with the AppHost's overrides. Nothing is written into
//   Web/Admin.UI, so ng serve outside the AppHost is unaffected. A developer's own
//   src/assets/app.config.local.json, if there is one, is served first and wins.
import http from 'node:http';

const target = process.env.LINK_ADMIN_BFF_URL;
if (!target) {
  throw new Error('LINK_ADMIN_BFF_URL is not set. This proxy file is for the Aspire AppHost.');
}

const overrides = process.env.LINK_ADMIN_UI_CONFIG || '{}';
JSON.parse(overrides); // Fail at startup, not in the browser, if the AppHost passes something that is not JSON.

const configServer = http.createServer((request, response) => {
  response.writeHead(200, { 'Content-Type': 'application/json', 'Cache-Control': 'no-store' });
  response.end(overrides);
});
await new Promise((resolve) => configServer.listen(0, '127.0.0.1', resolve));
const configServerUrl = `http://127.0.0.1:${configServer.address().port}`;

export default {
  '/assets/app.config.local.json': {
    target: configServerUrl,
    changeOrigin: true
  },
  '/api': {
    target,
    changeOrigin: true,
    secure: false,
    logLevel: 'info'
  }
};
