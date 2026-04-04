import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';
import { ApiResponse, DepartmentDto, CreateDepartmentRequest, UpdateDepartmentRequest } from '../models/models';

/**
 * Service for managing departments and governance policies.
 */
@Injectable({ providedIn: 'root' })
export class DepartmentsService {
  constructor(private api: ApiService) {}

  /**
   * Gets all departments in the system.
   */
  getAll(): Observable<DepartmentDto[]> {
    return this.api.get<ApiResponse<DepartmentDto[]>>('departments').pipe(map(r => r.data));
  }

  /**
   * Gets a specific department by ID.
   */
  getById(id: number): Observable<DepartmentDto> {
    return this.api.get<ApiResponse<DepartmentDto>>(`departments/${id}`).pipe(map(r => r.data));
  }

  /**
   * Creates a new department.
   */
  create(request: CreateDepartmentRequest): Observable<DepartmentDto> {
    return this.api.post<ApiResponse<DepartmentDto>>('departments', request).pipe(map(r => r.data));
  }

  /**
   * Updates an existing department.
   */
  update(id: number, request: UpdateDepartmentRequest): Observable<DepartmentDto> {
    return this.api.put<ApiResponse<DepartmentDto>>(`departments/${id}`, request).pipe(map(r => r.data));
  }

  /**
   * Deletes a department.
   * Returns void if successful.
   */
  delete(id: number): Observable<void> {
    return this.api.delete<ApiResponse<void>>(`departments/${id}`).pipe(map(() => void 0));
  }

  /**
   * Gets the retry policy for a department.
   * Returns an object with Policy (string) and PolicyValue (number).
   */
  getRetryPolicy(id: number): Observable<{ Policy: string; PolicyValue: number }> {
    return this.api.get<ApiResponse<{ Policy: string; PolicyValue: number }>>(`departments/${id}/retry-policy`).pipe(map(r => r.data));
  }
}
