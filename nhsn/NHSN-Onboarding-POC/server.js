'use strict';
/*
 * Minimal static file + persistence server for the NHSN Link Onboarding POC.
 * No third-party dependencies — only Node's built-in http/fs modules.
 * Serves index.html and exposes:
 *   GET  /api/facilities        -> array of facility records (parsed from facilities.ndjson)
 *   POST /api/facilities        -> upsert one facility record by facilityId (rewrites the ndjson file)
 */
const http = require('http');
const fs = require('fs');
const path = require('path');

const PORT = process.env.PORT || 5173;
const ROOT = __dirname;
const DATA_FILE = path.join(ROOT, 'facilities.ndjson');

const MIME_TYPES = {
  '.html': 'text/html; charset=utf-8',
  '.js': 'text/javascript; charset=utf-8',
  '.css': 'text/css; charset=utf-8',
  '.pdf': 'application/pdf',
  '.svg': 'image/svg+xml',
  '.png': 'image/png',
};

function readFacilities() {
  if (!fs.existsSync(DATA_FILE)) return [];
  const raw = fs.readFileSync(DATA_FILE, 'utf8');
  return raw
    .split('\n')
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      try {
        return JSON.parse(line);
      } catch (err) {
        console.error('Skipping malformed ndjson line:', err.message);
        return null;
      }
    })
    .filter(Boolean);
}

function writeFacilities(list) {
  const lines = list.map((f) => JSON.stringify(f));
  const contents = lines.length ? lines.join('\n') + '\n' : '';
  fs.writeFileSync(DATA_FILE, contents, 'utf8');
}

function sendJson(res, status, obj) {
  const body = JSON.stringify(obj);
  res.writeHead(status, {
    'Content-Type': 'application/json; charset=utf-8',
    'Content-Length': Buffer.byteLength(body),
  });
  res.end(body);
}

const server = http.createServer((req, res) => {
  let u;
  try {
    u = new URL(req.url, `http://${req.headers.host}`);
  } catch {
    res.writeHead(400);
    return res.end('Bad request');
  }

  if (u.pathname === '/api/facilities' && req.method === 'GET') {
    try {
      return sendJson(res, 200, readFacilities());
    } catch (err) {
      return sendJson(res, 500, { error: err.message });
    }
  }

  if (u.pathname === '/api/facilities' && req.method === 'POST') {
    let body = '';
    let tooLarge = false;
    req.on('data', (chunk) => {
      body += chunk;
      if (body.length > 5_000_000) {
        tooLarge = true;
        req.destroy();
      }
    });
    req.on('end', () => {
      if (tooLarge) return;
      try {
        const incoming = JSON.parse(body);
        if (!incoming || typeof incoming.facilityId !== 'string' || !incoming.facilityId.trim()) {
          return sendJson(res, 400, { error: 'facilityId is required' });
        }
        const list = readFacilities();
        const idx = list.findIndex((f) => f.facilityId === incoming.facilityId);
        const now = new Date().toISOString();
        incoming.updatedAt = now;
        if (idx === -1) {
          incoming.createdAt = incoming.createdAt || now;
          list.push(incoming);
        } else {
          incoming.createdAt = list[idx].createdAt || now;
          list[idx] = incoming;
        }
        writeFacilities(list);
        return sendJson(res, 200, { ok: true, facility: incoming });
      } catch (err) {
        return sendJson(res, 400, { error: err.message });
      }
    });
    return;
  }

  if (req.method === 'GET') {
    // Generic static file serving, restricted to plain top-level filenames
    // (no subdirectories, no traversal) so it can also serve downloadable
    // assets like the vendor census instruction PDFs.
    const requestedName = u.pathname === '/' ? 'index.html' : decodeURIComponent(u.pathname).slice(1);
    if (requestedName.includes('/') || requestedName.includes('\\') || requestedName.includes('..')) {
      res.writeHead(400, { 'Content-Type': 'text/plain' });
      return res.end('Bad request');
    }
    const filePath = path.join(ROOT, requestedName);
    fs.readFile(filePath, (err, data) => {
      if (err) {
        res.writeHead(404, { 'Content-Type': 'text/plain' });
        return res.end('Not found');
      }
      const ext = path.extname(filePath).toLowerCase();
      const headers = { 'Content-Type': MIME_TYPES[ext] || 'application/octet-stream' };
      if (ext === '.pdf') headers['Content-Disposition'] = 'inline; filename="' + requestedName + '"';
      res.writeHead(200, headers);
      res.end(data);
    });
    return;
  }

  res.writeHead(404, { 'Content-Type': 'text/plain' });
  res.end('Not found');
});

server.listen(PORT, () => {
  console.log(`NHSN Link Onboarding POC running at http://localhost:${PORT}`);
  console.log(`Facility data file: ${DATA_FILE}`);
});
