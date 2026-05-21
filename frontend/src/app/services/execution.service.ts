import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';
import { ApiResponse, BoxRunDto, BoxRunTaskExecutionDto, ExecuteBoxRequest, TaskExecutionLogDto, BoxRunMetricsDto, ExecutionDto, RunningExecutionDto } from '../models/models';

@Injectable({ providedIn: 'root' })
export class ExecutionService {
  constructor(private api: ApiService) {}

  getBoxRuns(limit = 100, boxId?: number): Observable<BoxRunDto[]> {
    const query = boxId != null ? `box-runs?limit=${limit}&boxId=${boxId}` : `box-runs?limit=${limit}`;
    return this.api
      .get<ApiResponse<BoxRunDto[]>>(query)
      .pipe(map(response => response.data));
  }

  getBoxRun(boxRunId: number): Observable<BoxRunDto> {
    return this.api
      .get<ApiResponse<BoxRunDto>>(`box-runs/${boxRunId}`)
      .pipe(map(response => response.data));
  }

  getBoxRunMetrics(boxRunId: number): Observable<BoxRunMetricsDto> {
    return this.api
      .get<ApiResponse<BoxRunMetricsDto>>(`box-runs/${boxRunId}/metrics`)
      .pipe(map(response => response.data));
  }

  getBoxRunTasks(boxRunId: number): Observable<BoxRunTaskExecutionDto[]> {
    return this.api
      .get<ApiResponse<BoxRunTaskExecutionDto[]>>(`box-runs/${boxRunId}/tasks`)
      .pipe(map(response => response.data));
  }

  getTaskExecutionLogs(taskExecutionId: number): Observable<TaskExecutionLogDto[]> {
    return this.api
      .get<ApiResponse<TaskExecutionLogDto[]>>(`task-executions/${taskExecutionId}/logs`)
      .pipe(map(response => response.data));
  }

  getBoxRunLogs(boxRunId: number): Observable<TaskExecutionLogDto[]> {
    return this.api
      .get<ApiResponse<TaskExecutionLogDto[]>>(`box-runs/${boxRunId}/logs`)
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

  getExecutionsForTask(taskId: number): Observable<ExecutionDto[]> {
    return this.api
      .get<ApiResponse<ExecutionDto[]>>(`executionhistory/task/${taskId}`)
      .pipe(map(response => response.data));
  }

  getLatest(limit = 20): Observable<ExecutionDto[]> {
    return this.api
      .get<ApiResponse<ExecutionDto[]>>(`executionhistory/latest?limit=${limit}`)
      .pipe(map(r => r.data));
  }

  getRunning(): Observable<RunningExecutionDto[]> {
    return this.api
      .get<ApiResponse<RunningExecutionDto[]>>('executionhistory/running')
      .pipe(map(r => r.data));
  }
}
