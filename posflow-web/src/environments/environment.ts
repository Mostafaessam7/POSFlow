export const environment = {
  production: true,

  // Empty = same-origin relative requests ('/api/...'), which is
  // correct when the Angular app is served from the same host as the
  // API (e.g. behind the same reverse proxy). Set this to a full
  // origin (e.g. 'https://api.yourshop.com') if the frontend and API
  // are deployed on different domains.
  apiBaseUrl: '',

  // Fill in the project DSN to enable Sentry error reporting. Empty means Sentry never
  // initializes and nothing leaves the browser -- see main.ts.
  sentryDsn: ''
};
