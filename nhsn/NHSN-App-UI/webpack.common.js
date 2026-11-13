const path = require('path');
const webpack = require('webpack');

/**
 * `@nhsn/nhsn-react-core` reads `process.env.ADMIN_URL` at module scope
 * (`dist/constants/constants.js`). Webpack 5 no longer shims Node globals, so
 * in the browser that throws `ReferenceError: process is not defined` before
 * any of our code runs.
 *
 * Production builds hid this: the constant is unused by us, so tree-shaking
 * dropped the module. Development builds do not tree-shake, so `npm start`
 * failed while `npm run build` succeeded.
 *
 * DefinePlugin substitutes at compile time rather than injecting a `process`
 * object — which matters for the embed build, where defining a global inside
 * the CDC NHSN App's page is not ours to do.
 *
 * Left `undefined` when unset so the package's own `?? 'http://localhost:3031'`
 * default still applies. We use no admin features, so the value is inert.
 */
const processEnvDefines = {
  'process.env.ADMIN_URL': process.env.ADMIN_URL
    ? JSON.stringify(process.env.ADMIN_URL)
    : 'undefined'
};

module.exports = {
  resolve: {
    extensions: ['.tsx', '.ts', '.js']
  },
  module: {
    rules: [
      {
        test: /\.(ts|tsx)$/,
        exclude: /node_modules/,
        use: {
          loader: 'ts-loader'
        }
      },
      {
        test: /\.css$/,
        use: ['style-loader', 'css-loader']
      },
      {
        test: /\.scss$/,
        use: ['style-loader', 'css-loader', 'sass-loader']
      }
    ]
  },
  output: {
    path: path.resolve(__dirname, 'dist')
  },
  plugins: [new webpack.DefinePlugin(processEnvDefines)]
};
