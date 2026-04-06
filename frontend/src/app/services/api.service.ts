import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { ErrorHandlerService } from './error-handler.service';

type RuntimeConfig = {
  backendUrl?: string;
};

/** Base URL consumed by authInterceptor-augmented requests. */
const FALLBACK_BASE_URL = '/api';

function resolveBaseUrl(): string {
  const runtimeConfig = globalThis.__CHRONIQ_RUNTIME_CONFIG__ as RuntimeConfig | undefined;
  const backendUrl = runtimeConfig?.backendUrl;

  if (!backendUrl) {
    return FALLBACK_BASE_URL;
  }

  const sanitized = backendUrl.trim().replace(/\/$/, '');
  return sanitized.length > 0 ? sanitized : FALLBACK_BASE_URL;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private errorHandler = inject(ErrorHandlerService);

  // Auth header is injected by authInterceptor — no manual headers needed.

  get<T>(path: string): Observable<T> {
    return this.http.get<T>(`${resolveBaseUrl()}/${path}`).pipe(catchError(err => this.handleError(err)));
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<T>(`${resolveBaseUrl()}/${path}`, body).pipe(catchError(err => this.handleError(err)));
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(`${resolveBaseUrl()}/${path}`, body).pipe(catchError(err => this.handleError(err)));
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${resolveBaseUrl()}/${path}`).pipe(catchError(err => this.handleError(err)));
  }

  private handleError(error: unknown): Observable<never> {
    if (error instanceof HttpErrorResponse) {
      const msg = error.error?.message || error.message || `HTTP ${error.status}`;
      // Don't toast 401 errors — the auth interceptor handles those
      if (error.status !== 401) {
        this.errorHandler.showError(msg);
      }
    } else {
      this.errorHandler.showError('An unexpected error occurred.');
    }
    return throwError(() => error);
  }
}
