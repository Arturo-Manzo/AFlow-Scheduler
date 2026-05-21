import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { App } from './app';
import { AuthService } from './services/auth.service';
import { LanguageService } from './services/language.service';

describe('App', () => {
  beforeEach(async () => {
    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: (query: string) => ({
        matches: false,
        media: query,
        onchange: null,
        addListener: () => undefined,
        removeListener: () => undefined,
        addEventListener: () => undefined,
        removeEventListener: () => undefined,
        dispatchEvent: () => false
      })
    });

    await TestBed.configureTestingModule({
      imports: [App],
      providers: [provideRouter([])]
    }).compileComponents();
  });

  it('should create the app', () => {
    const fixture = TestBed.createComponent(App);
    const app = fixture.componentInstance;
    expect(app).toBeTruthy();
  });

  it('should render the user language selector and update labels without logout', async () => {
    const fixture = TestBed.createComponent(App);
    const auth = TestBed.inject(AuthService);
    const i18n = TestBed.inject(LanguageService);
    auth.currentUser.set({
      userId: 1,
      username: 'admin',
      email: 'admin@example.com',
      roleId: 1,
      roleName: 'Admin',
      isActive: true,
      createdAt: new Date().toISOString()
    });
    fixture.detectChanges();

    fixture.componentInstance.menuOpen.set(true);
    fixture.detectChanges();
    await fixture.whenStable();
    const compiled = fixture.nativeElement as HTMLElement;
    expect(compiled.textContent).toContain('Language / Idioma');

    i18n.setLanguage('es');
    fixture.detectChanges();

    expect(compiled.textContent).toContain('Cambiar contrasena');
    expect(auth.currentUser()).toBeTruthy();
  });
});
