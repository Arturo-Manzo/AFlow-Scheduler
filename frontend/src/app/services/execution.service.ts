import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';
import { ApiResponse, BoxRun, TaskExecution, ExecuteBoxRequest, TaskExecutionLogEntry, BoxRunMetricsDto } from '../models/models';

@Injectable({ providedIn: 'root' })
export class ExecutionService {
  constructor(private api: ApiService) {}

  getBoxRuns(limit = 100): Observable<BoxRun[]> {
    return this.api
      .get<ApiResponse<BoxRun[]>>(`box-runs?limit=${limit}`)
      .pipe(map(response => response.data));
  }

  getBoxRun(boxRunId: number): Observable<BoxRun> {
    return this.api
      .get<ApiResponse<BoxRun>>(`box-runs/${boxRunId}`)
      .pipe(map(response => response.data));
  }

  getBoxRunMetrics(boxRunId: number): Observable<BoxRunMetricsDto> {
    return this.api
      .get<ApiResponse<BoxRunMetricsDto>>(`box-runs/${boxRunId}/metrics`)
      .pipe(map(response => response.data));
  }

  getBoxRunTasks(boxRunId: number): Observable<TaskExecution[]> {
    return this.api
      .get<ApiResponse<TaskExecution[]>>(`box-runs/${boxRunId}/tasks`)
      .pipe(map(response => response.data));
  }

  getTaskExecutionLogs(taskExecutionId: number): Observable<TaskExecutionLogEntry[]> {
    return this.api
      .get<ApiResponse<TaskExecutionLogEntry[]>>(`task-executions/${taskExecutionId}/logs`)
      .pipe(map(response => response.data));
  }

  getBoxRunLogs(boxRunId: number): Observable<TaskExecutionLogEntry[]> {
    return this.api
      .get<ApiResponse<TaskExecutionLogEntry[]>>(`box-runs/${boxRunId}/logs`)
      .pipe(map(response => response.data));
  }

  resumeBoxRun(boxRunId: number): Observable<void> {
    return this.api
      .post<ApiResponse<object>>(`box/${boxRunId}/resume`, {})
      .pipe(map(() => void 0));
  }

  cancelBoxRun(boxRunId: number): Observable<void> {
    return this.api
      .post<ApiResponse<object>>(`box/${boxRunId}/cancel`, {})
      .pipe(map(() => void 0));
  }

  runBox(boxId: number, reason: string): Observable<void> {
    const request: ExecuteBoxRequest = {
      ignoreDependencies: false,
      ignoreSchedule: false,
      reason
    };

    return this.api
      .post<ApiResponse<object>>(`box/${boxId}/run`, request)
      .pipe(map(() => void 0));
  }
}
