const common = require('./webpack.common');

module.exports = {
  ...common,
  entry: './src/web-component/register.tsx',
  output: {
    ...common.output,
    filename: 'embed/nhsn-link.js',
    clean: false,
    library: {
      name: 'NhsnLinkEmbed',
      type: 'umd'
    },
    globalObject: 'this'
  }
};