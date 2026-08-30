import { ErrorHandler, Provider } from '@angular/core';
import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';
import { environment } from './environments/environment';

/**
 * Sentry is imported dynamically, and only when a DSN is configured.
 *
 * A static `import * as Sentry` adds its weight to the initial bundle whether or not error
 * reporting is ever switched on. A dynamic import puts it in its own chunk, fetched only by
 * deployments that actually use it, so the default build pays nothing for a feature it is not
 * using.
 *
 * Guarding on the DSN rather than always initializing also keeps development quiet: an
 * unconfigured `Sentry.init()` still installs global error and unhandled-rejection handlers, so
 * every local error would take a detour through Sentry's machinery before reaching the console
 * where it is being read.
 */
async function sentryProviders(): Promise<Provider[]> {
  if (!environment.sentryDsn) {
    return [];
  }

  const Sentry = await import('@sentry/angular');

  Sentry.init({
    dsn: environment.sentryDsn,
    environment: environment.production ? 'production' : 'development',

    // The default (1.0) sends a performance trace for every transaction. A POS runs a high volume
    // of small interactions, so that exhausts the quota quickly and then starts silently dropping
    // the errors too, which are the part actually worth having.
    tracesSampleRate: 0.1,

    // No names, emails or IP addresses leave the browser. This is a point-of-sale system: the
    // people using it are staff, and the data on screen is customer data.
    sendDefaultPii: false
  });

  return [{ provide: ErrorHandler, useValue: Sentry.createErrorHandler() }];
}

async function bootstrap(): Promise<void> {
  await bootstrapApplication(App, {
    ...appConfig,
    providers: [...(appConfig.providers ?? []), ...(await sentryProviders())]
  });
}

bootstrap().catch((err) => console.error(err));
