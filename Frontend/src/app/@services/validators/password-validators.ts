import { AbstractControl, ValidationErrors } from '@angular/forms';

export class PasswordValidators {
  static ValidationPattern: string =
    '(?=.*[\\d\\u06F0-\\u06F9])((?=.*[a-z])(?=.*[A-Z])|(?=.*[\\u0622-\\u0648\\u067E-\\u06CC])).+';

  static confirmPassword(control: AbstractControl): ValidationErrors | null {
    const password = control.get('password');
    const confirm = control.get('confirmPassword');
    if (password?.valid) {
      const passValue = password?.value ?? '';
      const confirmValue = confirm?.value ?? '';
      if (password?.valid && passValue === confirmValue) {
        confirm?.setErrors(null);
        return null;
      }
    }
    confirm?.setErrors({ passwordMismatch: true });
    return { passwordMismatch: true };
  }
}
