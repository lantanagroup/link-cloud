const express = require('express');
const fs = require('fs');
const { createProxyMiddleware } = require('http-proxy-middleware');
const path = require('path');
const { loadRuntimeConfiguration } = require('./configuration/runtime-configuration');

function escapeForScript(value) {
  return JSON.stringify(value ?? '')
    .replaceAll('<', '\\u003c')
    .replaceAll('>', '\\u003e')
    .replaceAll('&', '\\u0026');
}

function createApp(runtimeConfiguration, { root = path.resolve(__dirname, '..', 'dist') } = {}) {
  const app = express();
  const apiTarget = runtimeConfiguration.bffBaseUrl;

  app.get('/shell-config.js', (_req, res) => {
    res.type('application/javascript');
    res.send(`window.__NHSN_APP_UI_CONFIG__ = {
  defaultJwtIssuer: ${escapeForScript(runtimeConfiguration.defaultJwtIssuer)},
  defaultJwtKeyId: ${escapeForScript(runtimeConfiguration.defaultJwtKeyId)},
  defaultJwtPrivateKeyPem: ${escapeForScript(runtimeConfiguration.defaultJwtPrivateKeyPem)}
};`);
  });

  app.use('/api', createProxyMiddleware({
    target: apiTarget,
    changeOrigin: true,
    xfwd: true
  }));

  app.use(express.static(root));

  // Read once so the SPA fallback never touches the file system per request.
  const indexHtml = readIndexHtml(root);
  app.get('*', (_req, res) => {
    if (indexHtml === null) {
      res.sendStatus(404);
      return;
    }

    res.type('html').send(indexHtml);
  });

  return { app, apiTarget };
}

async function startServer(environment = process.env) {
  const runtimeConfiguration = await loadRuntimeConfiguration(environment);
  const { app, apiTarget } = createApp(runtimeConfiguration);
  const port = parsePort(runtimeConfiguration.port);

  return app.listen(port, () => {
    console.log(`NHSN-App-UI shell listening on ${port}; proxying /api to ${apiTarget}`);
  });
}

function readIndexHtml(root) {
  try {
    return fs.readFileSync(path.join(root, 'index.html'));
  } catch (error) {
    if (error.code === 'ENOENT') {
      return null;
    }

    throw error;
  }
}

function parsePort(value) {
  const port = Number(value);
  if (!Number.isInteger(port) || port < 1 || port > 65535) {
    throw new Error(`NHSN-App-UI server port must be an integer from 1 through 65535; received ${JSON.stringify(value)}.`);
  }

  return port;
}

if (require.main === module) {
  startServer().catch(error => {
    console.error('NHSN-App-UI shell failed to start:', error);
    process.exitCode = 1;
  });
}

module.exports = { createApp, parsePort, startServer };
