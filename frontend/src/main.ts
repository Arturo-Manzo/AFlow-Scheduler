import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { App } from './app/app';

type FrontendRuntimeConfig = {
  port?: number;
  backendUrl?: string;
};

const FALLBACK_BACKEND_URL = '/api';

async function loadFrontendRuntimeConfig(): Promise<void> {
  try {
    const response = await fetch('/config.json', { cache: 'no-store' });
    if (!response.ok) {
      return;
    }

    const config = (await response.json()) as FrontendRuntimeConfig;
    const sanitizedBackendUrl = sanitizeBackendUrl(config.backendUrl);

    if (!sanitizedBackendUrl) {
      return;
    }

    globalThis.__CHRONIQ_RUNTIME_CONFIG__ = {
      backendUrl: sanitizedBackendUrl,
      port: config.port,
    };
  } catch {
    // Ignore runtime config load errors and keep defaults.
  }
}

function sanitizeBackendUrl(backendUrl: string | undefined): string {
  if (!backendUrl) {
    return '';
  }

  const trimmed = backendUrl.trim();
  return trimmed.length > 0 ? trimmed.replace(/\/$/, '') : '';
}

globalThis.__CHRONIQ_RUNTIME_CONFIG__ = {
  backendUrl: FALLBACK_BACKEND_URL,
};

loadFrontendRuntimeConfig()
  .then(() => bootstrapApplication(App, appConfig))
  .catch((err) => console.error(err));
