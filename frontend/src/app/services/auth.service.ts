import { Injectable, signal, PLATFORM_ID, inject } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { Router } from '@angular/router';
import { LoginResponse, UserDto } from '../models/models';

const TOKEN_KEY = 'ascheduler_token';
const USER_KEY  = 'ascheduler_user';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private platformId = inject(PLATFORM_ID);
  private isBrowser  = isPlatformBrowser(this.platformId);

  readonly currentUser = signal<UserDto | null>(this.loadUser());

  get isAdmin(): boolean {
    return this.currentUser()?.roleName === 'Admin';
  }

  get isOperator(): boolean {
    const role = this.currentUser()?.roleName;
    return role === 'Admin' || role === 'Operator';
  }

  get isLoggedIn(): boolean {
    return !!this.getToken();
  }

  constructor(private router: Router) {}

  getToken(): string | null {
    return this.isBrowser ? localStorage.getItem(TOKEN_KEY) : null;
  }

  storeLogin(response: LoginResponse): void {
    if (this.isBrowser) {
      localStorage.setItem(TOKEN_KEY, response.accessToken);
      localStorage.setItem(USER_KEY, JSON.stringify(response.user));
    }
    this.currentUser.set(response.user);
  }

  logout(): void {
    if (this.isBrowser) {
      localStorage.removeItem(TOKEN_KEY);
      localStorage.removeItem(USER_KEY);
    }
    this.currentUser.set(null);
    this.router.navigate(['/login']);
  }

  private loadUser(): UserDto | null {
    if (!this.isBrowser) return null;
    try {
      const raw = localStorage.getItem(USER_KEY);
      return raw ? JSON.parse(raw) : null;
    } catch {
      return null;
    }
  }
}
