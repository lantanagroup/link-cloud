const express = require('express');
const { createProxyMiddleware } = require('http-proxy-middleware');
const path = require('path');

const app = express();
const root = path.resolve(__dirname, '..', 'dist');
const apiTarget = process.env.BFF_BASE_URL || 'http://nhsn-app-bff:8079/api';

function escapeForScript(value) {
  return JSON.stringify(value ?? '');
}

app.get('/shell-config.js', (_req, res) => {
  res.type('application/javascript');
  res.send(`window.__NHSN_APP_UI_CONFIG__ = {
  defaultJwtIssuer: ${escapeForScript(process.env.NHSN_APP_UI_DEFAULT_JWT_ISSUER || 'https://dev-nhsn-app.example.org')},
  defaultJwtKeyId: ${escapeForScript(process.env.NHSN_APP_UI_DEFAULT_JWT_KEY_ID || '')},
  defaultJwtPrivateKeyPem: ${escapeForScript(process.env.NHSN_APP_UI_DEFAULT_JWT_PRIVATE_KEY_PEM || '')}
};`);
});

app.use('/api', createProxyMiddleware({
  target: apiTarget,
  changeOrigin: true,
  xfwd: true
}));

app.use(express.static(root));

app.get('*', (_req, res) => {
  res.sendFile(path.join(root, 'index.html'));
});

const port = process.env.PORT || 8090;
app.listen(port, () => {
  console.log(`NHSN-App-UI shell listening on ${port}; proxying /api to ${apiTarget}`);
});
