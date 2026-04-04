import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { DepartmentsService } from '../../services/departments.service';
import { DepartmentDto, CreateDepartmentRequest, UpdateDepartmentRequest } from '../../models/models';
import { isFieldInvalid } from '../../shared/form-utils';

@Component({
  selector: 'app-department-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  template: `
    <div class="view-shell">
      <div class="view-hero">
        <div class="view-hero-main">
          <div class="view-eyebrow">Administration</div>
          <h1>Departments</h1>
          <p class="view-description">
            Manage department units, ownership contact and log retention settings.
          </p>
        </div>
        <div class="view-hero-kpi">
          <span class="kpi-value">{{ loading() ? '--' : departments().length }}</span>
          <span class="kpi-label">Departments</span>
        </div>
      </div>

      <section class="data-panel">
        <div class="panel-header">
          <div class="panel-title-wrap">
            <div class="panel-title">Department Registry</div>
            <div class="panel-subtitle">Create and manage departments. The Default department cannot be edited or deleted.</div>
          </div>
          <div class="panel-toolbar">
            <button class="btn btn-primary" (click)="toggleCreateForm()">
              {{ showCreateForm() ? 'Cancel' : 'New Department' }}
            </button>
            <button class="btn" (click)="loadDepartments()">Refresh</button>
          </div>
        </div>

        @if (errorMessage()) {
          <div class="panel-body"><div class="alert alert-danger">{{ errorMessage() }}</div></div>
        }

        @if (showCreateForm()) {
          <div class="panel-body create-form-shell">
            <h4>New Department</h4>
            <form [formGroup]="createForm" (ngSubmit)="submitCreate()" novalidate>
              <div class="form-grid">
                <div class="field">
                  <label for="dept-name">Name <span class="req">*</span></label>
                  <input id="dept-name" formControlName="name" placeholder="e.g. Engineering, Finance" [class.is-invalid]="fi('name')" />
                  @if (fi('name')) { <span class="field-hint">Name is required and must be at least 3 characters.</span> }
                </div>
                <div class="field">
                  <label for="dept-contact">Contact Email <span class="req">*</span></label>
                  <input id="dept-contact" type="email" formControlName="contactEmail" placeholder="team-contact@company.com" [class.is-invalid]="fi('contactEmail')" />
                  @if (fi('contactEmail')) { <span class="field-hint">Contact email is required and must be valid.</span> }
                </div>
                <div class="field field-full">
                  <label for="dept-desc">Description</label>
                  <input id="dept-desc" formControlName="description" placeholder="Optional description" />
                </div>
                <div class="field">
                  <label for="dept-retention">Log Retention (days) <span class="req">*</span></label>
                  <input id="dept-retention" type="number" formControlName="logRetentionDays" min="1" [class.is-invalid]="fi('logRetentionDays')" />
                  @if (fi('logRetentionDays')) { <span class="field-hint">Must be at least 1 day.</span> }
                </div>
              </div>
              <div class="form-actions">
                <button type="button" class="btn" (click)="toggleCreateForm()">Cancel</button>
                <button type="submit" class="btn btn-primary" [disabled]="saving()">{{ saving() ? 'Creating...' : 'Create Department' }}</button>
              </div>
            </form>
          </div>
        }

        @if (showEditForm()) {
          <div class="panel-body create-form-shell">
            <h4>Edit Department</h4>
            <form [formGroup]="editForm" (ngSubmit)="submitEdit()" novalidate>
              <div class="form-grid">
                <div class="field">
                  <label for="dept-edit-name">Name <span class="req">*</span></label>
                  <input id="dept-edit-name" formControlName="name" placeholder="e.g. Engineering, Finance" [class.is-invalid]="editFieldInvalid('name')" />
                  @if (editFieldInvalid('name')) { <span class="field-hint">Name is required and must be at least 3 characters.</span> }
                </div>
                <div class="field">
                  <label for="dept-edit-contact">Contact Email <span class="req">*</span></label>
                  <input id="dept-edit-contact" type="email" formControlName="contactEmail" placeholder="team-contact@company.com" [class.is-invalid]="editFieldInvalid('contactEmail')" />
                  @if (editFieldInvalid('contactEmail')) { <span class="field-hint">Contact email is required and must be valid.</span> }
                </div>
                <div class="field field-full">
                  <label for="dept-edit-desc">Description</label>
                  <input id="dept-edit-desc" formControlName="description" placeholder="Optional description" />
                </div>
                <div class="field">
                  <label for="dept-edit-retention">Log Retention (days) <span class="req">*</span></label>
                  <input id="dept-edit-retention" type="number" formControlName="logRetentionDays" min="1" [class.is-invalid]="editFieldInvalid('logRetentionDays')" />
                  @if (editFieldInvalid('logRetentionDays')) { <span class="field-hint">Must be at least 1 day.</span> }
                </div>
              </div>
              <div class="form-actions">
                <button type="button" class="btn" (click)="closeEditForm()">Cancel</button>
                <button type="submit" class="btn btn-primary" [disabled]="saving()">{{ saving() ? 'Saving...' : 'Save Changes' }}</button>
              </div>
            </form>
          </div>
        }

        @if (loading()) {
          <div class="loading-state"><span class="spinner"></span> Loading departments...</div>
        } @else if (departments().length === 0 && !errorMessage()) {
          <p class="empty-state">No departments configured yet. Create one to get started.</p>
        } @else {
          <table class="data-table">
            <thead>
              <tr>
                <th>Name</th>
                <th>Description</th>
                <th>Contact Email</th>
                <th>Log Retention</th>
                <th>Created</th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              @for (dept of departments(); track dept.departmentId) {
                <tr>
                  <td><strong>{{ dept.name }}</strong></td>
                  <td class="desc-cell">{{ dept.description || '-' }}</td>
                  <td>{{ dept.contactEmail }}</td>
                  <td>{{ dept.logRetentionDays }}d</td>
                  <td>{{ dept.createdAt | date:'mediumDate' }}</td>
                  <td class="table-actions">
                    <button
                      class="btn btn-sm"
                      (click)="openEditForm(dept)"
                      [disabled]="dept.name === 'Default'"
                      [title]="dept.name === 'Default' ? 'Cannot edit the Default department' : 'Edit'"
                    >Edit</button>
                    <button
                      class="btn btn-sm btn-danger"
                      (click)="deleteDepartment(dept)"
                      [disabled]="dept.name === 'Default'"
                      [title]="dept.name === 'Default' ? 'Cannot delete the Default department' : 'Delete'"
                    >Delete</button>
                  </td>
                </tr>
              }
            </tbody>
          </table>
        }
      </section>
    </div>
  `,
  styles: [`
    .create-form-shell { border-bottom: 1px solid var(--border); padding-bottom: 1.5rem; }
    .create-form-shell h4 { margin: 0 0 1rem; font-size: .95rem; }
    .form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: .75rem 1.25rem; }
    .field-full { grid-column: 1 / -1; }
    .form-actions { display: flex; gap: .5rem; justify-content: flex-end; margin-top: 1rem; }
    .req { color: var(--danger); }
    .desc-cell { font-size: .85rem; color: var(--text-2); max-width: 280px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
    @media (max-width: 640px) { .form-grid { grid-template-columns: 1fr; } }
  `]
})
export class DepartmentListComponent implements OnInit {
  private departmentsService = inject(DepartmentsService);
  private fb = inject(FormBuilder);

  departments = signal<DepartmentDto[]>([]);
  loading = signal(true);
  saving = signal(false);
  errorMessage = signal('');
  showCreateForm = signal(false);
  showEditForm = signal(false);
  editingDepartment = signal<DepartmentDto | null>(null);

  createForm = this.fb.group({
    name:             ['', [Validators.required, Validators.minLength(3)]],
    description:      [''],
    contactEmail:     ['', [Validators.required, Validators.email]],
    logRetentionDays: [90, [Validators.required, Validators.min(1)]]
  });

  editForm = this.fb.group({
    name:             ['', [Validators.required, Validators.minLength(3)]],
    description:      [''],
    contactEmail:     ['', [Validators.required, Validators.email]],
    logRetentionDays: [90, [Validators.required, Validators.min(1)]]
  });

  ngOnInit(): void {
    this.loadDepartments();
  }

  loadDepartments(): void {
    this.loading.set(true);
    this.errorMessage.set('');
    this.departmentsService.getAll().subscribe({
      next: (depts) => { this.departments.set(depts); this.loading.set(false); },
      error: (err) => { this.errorMessage.set(err?.error?.message || 'Failed to load departments.'); this.loading.set(false); }
    });
  }

  toggleCreateForm(): void {
    const next = !this.showCreateForm();
    this.showCreateForm.set(next);
    if (next) this.closeEditForm();
    if (!next) this.createForm.reset({ contactEmail: '', logRetentionDays: 90 });
  }

  submitCreate(): void {
    this.createForm.markAllAsTouched();
    if (this.createForm.invalid) return;
    const v = this.createForm.value;
    const request: CreateDepartmentRequest = {
      name: v.name!,
      description: v.description || undefined,
      contactEmail: v.contactEmail!,
      logRetentionDays: v.logRetentionDays!
    };
    this.saving.set(true);
    this.errorMessage.set('');
    this.departmentsService.create(request).subscribe({
      next: (newDept) => {
        this.departments.update(list => [...list, newDept]);
        this.createForm.reset({ contactEmail: '', logRetentionDays: 90 });
        this.showCreateForm.set(false);
        this.saving.set(false);
      },
      error: (err) => { this.errorMessage.set(err?.error?.message || 'Failed to create department.'); this.saving.set(false); }
    });
  }

  openEditForm(dept: DepartmentDto): void {
    if (dept.name === 'Default') {
      return;
    }

    this.showCreateForm.set(false);
    this.editingDepartment.set(dept);
    this.editForm.reset({
      name: dept.name,
      description: dept.description || '',
      contactEmail: dept.contactEmail,
      logRetentionDays: dept.logRetentionDays
    });
    this.showEditForm.set(true);
    this.errorMessage.set('');
  }

  closeEditForm(): void {
    this.showEditForm.set(false);
    this.editingDepartment.set(null);
    this.editForm.reset({ contactEmail: '', logRetentionDays: 90 });
  }

  submitEdit(): void {
    this.editForm.markAllAsTouched();
    if (this.editForm.invalid) return;

    const current = this.editingDepartment();
    if (!current || current.name === 'Default') {
      this.errorMessage.set('The Default department cannot be edited.');
      return;
    }

    const value = this.editForm.value;
    const request: UpdateDepartmentRequest = {
      name: value.name!,
      description: value.description || undefined,
      contactEmail: value.contactEmail!,
      retryPolicy: current.retryPolicy,
      logRetentionDays: value.logRetentionDays!
    };

    this.saving.set(true);
    this.errorMessage.set('');
    this.departmentsService.update(current.departmentId, request).subscribe({
      next: (updated) => {
        this.departments.update(list => list.map(dept => dept.departmentId === updated.departmentId ? updated : dept));
        this.saving.set(false);
        this.closeEditForm();
      },
      error: (err) => {
        this.errorMessage.set(err?.error?.message || 'Failed to update department.');
        this.saving.set(false);
      }
    });
  }

  deleteDepartment(dept: DepartmentDto): void {
    if (!confirm(`Delete "${dept.name}"? This cannot be undone.`)) return;
    this.departmentsService.delete(dept.departmentId).subscribe({
      next: () => this.departments.update(list => list.filter(d => d.departmentId !== dept.departmentId)),
      error: (err) => this.errorMessage.set(err?.error?.message || 'Failed to delete department.')
    });
  }

  fi(field: string): boolean {
    return isFieldInvalid(this.createForm, field);
  }

  editFieldInvalid(field: string): boolean {
    return isFieldInvalid(this.editForm, field);
  }
}
