import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, throwError } from 'rxjs';
import { catchError } from 'rxjs/operators';

/** Base URL consumed by authInterceptor-augmented requests. */
const BASE_URL = 'http://localhost:5000/api';

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);

  // Auth header is injected by authInterceptor — no manual headers needed.

  get<T>(path: string): Observable<T> {
    return this.http.get<T>(`${BASE_URL}/${path}`).pipe(catchError(handleError));
  }

  post<T>(path: string, body: unknown): Observable<T> {
    return this.http.post<T>(`${BASE_URL}/${path}`, body).pipe(catchError(handleError));
  }

  put<T>(path: string, body: unknown): Observable<T> {
    return this.http.put<T>(`${BASE_URL}/${path}`, body).pipe(catchError(handleError));
  }

  delete<T>(path: string): Observable<T> {
    return this.http.delete<T>(`${BASE_URL}/${path}`).pipe(catchError(handleError));
  }
}

function handleError(error: unknown): Observable<never> {
  return throwError(() => error);
}
