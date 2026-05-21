import { Component, HostListener, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ButtonDirective } from 'ui-design-system';
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
import { TranslatePipe } from '../../shared/translate.pipe';

@Component({
  selector: 'app-users',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonDirective, TranslatePipe],
  templateUrl: './users.component.html',
  styles: [`
    .user-modal-field {
      display: flex;
      flex-direction: column;
      gap: 0.35rem;
    }

    .user-modal-label {
      color: var(--color-muted);
      font-size: 0.75rem;
      font-weight: 700;
      letter-spacing: 0.1em;
      line-height: 1rem;
      text-transform: uppercase;
    }

    .user-modal-error {
      color: var(--ui-danger-text);
      font-size: 0.75rem;
      line-height: 1rem;
      min-height: 1rem;
    }

    .user-modal-check {
      align-items: center;
      color: var(--color-muted);
      display: flex;
      gap: 0.5rem;
      min-height: 2.5rem;
    }
  `]
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
