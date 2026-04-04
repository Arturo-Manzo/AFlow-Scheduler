import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { ApiService } from './api.service';
import {
  ApiResponse,
  SmtpNotificationSettingsDto,
  SmtpTestResultDto,
  TestSmtpNotificationRequest,
  UpdateSmtpNotificationSettingsRequest
} from '../models/models';

@Injectable({ providedIn: 'root' })
export class NotificationSettingsService {
  constructor(private api: ApiService) {}

  getSmtpSettings(): Observable<SmtpNotificationSettingsDto> {
    return this.api
      .get<ApiResponse<SmtpNotificationSettingsDto>>('notificationsettings/smtp')
      .pipe(map(response => response.data));
  }

  updateSmtpSettings(request: UpdateSmtpNotificationSettingsRequest): Observable<SmtpNotificationSettingsDto> {
    return this.api
      .put<ApiResponse<SmtpNotificationSettingsDto>>('notificationsettings/smtp', request)
      .pipe(map(response => response.data));
  }

  sendSmtpTest(request: TestSmtpNotificationRequest): Observable<SmtpTestResultDto> {
    return this.api
      .post<ApiResponse<SmtpTestResultDto>>('notificationsettings/smtp/test', request)
      .pipe(map(response => response.data));
  }
}