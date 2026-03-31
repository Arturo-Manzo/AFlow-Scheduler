import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';
import { ApiResponse, ExecutionDto, RunningExecutionDto } from '../models/models';

@Injectable({ providedIn: 'root' })
export class LogsService {
  constructor(private api: ApiService) {}

  getLatest(limit = 20): Observable<ExecutionDto[]> {
    return this.api
      .get<ApiResponse<ExecutionDto[]>>(`executionhistory/latest?limit=${limit}`)
      .pipe(map(r => r.data));
  }

  getForTask(taskId: number, fromUtc?: string, toUtc?: string): Observable<ExecutionDto[]> {
    const query = new URLSearchParams();
    if (fromUtc) query.set('fromUtc', fromUtc);
    if (toUtc) query.set('toUtc', toUtc);
    const suffix = query.toString() ? `?${query.toString()}` : '';

    return this.api
      .get<ApiResponse<ExecutionDto[]>>(`executionhistory/task/${taskId}${suffix}`)
      .pipe(map(r => r.data));
  }

  getLastForTask(taskId: number): Observable<ExecutionDto> {
    return this.api
      .get<ApiResponse<ExecutionDto>>(`executionhistory/task/${taskId}/last`)
      .pipe(map(r => r.data));
  }

  getRunning(): Observable<RunningExecutionDto[]> {
    return this.api
      .get<ApiResponse<RunningExecutionDto[]>>('executionhistory/running')
      .pipe(map(r => r.data));
  }
}
