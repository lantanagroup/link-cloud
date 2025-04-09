const express = require('express');
const fs = require('fs');
const path = require('path');
const rateLimit = require('express-rate-limit');

const app = express();
const port = process.env.PORT || 80;

// Basic rate limiting middleware
const apiLimiter = rateLimit({
  windowMs: 15 * 60 * 1000, // 15 minutes
  max: 100, // limit each IP to 100 requests per windowMs
  message: 'Too many requests from this IP, please try again later'
});

let distFolder = getDistFolder();
console.log(`Using dist folder: ${distFolder}`);

const config = getConfig();

app.use(express.static(distFolder));

app.get('/assets/app.config.local.json', (req, res) => {
  res.json(config); // Don't log every time the request is made
});

app.get('/*any', apiLimiter, (req, res) => {
  res.sendFile(path.join(distFolder, 'index.html'));
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
  const configPath = path.join(__dirname, 'src', 'assets', 'app.config.local.json');

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

  return config;
}
