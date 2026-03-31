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
  initialTask: InitialTaskRequest;
}

export interface UpdateBoxRequest {
  name: string;
  description: string;
  cronExpression: string;
  timeZoneId: string;
  enabled: boolean;
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
  dependencyTaskIds: number[];
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
  boxId: number;
  boxName: string;
  boxTimeZoneId: string;
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
