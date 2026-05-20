import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { DepartmentsService } from '../../services/departments.service';
import { DepartmentDto, CreateDepartmentRequest, UpdateDepartmentRequest } from '../../models/models';
import { isFieldInvalid } from '../../shared/form-utils';
import { ButtonDirective } from 'ui-design-system';

@Component({
  selector: 'app-department-list',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, ButtonDirective],
  templateUrl: './department-list.component.html',
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
