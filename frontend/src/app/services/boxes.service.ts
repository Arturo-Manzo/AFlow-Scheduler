import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';
import { ApiResponse, BoxDto, CreateBoxRequest, UpdateBoxRequest, ExecuteBoxRequest } from '../models/models';

@Injectable({ providedIn: 'root' })
export class BoxesService {
  constructor(private api: ApiService) {}

  getAll(): Observable<BoxDto[]> {
    return this.api.get<ApiResponse<BoxDto[]>>('boxes').pipe(map(r => r.data));
  }

  getById(id: number): Observable<BoxDto> {
    return this.api.get<ApiResponse<BoxDto>>(`boxes/${id}`).pipe(map(r => r.data));
  }

  create(request: CreateBoxRequest): Observable<BoxDto> {
    return this.api.post<ApiResponse<BoxDto>>('boxes', request).pipe(map(r => r.data));
  }

  update(id: number, request: UpdateBoxRequest): Observable<BoxDto> {
    return this.api.put<ApiResponse<BoxDto>>(`boxes/${id}`, request).pipe(map(r => r.data));
  }

  delete(id: number): Observable<void> {
    return this.api.delete<ApiResponse<void>>(`boxes/${id}`).pipe(map(() => void 0));
  }

  runNow(id: number, request: ExecuteBoxRequest): Observable<unknown> {
    return this.api.post<ApiResponse<unknown>>(`boxes/${id}/run`, request).pipe(map(r => r.data));
  }
}
