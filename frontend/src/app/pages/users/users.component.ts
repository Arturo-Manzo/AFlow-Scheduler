import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ApiService } from '../../services/api.service';
import { AuthService } from '../../services/auth.service';
import {
  ApiResponse,
  CreateUserRequest,
  PaginatedResponse,
  UpdateUserRequest,
  UserDto
} from '../../models/models';
import { detectUserTimeZone, formatUtcInTimeZone } from '../../shared/timezone-utils';
import { isFieldInvalid } from '../../shared/form-utils';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="view-shell">
      <div class="view-hero">
        <div class="view-hero-main">
          <div class="view-eyebrow">Security And Access</div>
          <h1>{{ auth.isAdmin ? 'Users' : 'Account' }}</h1>
          <p class="view-description">
            {{ auth.isAdmin ? 'Manage access, roles and activation state for platform users.' : 'Review your account context and use the shell menu to change credentials.' }}
          </p>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ auth.isAdmin ? users().length : 1 }}</span>
          <span class="kpi-label">{{ auth.isAdmin ? 'Known Users' : 'Account' }}</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ auth.isAdmin ? activeUsers() : '--' }}</span>
          <span class="kpi-label">Active</span>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ auth.isAdmin ? adminUsers() : auth.currentUser()!.roleName }}</span>
          <span class="kpi-label">{{ auth.isAdmin ? 'Administrators' : 'Current Role' }}</span>
        </div>
      </div>

      @if (!auth.isAdmin) {
        <div class="account-card">
          <h3>Account</h3>
          <p class="account-copy">Use the account menu in the sidebar footer to change your password or sign out.</p>
        </div>
      }

      @if (auth.isAdmin) {
        <section class="data-panel">
          <div class="panel-header">
            <div class="panel-title-wrap">
              <div class="panel-title">User Directory</div>
              <div class="panel-subtitle">Current access registry grouped by identity, role and lifecycle state.</div>
            </div>
            <div class="panel-toolbar">
              <button class="btn btn-primary" (click)="openCreate()">
                <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.5"><line x1="12" y1="5" x2="12" y2="19"/><line x1="5" y1="12" x2="19" y2="12"/></svg>
                New User
              </button>
            </div>
          </div>

          @if (loadError()) {
            <div class="panel-body"><div class="alert alert-danger">{{ loadError() }}</div></div>
          }

          @if (loading()) {
            <div class="loading-state"><span class="spinner"></span> Loading users...</div>
          } @else if (users().length === 0 && !loadError()) {
            <p class="empty-state">No users found.</p>
          } @else {
            <table class="data-table">
              <thead>
                <tr>
                  <th>Username</th>
                  <th>Email</th>
                  <th>Role</th>
                  <th>Active</th>
                  <th>Created</th>
                  <th>Actions</th>
                </tr>
              </thead>
              <tbody>
                @for (user of users(); track user.userId) {
                  <tr>
                    <td><strong>{{ user.username }}</strong></td>
                    <td>{{ user.email }}</td>
                    <td><span class="badge badge-neutral">{{ user.roleName }}</span></td>
                    <td>
                      <span [class]="'badge ' + (user.isActive ? 'badge-success' : 'badge-danger')">
                        {{ user.isActive ? 'Active' : 'Inactive' }}
                      </span>
                    </td>
                    <td>{{ formatCreatedAt(user.createdAt) }}</td>
                    <td class="table-actions">
                      <button class="btn btn-sm" (click)="openEdit(user)">Edit</button>
                    </td>
                  </tr>
                }
              </tbody>
            </table>
          }
        </section>
      }

    @if (showForm()) {
      <div class="modal-overlay" role="dialog" aria-modal="true" [attr.aria-label]="editingUser() ? 'Edit user' : 'New user'">
        <div class="modal" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>{{ editingUser() ? 'Edit User' : 'New User' }}</h3>
            <button type="button" class="modal-close" (click)="closeForm()" aria-label="Close">x</button>
          </div>
          <form [formGroup]="form" (ngSubmit)="saveUser()" novalidate>
            <div class="modal-body">
              @if (!editingUser()) {
                <div class="field">
                  <label for="u-name">Username</label>
                  <input id="u-name" formControlName="username" autocomplete="username" [class.is-invalid]="fi('username')" />
                  @if (fi('username')) { <span class="field-hint">Username is required.</span> }
                </div>
                <div class="field">
                  <label for="u-pass">Password</label>
                  <input id="u-pass" type="password" formControlName="password" autocomplete="new-password" [class.is-invalid]="fi('password')" />
                  @if (fi('password')) {
                    @if (form.get('password')!.hasError('required')) {
                      <span class="field-hint">Password is required.</span>
                    } @else {
                      <span class="field-hint">Must be at least 8 characters.</span>
                    }
                  }
                </div>
              }

              <div class="field">
                <label for="u-email">Email</label>
                <input id="u-email" type="email" formControlName="email" autocomplete="email" [class.is-invalid]="fi('email')" />
                @if (fi('email')) {
                  @if (form.get('email')!.hasError('required')) {
                    <span class="field-hint">Email is required.</span>
                  } @else {
                    <span class="field-hint">Enter a valid email address.</span>
                  }
                }
              </div>

              <div class="field">
                <label for="u-role">Role</label>
                <select id="u-role" formControlName="roleId">
                  <option [value]="1">Admin</option>
                  <option [value]="2">Operator</option>
                  <option [value]="3">Viewer</option>
                </select>
              </div>

              @if (editingUser()) {
                <div class="field field-check">
                  <input type="checkbox" formControlName="isActive" id="u-active" />
                  <label for="u-active">Active</label>
                </div>
              }

              @if (formError()) { <div class="alert alert-danger">{{ formError() }}</div> }
            </div>

            <div class="modal-footer">
              <button type="button" class="btn" (click)="closeForm()">Cancel</button>
              <button type="submit" class="btn btn-primary" [disabled]="form.invalid || saving()">{{ saving() ? 'Saving...' : 'Save' }}</button>
            </div>
          </form>
        </div>
      </div>
    }
  `,
  styleUrl: './users.component.scss'
})
export class UsersComponent implements OnInit {
  private api = inject(ApiService);
  auth = inject(AuthService);
  private fb = inject(FormBuilder);

  readonly userTimeZone = detectUserTimeZone();

  users = signal<UserDto[]>([]);
  loading = signal(false);
  saving = signal(false);
  loadError = signal('');
  showForm = signal(false);
  editingUser = signal<UserDto | null>(null);
  formError = signal('');

  form = this.fb.group({
    username: [''],
    password: [''],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(100)]],
    roleId: [2, Validators.required],
    isActive: [true]
  });

  @HostListener('document:keydown.escape')
  onEscape(): void {
    if (this.showForm()) this.closeForm();
  }

  fi(field: string): boolean {
    return isFieldInvalid(this.form, field);
  }

  ngOnInit(): void {
    if (this.auth.isAdmin) {
      this.loadUsers();
    }
  }

  formatCreatedAt(value: string): string {
    return formatUtcInTimeZone(value, this.userTimeZone, { dateStyle: 'medium' });
  }

  loadUsers(): void {
    this.loading.set(true);
    this.loadError.set('');
    this.api.get<ApiResponse<PaginatedResponse<UserDto>>>('users').subscribe({
      next: (r) => {
        this.users.set(r.data.items);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set('Failed to load users.');
        this.loading.set(false);
      }
    });
  }

  openCreate(): void {
    this.editingUser.set(null);
    this.form.reset({ roleId: 2, isActive: true });
    const usernameCtrl = this.form.get('username')!;
    const passwordCtrl = this.form.get('password')!;
    usernameCtrl.setValidators([Validators.required, Validators.minLength(3), Validators.maxLength(50)]);
    passwordCtrl.setValidators([Validators.required, Validators.minLength(8)]);
    usernameCtrl.updateValueAndValidity();
    passwordCtrl.updateValueAndValidity();
    this.formError.set('');
    this.showForm.set(true);
  }

  openEdit(user: UserDto): void {
    this.editingUser.set(user);
    this.form.patchValue({ email: user.email, roleId: user.roleId, isActive: user.isActive });
    const usernameCtrl = this.form.get('username')!;
    const passwordCtrl = this.form.get('password')!;
    usernameCtrl.clearValidators();
    passwordCtrl.clearValidators();
    usernameCtrl.updateValueAndValidity();
    passwordCtrl.updateValueAndValidity();
    this.formError.set('');
    this.showForm.set(true);
  }

  closeForm(): void {
    this.showForm.set(false);
  }

  saveUser(): void {
    this.form.markAllAsTouched();
    if (this.form.invalid) return;

    this.saving.set(true);
    const v = this.form.value;
    const editing = this.editingUser();

    if (editing) {
      const req: UpdateUserRequest = { email: v.email!, roleId: Number(v.roleId), isActive: v.isActive ?? true };
      this.api.put<ApiResponse<UserDto>>(`users/${editing.userId}`, req).subscribe({
        next: () => {
          this.saving.set(false);
          this.closeForm();
          this.loadUsers();
        },
        error: () => {
          this.formError.set('Failed to update user.');
          this.saving.set(false);
        }
      });
    } else {
      const req: CreateUserRequest = {
        username: v.username!,
        email: v.email!,
        password: v.password!,
        roleId: Number(v.roleId)
      };
      this.api.post<ApiResponse<UserDto>>('users', req).subscribe({
        next: () => {
          this.saving.set(false);
          this.closeForm();
          this.loadUsers();
        },
        error: () => {
          this.formError.set('Failed to create user.');
          this.saving.set(false);
        }
      });
    }
  }

  activeUsers(): number {
    return this.users().filter(user => user.isActive).length;
  }

  adminUsers(): number {
    return this.users().filter(user => user.roleName === 'Admin').length;
  }

}
