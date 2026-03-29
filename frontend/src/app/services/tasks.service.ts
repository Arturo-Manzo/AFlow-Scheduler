import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';
import { ApiResponse, TaskDto, CreateTaskRequest, UpdateTaskRequest } from '../models/models';

@Injectable({ providedIn: 'root' })
export class TasksService {
  constructor(private api: ApiService) {}

  getForBox(boxId: number): Observable<TaskDto[]> {
    return this.api.get<ApiResponse<TaskDto[]>>('tasks?boxId=' + boxId).pipe(map(r => r.data));
  }

  getById(id: number): Observable<TaskDto> {
    return this.api.get<ApiResponse<TaskDto>>('tasks/' + id).pipe(map(r => r.data));
  }

  create(request: CreateTaskRequest): Observable<TaskDto> {
    return this.api.post<ApiResponse<TaskDto>>('tasks', request).pipe(map(r => r.data));
  }

  update(id: number, request: UpdateTaskRequest): Observable<TaskDto> {
    return this.api.put<ApiResponse<TaskDto>>('tasks/' + id, request).pipe(map(r => r.data));
  }

  delete(id: number): Observable<void> {
    return this.api.delete<ApiResponse<void>>('tasks/' + id).pipe(map(() => void 0));
  }
}
