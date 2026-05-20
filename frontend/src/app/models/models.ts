// API response models matching the backend DTOs

export interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  errorCode?: string;
}

export interface PaginatedResponse<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
  totalPages: number;
}

// --- Auth ---
export interface LoginRequest {
  username: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  user: UserDto;
}

// --- Users ---
export interface UserDto {
  userId: number;
  username: string;
  email: string;
  roleId: number;
  roleName: string;
  isActive: boolean;
  departmentId?: number;
  departmentName?: string;
  createdAt: string;
}

export interface CreateUserRequest {
  username: string;
  email: string;
  password: string;
  roleId: number;
}

export interface UpdateUserRequest {
  email: string;
  roleId: number;
  isActive: boolean;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

// --- Boxes ---
export interface BoxDto {
  boxId: number;
  name: string;
  description: string;
  cronExpression: string;
  timeZoneId: string;
  enabled: boolean;
  lastRunUtc?: string;
  createdAt: string;
  notificationEmail?: string;
  departmentId?: number;
  departmentName?: string;
  tasks: TaskDto[];
}

export interface InitialTaskRequest {
  name: string;
  description: string;
  command: string;
  taskType: string;
}

export interface CreateBoxRequest {
  name: string;
  description: string;
  cronExpression: string;
  timeZoneId: string;
  notificationEmail?: string;
  departmentId?: number;
  initialTask: InitialTaskRequest;
}

export interface UpdateBoxRequest {
  name: string;
  description: string;
  cronExpression: string;
  timeZoneId: string;
  enabled: boolean;
  notificationEmail?: string;
  departmentId?: number;
}

export interface ExecuteBoxRequest {
  ignoreDependencies: boolean;
  ignoreSchedule: boolean;
  reason: string;
}

// --- Tasks ---
export interface TaskDto {
  taskId: number;
  boxId: number;
  name: string;
  description: string;
  command: string;
  taskType: string;
  enabled: boolean;
  createdAt: string;
  lastExecutionStatus?: string;
  lastExecutionAtUtc?: string;
  dependencyTaskIds: number[];
}

export type SearchScope = 'all' | 'box' | 'task';

export interface SearchResultDto {
  resultType: 'box' | 'task' | string;
  boxId: number;
  taskId?: number;
  title: string;
  description: string;
  boxName: string;
  command: string;
  taskType: string;
  timeZoneId: string;
  createdAt: string;
  enabled: boolean;
  boxEnabled: boolean;
  activeTaskCount: number;
}

export interface CreateTaskRequest {
  boxId: number;
  name: string;
  description: string;
  command: string;
  taskType: string;
  dependencyTaskIds: number[];
}

export interface UpdateTaskRequest {
  name: string;
  description: string;
  command: string;
  taskType: string;
  enabled: boolean;
  dependencyTaskIds: number[];
}

export interface ForceStartTaskRequest {
  reason: string;
}

// --- Executions ---
export interface ExecutionDto {
  executionId: number;
  taskId: number;
  taskName: string;
  taskType: string;
  command: string;
  boxId: number;
  boxName: string;
  boxTimeZoneId: string;
  departmentName?: string;
  failureAlertEmail?: string;
  boxRunId?: number;   // null for ForceStart executions
  startedAt: string;
  endedAt?: string;
  status: string;
  exitCode?: number;
  stdOut: string;
  stdErr: string;
  durationSeconds?: number;
  triggerSource: string;
  reason?: string;
  requestedByUserId?: number;
  requestedByUsername?: string;
  errorMessage?: string;
  isStale: boolean;
}

export interface RunningExecutionDto extends ExecutionDto {}

export interface SystemStatus {
  apiOnline: boolean;
  dbConnected: boolean;
  activeWorkers: number;
  totalWorkers: number;
  runningBoxRuns: number;
  runningExecutions: number;
  staleExecutions: number;
  staleExecutionThresholdMinutes: number;
  queueDepth: number;
  failNotificationEnabled: boolean;
  backendVersion: string;
  autoRecoveryEnabled: boolean;
  startupRecoveryCompleted: boolean;
  lastRecoveryCompletedAtUtc?: string;
  lastRecoveredExecutionCount: number;
  lastRecoveredBoxRunCount: number;
  environment: string;
}

export type BoxRunStatus = 'Pending' | 'Running' | 'Stopping' | 'Completed' | 'Partial' | 'Failed' | 'Cancelled';

export type TaskExecutionStatus = 'Pending' | 'Running' | 'Success' | 'Failed' | 'Skipped';

export interface BoxRunDto {
  id: number;
  boxId: number;
  boxName: string;
  status: BoxRunStatus;
  isCancellationRequested?: boolean;
  startTime: string;
  endTime?: string;
  scheduledForUtc?: string;
  triggerSource: string;
  durationSeconds?: number;
}

export interface TaskMetricDto {
  taskId: number;
  name: string;
  status: string;
  duration?: string;
  durationSeconds?: number;
}

export interface BoxRunMetricsDto {
  boxRunId: number;
  totalTasks: number;
  successCount: number;
  failedCount: number;
  pendingCount: number;
  totalDuration?: string;
  totalDurationSeconds?: number;
  successRate: number;
  tasks: TaskMetricDto[];
}

export interface BoxRunTaskExecutionDto {
  executionId?: number;
  taskId: number;
  name: string;
  status: TaskExecutionStatus;
  startTime?: string;
  endTime?: string;
  durationSeconds?: number;
  error?: string;
  stackTrace?: string;
  dependsOn: number[];
}

export interface TaskExecutionLogDto {
  id: string;
  boxRunId?: number;
  taskId: number;
  taskExecutionId: number;
  timestamp: string;
  level: 'Info' | 'Warning' | 'Error' | string;
  message: string;
  details?: string;
}

// --- Departments ---
export enum RetryPolicy {
  RequireApproval = 0,
  Auto = 1,
  ManualOnly = 2
}

export interface DepartmentDto {
  departmentId: number;
  name: string;
  description?: string;
  contactEmail: string;
  retryPolicy?: RetryPolicy;
  logRetentionDays: number;
  createdAt: string;
  updatedAt: string;
}

export interface CreateDepartmentRequest {
  name: string;
  description?: string;
  contactEmail: string;
  retryPolicy?: RetryPolicy;
  logRetentionDays: number;
}

export interface UpdateDepartmentRequest {
  name: string;
  description?: string;
  contactEmail: string;
  retryPolicy?: RetryPolicy;
  logRetentionDays: number;
}

// --- Notification Settings ---
export interface SmtpNotificationSettingsDto {
  enabled: boolean;
  host: string;
  port: number;
  username: string;
  hasPassword: boolean;
  fromAddress: string;
  fromDisplayName: string;
  enableSsl: boolean;
}

export interface UpdateSmtpNotificationSettingsRequest {
  enabled: boolean;
  host: string;
  port: number;
  username: string;
  password?: string;
  fromAddress: string;
  fromDisplayName: string;
  enableSsl: boolean;
}

export interface TestSmtpNotificationRequest {
  testRecipientEmail: string;
}

export interface SmtpTestResultDto {
  success: boolean;
  message: string;
  durationMs: number;
}
