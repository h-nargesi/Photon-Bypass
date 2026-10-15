// Karma configuration for Photon-Bypass.
// Mirrors the Angular builder's built-in config (see
// @angular-devkit/build-angular/src/builders/karma/index.js, getBuiltInKarmaConfig)
// with one difference: karma-firefox-launcher is added and FirefoxHeadless is
// the default browser, so `yarn test` runs headless on any machine with a
// native Firefox and needs no CHROME_BIN or browser download. Chrome launcher
// stays registered for interactive watch runs (`--browsers=Chrome`).

process.on('SIGINT', () => process.exit(0));

module.exports = function (config) {
  config.set({
    basePath: '',
    frameworks: ['jasmine', '@angular-devkit/build-angular'],
    plugins: [
      require('karma-jasmine'),
      require('karma-chrome-launcher'),
      require('karma-jasmine-html-reporter'),
      require('karma-coverage'),
      require('karma-firefox-launcher'),
      require('@angular-devkit/build-angular/plugins/karma')
    ],
    client: {
      jasmine: {},
      clearContext: false
    },
    jasmineHtmlReporter: {
      suppressAll: true
    },
    coverageReporter: {
      dir: require('path').join(__dirname, './coverage/photon-bypass'),
      subdir: '.',
      reporters: [{ type: 'html' }, { type: 'text-summary' }]
    },
    reporters: ['progress', 'kjhtml'],
    browsers: ['FirefoxHeadless'],
    customLaunchers: {
      // Kept for containerized environments that run Chrome without a sandbox.
      ChromeHeadlessNoSandbox: {
        base: 'ChromeHeadless',
        flags: ['--no-sandbox', '--headless', '--disable-gpu', '--disable-dev-shm-usage']
      }
    },
    restartOnFileChange: true
  });
};
