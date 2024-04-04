module.exports = [
  {
    context: ['/proxy-api/**'],
    target: process.env['BASE_API_URL'] || 'http://localhost:7777',
    secure: false,
  }
];
