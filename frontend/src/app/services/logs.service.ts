import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';
import { ApiResponse, ExecutionDto } from '../models/models';

@Injectable({ providedIn: 'root' })
export class LogsService {
  constructor(private api: ApiService) {}

  getLatest(limit = 20): Observable<ExecutionDto[]> {
    return this.api
      .get<ApiResponse<ExecutionDto[]>>(`executionhistory/latest?limit=${limit}`)
      .pipe(map(r => r.data));
  }

  getForTask(taskId: number): Observable<ExecutionDto[]> {
    return this.api
      .get<ApiResponse<ExecutionDto[]>>(`executionhistory/task/${taskId}`)
      .pipe(map(r => r.data));
  }
}
