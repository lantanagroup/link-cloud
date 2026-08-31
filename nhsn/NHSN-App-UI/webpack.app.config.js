const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const common = require('./webpack.common');

module.exports = {
  ...common,
  // The only entry permitted to reach shell/. Global styles are left
  // unscoped here — the shell owns its page, unlike the embed build.
  entry: './src/app/main.tsx',
  output: {
    ...common.output,
    filename: '[name].[contenthash].js',
    clean: true,
    publicPath: '/'
  },
  devServer: {
    historyApiFallback: true,
    static: {
      directory: path.resolve(__dirname, 'dist')
    },
    proxy: [
      {
        context: ['/api'],
        target: 'http://localhost:8079',
        changeOrigin: true
      }
    ]
  },
  // Spread common's plugins — assigning a fresh array would silently drop
  // the DefinePlugin that keeps `process.env` out of the browser.
  plugins: [
    ...common.plugins,
    new HtmlWebpackPlugin({
      template: './src/app/index.html',
      filename: 'index.html'
    })
  ]
};