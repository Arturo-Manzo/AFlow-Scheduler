import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';
import { ApiResponse, SearchResultDto, SearchScope } from '../models/models';

@Injectable({ providedIn: 'root' })
export class SearchService {
  constructor(private api: ApiService) {}

  search(query: string, scope: SearchScope, limit = 25): Observable<SearchResultDto[]> {
    const q = encodeURIComponent(query.trim());
    const normalizedScope = encodeURIComponent(scope);
    return this.api
      .get<ApiResponse<SearchResultDto[]>>(`search?q=${q}&scope=${normalizedScope}&limit=${limit}`)
      .pipe(map(response => response.data));
  }
}