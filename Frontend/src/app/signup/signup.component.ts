import { Component, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { ReservationService } from '../services/reservation.service';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';

@Component({
  selector: 'app-signup',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './signup.component.html',
  styleUrls: ['./signup.component.css']
})
export class SignupComponent implements OnInit {
  signupForm: FormGroup;
  faculties: any[] = [];
  isLoadingFaculties = false;
  isSubmitting = false;

  showPassword = false;
  showPasswordConfirm = false;

  errorMessage = '';
  showErrorBanner = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private reservationService: ReservationService,
    private router: Router
  ) {
    this.signupForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.minLength(3)]],
      studentNumber: ['', [Validators.required]],
      academicLevel: ['', [Validators.required]],
      facultyId: [0, [Validators.required, Validators.min(1)]],
      department: [{ value: '', disabled: true }],
      email: ['', [Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8), Validators.pattern('^(?=.*[A-Z])(?=.*\\d).+$')]],
      passwordConfirm: ['', [Validators.required]]
    }, { validators: this.matchPasswords });
  }

  ngOnInit(): void {
    this.loadFaculties();

    this.signupForm.get('facultyId')?.valueChanges.subscribe(value => {
      const deptControl = this.signupForm.get('department');
      if (value && value > 0) {
        deptControl?.enable();
      } else {
        deptControl?.disable();
        deptControl?.setValue('');
      }
    });
  }

  matchPasswords(group: AbstractControl): ValidationErrors | null {
    const password = group.get('password')?.value;
    const confirm = group.get('passwordConfirm')?.value;
    if (!password || !confirm) return null;
    return password === confirm ? null : { passwordsMismatch: true };
  }

  loadFaculties() {
    this.isLoadingFaculties = true;
    this.reservationService.getFaculties().subscribe({
      next: (data) => {
        this.faculties = data;
        this.isLoadingFaculties = false;
      },
      error: () => {
        this.isLoadingFaculties = false;
      }
    });
  }

  togglePassword() {
    this.showPassword = !this.showPassword;
  }

  togglePasswordConfirm() {
    this.showPasswordConfirm = !this.showPasswordConfirm;
  }

  displayError(message: string) {
    this.errorMessage = message;
    this.showErrorBanner = true;
    setTimeout(() => {
      this.showErrorBanner = false;
    }, 5000);
  }

  onSubmit() {
    if (this.signupForm.invalid) {
      this.signupForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    const formValue = this.signupForm.getRawValue();

    const registerData = {
      studentNumber: formValue.studentNumber,
      fullName: formValue.fullName,
      academicLevel: formValue.academicLevel,
      email: formValue.email || '',
      password: formValue.password
    };

    this.authService.register(registerData).subscribe({
      next: () => {
        this.authService.login(formValue.studentNumber, formValue.password).subscribe({
          next: () => {
            this.reservationService.updateStudentDepartment(
              formValue.studentNumber, 
              Number(formValue.facultyId), 
              formValue.department || ''
            ).subscribe({
              next: () => {
                this.isSubmitting = false;
                this.router.navigate(['/']);
              },
              error: (err) => {
                console.error('Bölüm güncellenemedi:', err);
                this.isSubmitting = false;
                this.router.navigate(['/']);
              }
            });
          },
          error: (err) => {
            console.error('Otomatik giriş başarısız:', err);
            this.isSubmitting = false;
            this.displayError('Kayıt başarılı ancak otomatik giriş yapılamadı. Lütfen manuel giriş yapın.');
            setTimeout(() => this.router.navigate(['/login']), 2000);
          }
        });
      },
      error: (err) => {
        this.isSubmitting = false;
        if (err.status === 409) {
          this.displayError('Bu öğrenci numarası zaten kayıtlı. Giriş yapmayı deneyin.');
        } else {
          this.displayError('Kayıt sırasında bir hata oluştu. Lütfen tekrar deneyin.');
        }
      }
    });
  }
}
