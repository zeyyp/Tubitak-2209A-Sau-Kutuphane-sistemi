import { Component, OnInit } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './login.component.html',
  styleUrls: ['./login.component.css']
})
export class LoginComponent implements OnInit {
  loginForm: FormGroup;
  isLoading = false;
  showPassword = false;

  errorMessage = '';
  showErrorBanner = false;

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private router: Router
  ) {
    this.loginForm = this.fb.group({
      studentNumber: ['', [Validators.required]],
      password: ['', [Validators.required]],
      rememberMe: [false]
    });
  }

  ngOnInit() {
    if (this.authService.isLoggedIn()) {
      const role = this.authService.getUserRole();
      if (role === 'admin') {
        this.router.navigate(['/admin']);
      } else {
        this.router.navigate(['/']);
      }
    }
  }

  togglePassword() {
    this.showPassword = !this.showPassword;
  }

  displayError(message: string) {
    this.errorMessage = message;
    this.showErrorBanner = true;
    setTimeout(() => {
      this.showErrorBanner = false;
    }, 5000);
  }

  onLogin() {
    if (this.loginForm.invalid) {
      this.loginForm.markAllAsTouched();
      return;
    }

    this.isLoading = true;
    this.showErrorBanner = false;
    const { studentNumber, password, rememberMe } = this.loginForm.value;

    this.authService.login(studentNumber, password).subscribe({
      next: (response: any) => {
        this.isLoading = false;
        
        if (!rememberMe && response && response.refreshToken) {
           sessionStorage.setItem('refresh_token', response.refreshToken);
           localStorage.removeItem('refresh_token');
        }

        const role = localStorage.getItem('user_role');
        if (role === 'admin') {
          this.router.navigate(['/admin']);
        } else {
          this.router.navigate(['/']);
        }
      },
      error: (err) => {
        this.isLoading = false;
        if (err.status === 401) {
          this.displayError('Öğrenci numarası veya şifre hatalı.');
        } else if (err.status === 423) {
          this.displayError('Hesabınız geçici olarak kilitlendi. 15 dakika sonra tekrar deneyin.');
        } else {
          this.displayError('Giriş sırasında bir hata oluştu. Lütfen tekrar deneyin.');
        }
      }
    });
  }
}
