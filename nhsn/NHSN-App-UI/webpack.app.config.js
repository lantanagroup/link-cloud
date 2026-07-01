const path = require('path');
const HtmlWebpackPlugin = require('html-webpack-plugin');
const common = require('./webpack.common');

module.exports = {
  ...common,
  entry: './src/app-shell/main.tsx',
  output: {
    ...common.output,
    filename: '[name].[contenthash].js',
    clean: true,
    publicPath: '/'
  },
  devServer: {
    historyApiFallback: true,
    static: {
      directory: path.resolve(__dirname, 'dist/app-shell')
    },
    proxy: [
      {
        context: ['/api'],
        target: 'http://localhost:8079',
        changeOrigin: true
      }
    ]
  },
  plugins: [
    new HtmlWebpackPlugin({
      template: './src/app-shell/index.html',
      filename: 'index.html'
    })
  ]
};