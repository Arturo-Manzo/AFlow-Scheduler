import { FormGroup } from '@angular/forms';

/** Returns true when the control is invalid AND the user has interacted with it. */
export function isFieldInvalid(form: FormGroup, field: string): boolean {
  const control = form.get(field)!;
  return control.invalid && (control.dirty || control.touched);
}

/** Returns true when the control has been touched. */
export function isFieldTouched(form: FormGroup, field: string): boolean {
  const control = form.get(field)!;
  return control.touched;
}

/** Returns true when the control has a specific validation error and the user has interacted with it. */
export function hasFieldError(form: FormGroup, field: string, error: string): boolean {
  const control = form.get(field)!;
  return control.hasError(error) && (control.dirty || control.touched);
}
