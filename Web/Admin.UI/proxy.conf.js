module.exports = [
  {
    context: ['/proxy-api/**'],
    target: process.env['BASE_API_URL'] || 'http://localhost:5218',
    secure: false,
    changeOrigin: true
  }
];
