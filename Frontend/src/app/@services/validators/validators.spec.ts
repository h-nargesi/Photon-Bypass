import { AbstractControl, FormControl, FormGroup } from '@angular/forms';
import { PasswordValidators } from './password-validators';

describe('PasswordValidators', () => {
  describe('ValidationPattern', () => {
    const pattern = new RegExp(`^(?:${PasswordValidators.ValidationPattern})$`);

    it('accepts latin passwords with digits and both cases', () => {
      expect(pattern.test('Abc1234')).toBeTrue();
      expect(pattern.test('Passw0rd')).toBeTrue();
    });

    it('rejects latin passwords without upper case', () => {
      expect(pattern.test('abc1234')).toBeFalse();
    });

    it('rejects latin passwords without lower case', () => {
      expect(pattern.test('ABC1234')).toBeFalse();
    });

    it('rejects passwords without any digit', () => {
      expect(pattern.test('Abcdefg')).toBeFalse();
    });

    it('accepts Persian letters with Persian digits', () => {
      expect(pattern.test('کاربر۱۲۳۴')).toBeTrue();
    });

    it('rejects Persian letters without digits', () => {
      expect(pattern.test('کاربرکاربر')).toBeFalse();
    });
  });

  describe('confirmPassword', () => {
    function group(password: string, confirm: string): FormGroup {
      return new FormGroup({
        password: new FormControl(password),
        confirmPassword: new FormControl(confirm),
      });
    }

    it('passes when the confirm value matches a valid password', () => {
      const control = group('Passw0rd', 'Passw0rd');

      expect(PasswordValidators.confirmPassword(control as AbstractControl)).toBeNull();
      expect(control.get('confirmPassword')?.errors).toBeNull();
    });

    it('fails and flags the confirm field on mismatch', () => {
      const control = group('Passw0rd', 'Typo1234');

      const errors = PasswordValidators.confirmPassword(control as AbstractControl);

      expect(errors).toEqual({ passwordMismatch: true });
      expect(control.get('confirmPassword')?.errors).toEqual({ passwordMismatch: true });
    });

    it('fails when the password control itself is invalid', () => {
      const control = group('Passw0rd', 'Passw0rd');
      control.get('password')?.setErrors({ minlength: true });

      const errors = PasswordValidators.confirmPassword(control as AbstractControl);

      expect(errors).toEqual({ passwordMismatch: true });
    });
  });
});
