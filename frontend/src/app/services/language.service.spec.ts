import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { LanguageService } from './language.service';

describe('LanguageService', () => {
  const originalLanguages = Object.getOwnPropertyDescriptor(Navigator.prototype, 'languages');
  const originalLanguage = Object.getOwnPropertyDescriptor(Navigator.prototype, 'language');

  afterEach(() => {
    localStorage.clear();
    if (originalLanguages) {
      Object.defineProperty(Navigator.prototype, 'languages', originalLanguages);
    }
    if (originalLanguage) {
      Object.defineProperty(Navigator.prototype, 'language', originalLanguage);
    }
    TestBed.resetTestingModule();
  });

  it('uses English when there is no stored preference and the system language is unsupported', () => {
    mockNavigatorLanguages(['fr-FR']);
    const service = createService();

    expect(service.language()).toBe('en');
    expect(service.t('Sign In')).toBe('Sign In');
  });

  it('uses Spanish when the system language starts with es', () => {
    mockNavigatorLanguages(['es-MX', 'en-US']);
    const service = createService();

    expect(service.language()).toBe('es');
    expect(service.t('Sign In')).toBe('Iniciar sesion');
  });

  it('prefers the manual localStorage selection over the system language', () => {
    localStorage.setItem('CHRONIQ_language', 'en');
    mockNavigatorLanguages(['es-MX']);
    const service = createService();

    expect(service.language()).toBe('en');
  });

  it('falls back to English for an invalid stored language', () => {
    localStorage.setItem('CHRONIQ_language', 'de');
    mockNavigatorLanguages(['es-MX']);
    const service = createService();

    expect(service.language()).toBe('en');
  });

  it('stores manual language changes for future sessions', () => {
    mockNavigatorLanguages(['en-US']);
    const service = createService();

    service.setLanguage('es');

    expect(localStorage.getItem('CHRONIQ_language')).toBe('es');
    expect(service.t('Logout')).toBe('Cerrar sesion');
  });

  function createService(): LanguageService {
    TestBed.configureTestingModule({
      providers: [{ provide: PLATFORM_ID, useValue: 'browser' }]
    });
    return TestBed.inject(LanguageService);
  }

  function mockNavigatorLanguages(languages: string[]): void {
    Object.defineProperty(Navigator.prototype, 'languages', {
      configurable: true,
      get: () => languages
    });
    Object.defineProperty(Navigator.prototype, 'language', {
      configurable: true,
      get: () => languages[0]
    });
  }
});
