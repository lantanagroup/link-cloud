const blockPathMatchers = [
  '/viewcode.asp',
  /^\/viewcode\.asp$/i,         // example regex form (either style is fine)
  /^\/.*\.(asp|php|aspx)$/i,    // block whole extensions

  // ---- CMS: WordPress ----
  "/wp-admin/",
  "/wp-content/",
  "/wp-includes/",
  "/wp-json/",
  "/readme.html",
  "/license.txt",
  /^\/\.wp-config\.php\.(?:swp|swo|bak|old|save|tmp)$/i,

  // ---- CMS: Joomla ----
  "/administrator/",
  "/components/",
  "/modules/",
  "/templates/",

  // ---- CMS: Drupal ----
  "/user/login",
  "/core/",
  "/sites/default/",
  "/sites/all/",
  "/modules/",

  // ---- Env / VCS / config leaks ----
  /^\/\.env(?:\.[\w-]+)?$/i,
  "/.git/config",
  "/.gitignore",
  /^\/\.git\/?/i,
  /^\/\.(?:svn|hg)\/?/i,
  "/.DS_Store",
  "/config.json",
  "/settings.py",

  // ---- Framework probes ----
  // Laravel
  /^\/vendor\/?/i,
  "/artisan",
  "/_ignition/execute-solution",
  "/storage/logs/",

  // Rails
  "/rails/info",
  "/rails/mailers",
  "/config/database.yml",

  // Django / Python
  "/__debug__/",
  "/manage.py",

  // Node / JS
  /^\/node_modules\/?/i,
  "/package.json",
  "/yarn.lock",

  // ---- Backups & dumps ----
  /^\/(?:backup|backups)\/?/i,
  /^\/(?:dump|database|db|site)\.sql$/i,
  /^\/(?:site|www)\.(?:zip|tar|tar\.gz|tgz)$/i,
  /^\/old\/?/i,
  /^\/.+\.(?:bak|old|save|swp|swo|tmp|~)$/i,
  /^\/.+\.(?:php|asp|aspx|jsp)\.(?:bak|old|save|swp|swo|tmp|~)$/i,

  // ---- Admin/auth guessing ----
  /^\/(?:admin|administrator|login|signin|auth|dashboard|webadmin|console)\/?$/i,
  "/cpanel/",

  // ---- Server/hosting artifacts ----
  "/server-status",
  "/server-info",
  "/.htaccess",
  "/.htpasswd",
  "/nginx.conf",
  "/apache.conf",
  "/httpd.conf",

  // ---- API / debug tooling ----
  /^\/api(?:\/v\d+)?\/?/i,
  "/swagger",
  "/swagger-ui",
  "/openapi.json",
  "/graphql",
  "/graphiql",
  "/debug",
  "/debugbar",

  // ---- Cloud / container metadata ----
  /^\/\.?latest\/meta-data\/?/i,
  /^\/meta-data\/?/i,
  /^\/v1\/metadata\/?/i,
  "/.dockerenv",
  "/docker-compose.yml",
  "/kubernetes/",

  // ---- Webshell / malware-ish filenames ----
  /^\/(?:shell|cmd|upload|uploader|backdoor|wso|r57)\.php$/i,

  // ---- Misc high-noise ----
  "/test/",
  "/tmp/",
  "/logs/",
  "/error.log",
  "/access.log",
];

module.exports = { blockPathMatchers };
