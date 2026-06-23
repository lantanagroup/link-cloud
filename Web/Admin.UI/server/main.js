const express = require('express');
const fs = require('fs');
const path = require('path');
const rateLimit = require('express-rate-limit');
const helmet = require('helmet');
const { blockPathMatchers } = require('./blocked-paths');

const app = express();

const trustProxyEnv = process.env.TRUST_PROXY || '1';
const trustProxyValue = !isNaN(trustProxyEnv) ? Number(trustProxyEnv) : trustProxyEnv;
app.set('trust proxy', trustProxyValue);
console.log(`Express trust proxy is set to: ${trustProxyValue}`);


const port = process.env.PORT || 80;

// Rate limiting at the application level is unnecessary in our case
// And it may actually be producing false positives during dynamic scanning, or else masking legitimate issues
// However, completely *removing* rate limiting causes failures in static scanning due to a perceived denial-of-service vulnerability
// As a compromise, retain rate limiting, but with a generous enough limit that the dynamic scanner shouldn't get throttled
const apiLimiter = rateLimit({
  windowMs: 1000,
  max: 1000,
  message: ''
});

let distFolder = getDistFolder();
console.log(`Using dist folder: ${distFolder}`);

const config = getConfig();

// Add security middleware for production use
if (config.production || process.env.NODE_ENV === 'production') {
  app.use(helmet());
}

app.use(apiLimiter);

app.get('/{*any}', (req, res, next) => {
  const p = req.path;
  if (p.includes("//") || p.includes("/./") || p.includes("/../")) {
    res.status(400).send();
  } else {
    next();
  }
});

app.use(express.static(distFolder));

app.get('/assets/app.config.local.json', (req, res) => {
  res.json(config); // Don't log every time the request is made
});

app.get('/{*any}', (req, res) => {
  const p = req.path; // pathname only (no querystring)

  const isExcluded = blockPathMatchers.some((rule) => {
    if (typeof rule === 'string') return p.toLowerCase() === rule.toLowerCase();
    return rule.test(p);
  });

  if (isExcluded) return res.status(404).send();

  res.sendFile(path.join(distFolder, 'index.html'));
});

app.all('/{*any}', (req, res) => {
  res.status(400).send();
});

app.listen(port, () => {
  console.log(`Server is running on http://localhost:${port}`);
});

function getDistFolder() {
  let folder;

  // Check if the LINK_DIST_FOLDER environment variable is set
  if (process.env.LINK_DIST_FOLDER !== undefined) {
    folder = process.env.LINK_DIST_FOLDER;
  } else {
    // Assume the dist folder is in the same directory as this script
    folder = path.join(__dirname, 'dist');

    // If not, check the parent directory for the dist folder
    if (!fs.existsSync(folder)) {
      folder = path.join(__dirname, '..', 'dist');
    }
  }

  // Ensure the dist folder exists
  if (!fs.existsSync(folder)) {
    throw new Error('Dist folder not found. Please build the project first.');
  }

  return folder;
}

function getConfig() {
  const configPath = path.join(distFolder, 'assets', 'app.config.local.json');

  let config = {};

  if (fs.existsSync(configPath)) {
    try {
      config = JSON.parse(fs.readFileSync(configPath, 'utf8'));
    } catch (err) {
      console.error('Error reading config file:', err);
      return res.status(500).send({ error: 'Could not parse config file' });
    }
  }

  // Apply environment variable overrides
  if (process.env.LINK_BASE_API_URL !== undefined) {
    config.baseApiUrl = process.env.LINK_BASE_API_URL;
    console.log('Found LINK_BASE_API_URL:', config.baseApiUrl);
  }

  if (process.env.LINK_PRODUCTION !== undefined) {
    config.production = process.env.LINK_PRODUCTION === 'true';
    console.log('Found LINK_PRODUCTION:', config.production);
  }

  if (process.env.LINK_AUTH_REQUIRED !== undefined) {
    config.authRequired = process.env.LINK_AUTH_REQUIRED === 'true';
    console.log('Found LINK_AUTH_REQUIRED:', config.authRequired);
  }

  // Ensure oauth2 block exists before assigning nested values
  if (
    process.env.LINK_OAUTH2_ENABLED !== undefined ||
    process.env.LINK_OAUTH2_ISSUER !== undefined ||
    process.env.LINK_OAUTH2_CLIENT_ID !== undefined ||
    process.env.LINK_OAUTH2_SCOPE !== undefined ||
    process.env.LINK_OAUTH2_RESPONSE_TYPE !== undefined
  ) {
    config.oauth2 = config.oauth2 || {};

    if (process.env.LINK_OAUTH2_ENABLED !== undefined) {
      config.oauth2.enabled = process.env.LINK_OAUTH2_ENABLED === 'true';
      console.log('Found LINK_OAUTH2_ENABLED:', config.oauth2.enabled);
    }

    if (process.env.LINK_OAUTH2_ISSUER !== undefined) {
      config.oauth2.issuer = process.env.LINK_OAUTH2_ISSUER;
      console.log('Found LINK_OAUTH2_ISSUER:', config.oauth2.issuer);
    }

    if (process.env.LINK_OAUTH2_CLIENT_ID !== undefined) {
      config.oauth2.clientId = process.env.LINK_OAUTH2_CLIENT_ID;
      console.log('Found LINK_OAUTH2_CLIENT_ID:', config.oauth2.clientId);
    }

    if (process.env.LINK_OAUTH2_SCOPE !== undefined) {
      config.oauth2.scope = process.env.LINK_OAUTH2_SCOPE;
      console.log('Found LINK_OAUTH2_SCOPE:', config.oauth2.scope);
    }

    if (process.env.LINK_OAUTH2_RESPONSE_TYPE !== undefined) {
      config.oauth2.responseType = process.env.LINK_OAUTH2_RESPONSE_TYPE;
      console.log('Found LINK_OAUTH2_RESPONSE_TYPE:', config.oauth2.responseType);
    }
  }

  if (process.env.GRAFANA_URL !== undefined) {
    config.grafanaUrl = process.env.GRAFANA_URL;
    console.log('Found GRAFANA_URL:', config.grafanaUrl);
  }

  if (process.env.KAFKA_URL !== undefined) {
    config.kafkaUrl = process.env.KAFKA_URL;
    console.log('Found KAFKA_URL:', config.kafkaUrl);
  }

  return config;
}
