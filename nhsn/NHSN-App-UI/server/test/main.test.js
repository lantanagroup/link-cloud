const assert = require('node:assert/strict');
const fs = require('node:fs');
const os = require('node:os');
const path = require('node:path');
const test = require('node:test');
const { createApp, parsePort } = require('../main');

const testConfiguration = {
  bffBaseUrl: 'http://test-bff.example.org/api',
  port: '8090',
  defaultJwtIssuer: '',
  defaultJwtKeyId: '',
  defaultJwtPrivateKeyPem: ''
};

test('serves resolved runtime defaults from shell-config.js', async t => {
  const { app } = createApp({
    bffBaseUrl: 'http://test-bff.example.org/api',
    port: '8090',
    defaultJwtIssuer: 'https://issuer.example.org/</script>',
    defaultJwtKeyId: 'test-key',
    defaultJwtPrivateKeyPem: 'line 1\nline 2'
  });
  const server = app.listen(0);
  t.after(() => server.close());

  await new Promise(resolve => server.once('listening', resolve));
  const { port } = server.address();
  const response = await fetch(`http://127.0.0.1:${port}/shell-config.js`);
  const body = await response.text();

  assert.equal(response.status, 200);
  assert.match(response.headers.get('content-type'), /^application\/javascript/);
  assert.match(body, /defaultJwtKeyId: "test-key"/);
  assert.match(body, /defaultJwtPrivateKeyPem: "line 1\\nline 2"/);
  assert.doesNotMatch(body, /<\/script>/);
});

test('serves the cached index.html for client-side routes', async t => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'nhsn-app-ui-dist-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));
  fs.writeFileSync(path.join(root, 'index.html'), '<html>shell</html>');

  const { app } = createApp(testConfiguration, { root });
  fs.writeFileSync(path.join(root, 'index.html'), '<html>changed</html>');
  const server = app.listen(0);
  t.after(() => server.close());

  await new Promise(resolve => server.once('listening', resolve));
  const { port } = server.address();
  const response = await fetch(`http://127.0.0.1:${port}/reports/123`);

  assert.equal(response.status, 200);
  assert.match(response.headers.get('content-type'), /^text\/html/);
  assert.equal(await response.text(), '<html>shell</html>');
});

test('returns 404 for client-side routes when index.html has not been built', async t => {
  const root = fs.mkdtempSync(path.join(os.tmpdir(), 'nhsn-app-ui-dist-'));
  t.after(() => fs.rmSync(root, { recursive: true, force: true }));

  const { app } = createApp(testConfiguration, { root });
  const server = app.listen(0);
  t.after(() => server.close());

  await new Promise(resolve => server.once('listening', resolve));
  const { port } = server.address();
  const response = await fetch(`http://127.0.0.1:${port}/reports/123`);

  assert.equal(response.status, 404);
});

test('uses the resolved BFF base URL as the proxy target', () => {
  const { apiTarget } = createApp({
    bffBaseUrl: 'https://configured-bff.example.org/api',
    port: '8090',
    defaultJwtIssuer: '',
    defaultJwtKeyId: '',
    defaultJwtPrivateKeyPem: ''
  });

  assert.equal(apiTarget, 'https://configured-bff.example.org/api');
});

test('parses and validates the configured server port', () => {
  assert.equal(parsePort('8090'), 8090);
  assert.throws(() => parsePort('not-a-port'), /must be an integer/);
  assert.throws(() => parsePort('70000'), /must be an integer/);
});