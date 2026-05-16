import { Component, OnInit, OnDestroy, inject, PLATFORM_ID, ChangeDetectorRef } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Subscription, catchError, of } from 'rxjs';

@Component({
  selector: 'app-feedback',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule],
  templateUrl: './feedback.component.html',
  styleUrls: ['./feedback.component.css']
})
export class FeedbackComponent implements OnInit, OnDestroy {
  private fb = inject(FormBuilder);
  private http = inject(HttpClient);
  private router = inject(Router);
  private cdr = inject(ChangeDetectorRef);
  private platformId = inject(PLATFORM_ID);

  private readonly API_URL = 'http://localhost:5010';

  feedbackForm!: FormGroup;
  charCount = 0;
  isSubmitting = false;
  isSuccess = false;
  errorMessage = '';

  private charSub?: Subscription;
  private errorTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.feedbackForm = this.fb.group({
      message: ['', [Validators.required, Validators.minLength(10), Validators.maxLength(500)]]
    });

    // Karakter sayacı — valueChanges observable
    this.charSub = this.feedbackForm.get('message')!.valueChanges.subscribe(val => {
      this.charCount = (val || '').length;
    });

    // Giriş yapılmamışsa login'e yönlendir
    if (isPlatformBrowser(this.platformId)) {
      const token = localStorage.getItem('access_token');
      if (!token) {
        this.router.navigate(['/login']);
      }
    }
  }

  ngOnDestroy(): void {
    this.charSub?.unsubscribe();
    if (this.errorTimer) clearTimeout(this.errorTimer);
  }

  isFieldInvalid(field: string): boolean {
    const ctrl = this.feedbackForm.get(field);
    return !!(ctrl && ctrl.invalid && ctrl.touched);
  }

  onSubmit(): void {
    if (this.feedbackForm.invalid) {
      this.feedbackForm.markAllAsTouched();
      return;
    }

    if (!isPlatformBrowser(this.platformId)) return;

    const studentNumber = localStorage.getItem('current_user');
    const token = localStorage.getItem('access_token');

    if (!studentNumber || !token) {
      this.router.navigate(['/login']);
      return;
    }

    this.isSubmitting = true;
    this.errorMessage = '';
    this.cdr.detectChanges();

    const headers = new HttpHeaders({
      'Authorization': `Bearer ${token}`,
      'Content-Type': 'application/json'
    });

    const body = {
      studentNumber,
      message: this.feedbackForm.value.message
    };

    this.http.post(`${this.API_URL}/api/Feedback/Submit`, body, { headers }).pipe(
      catchError(err => {
        const msg = err?.error?.message ?? err?.error ?? 'Geri bildirim gönderilirken bir hata oluştu. Lütfen tekrar deneyin.';
        this.errorMessage = typeof msg === 'string' ? msg : 'Geri bildirim gönderilirken bir hata oluştu. Lütfen tekrar deneyin.';
        this.isSubmitting = false;
        this.cdr.detectChanges();

        // 5 saniye sonra banner kapanır
        this.errorTimer = setTimeout(() => {
          this.errorMessage = '';
          this.cdr.detectChanges();
        }, 5000);

        return of(null);
      })
    ).subscribe(res => {
      if (res !== null) {
        this.isSuccess = true;
        this.isSubmitting = false;
        this.cdr.detectChanges();
      }
    });
  }

  resetForm(): void {
    this.feedbackForm.reset({ message: '' });
    this.charCount = 0;
    this.isSuccess = false;
    this.errorMessage = '';
    this.cdr.detectChanges();
  }
}
