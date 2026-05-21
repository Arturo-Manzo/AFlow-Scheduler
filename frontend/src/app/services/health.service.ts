import { Injectable, inject } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiResponse, HealthDashboardDto } from '../models/models';
import { ApiService } from './api.service';

@Injectable({ providedIn: 'root' })
export class HealthService {
  private api = inject(ApiService);

  getDashboard(hours = 24, limit = 50): Observable<HealthDashboardDto> {
    return this.api
      .get<ApiResponse<HealthDashboardDto>>(`health-dashboard?hours=${hours}&limit=${limit}`)
      .pipe(map(response => response.data));
  }
}
