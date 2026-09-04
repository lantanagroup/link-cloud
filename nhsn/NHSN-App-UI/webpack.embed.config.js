const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const common = require('./webpack.common');
const scopePlugin = require('./postcss.embed');

const postcssLoader = {
  loader: 'postcss-loader',
  options: {postcssOptions: {plugins: [scopePlugin()]}}
};

module.exports = {
  ...common,
  entry: './src/embed/register.tsx',
  // Only this build scopes its CSS — the shell owns its own page.
  module: {
    rules: [
      common.module.rules[0],
      {test: /\.css$/, use: ['style-loader', 'css-loader', postcssLoader]},
      {test: /\.scss$/, use: ['style-loader', 'css-loader', postcssLoader, 'sass-loader']}
    ]
  },
  output: {
    ...common.output,
    filename: 'embed/nhsn-link.js',
    // Steps are code-split, so the initial artifact carries the machine and
    // the first screen rather than all thirteen.
    chunkFilename: 'embed/nhsn-link.[name].[contenthash].js',
    clean: false,
    library: {
      name: 'NhsnLinkEmbed',
      type: 'umd'
    },
    globalObject: 'this'
  },
  performance: {
    // Left failing loudly rather than silenced: this artifact loads inside
    // someone else's page, so growth should be visible in the build output
    // instead of at integration.
    hints: 'warning',
    maxAssetSize: 2_500_000,
    maxEntrypointSize: 2_500_000
  },
  // Spread common's plugins — assigning a fresh array would silently drop
  // the DefinePlugin that keeps `process.env` out of the browser.
  plugins: [
    ...common.plugins,
    new HtmlWebpackPlugin({
      template: './src/embed/index.html',
      filename: 'embed/index.html',
      inject: 'body'
    })
  ],
  resolve: {
    ...common.resolve,
    alias: {
      // Fails the build if anything on the embed chain reaches shell code,
      // rather than silently bundling it. The boundary test covers what a
      // build-time alias cannot.
      [path.resolve(__dirname, 'src/shell')]: false
    }
  }
};
