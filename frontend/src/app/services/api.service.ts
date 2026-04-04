import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { environment } from '../../environments/environment';
import { ErrorHandlerService } from './error-handler.service';

/** Base URL consumed by authInterceptor-augmented requests. */
const BASE_URL = environment.apiUrl;

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private errorHandler = inject(ErrorHandlerService);

  // Auth header is injected by authInterceptor — no manual headers needed.

  get<T>(path: string): Observable<T> {
    return this.http.get<T>(`${BASE_URL}/${path}`).pipe(catchError(err => this.handleError(err)));
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<T>(`${BASE_URL}/${path}`, body).pipe(catchError(err => this.handleError(err)));
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(`${BASE_URL}/${path}`, body).pipe(catchError(err => this.handleError(err)));
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${BASE_URL}/${path}`).pipe(catchError(err => this.handleError(err)));
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
