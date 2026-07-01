const express = require('express');
const { createProxyMiddleware } = require('http-proxy-middleware');
const path = require('path');

const app = express();
const root = path.resolve(__dirname, '..', 'dist');
const apiTarget = process.env.BFF_BASE_URL || 'http://nhsn-app-bff:8079/api';

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