import { mergeApplicationConfig, ApplicationConfig, signal } from '@angular/core';
import { provideServerRendering, withRoutes } from '@angular/ssr';
import { appConfig } from './app.config';
import { serverRoutes } from './app.routes.server';
import { ThemeMode, ThemeService } from 'ui-design-system';

/** SSR stub: must expose `mode` as a WritableSignal like ThemeService so templates can call `themeMode()`. */
class NoOpThemeService {
  readonly mode = signal<ThemeMode>('system');
  readonly activeTheme = signal<'light' | 'dark'>('light');
  init(): void {}
  setMode(m: ThemeMode): void {
    this.mode.set(m);
  }
}

const serverConfig: ApplicationConfig = {
  providers: [
    provideServerRendering(withRoutes(serverRoutes)),
    {
      provide: ThemeService,
      useClass: NoOpThemeService
    }
  ]
};

export const config = mergeApplicationConfig(appConfig, serverConfig);
