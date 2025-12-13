import { Component, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FeedbackService } from '../services/feedback.service';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-feedback',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <section class="feedback-section">
      <div class="container mt-5">
        <div class="row justify-content-center">
          <div class="col-lg-8">
            <div class="card feedback-card shadow-lg border-0">
              <div class="card-body p-5">
                <div class="text-center mb-4">
                  <div class="feedback-icon mb-3">
                    <i class="bi bi-chat-heart-fill display-1 text-info"></i>
                  </div>
                  <h2 class="fw-bold">Geri Bildirim</h2>
                  <p class="text-muted">Görüş ve önerileriniz bizim için çok değerli</p>
                </div>

                <form (ngSubmit)="onSubmit()">
                  <div class="mb-4">
                    <label for="message" class="form-label fw-bold">
                      <i class="bi bi-pencil-square text-info me-2"></i>Mesajınız
                    </label>
                    <textarea
                      class="form-control form-control-lg"
                      id="message"
                      rows="6"
                      [(ngModel)]="message"
                      name="message"
                      required
                      placeholder="Kütüphane hizmetleri hakkında görüş ve önerilerinizi buraya yazabilirsiniz..."
                    ></textarea>
                    <small class="text-muted">
                      <i class="bi bi-info-circle me-1"></i>
                      Geri bildiriminiz yöneticiler tarafından incelenecektir
                    </small>
                  </div>
                  <button type="submit" class="btn btn-info btn-lg w-100" [disabled]="isSubmitting">
                    <i class="bi bi-send-fill me-2"></i>
                    {{ isSubmitting ? 'Gönderiliyor...' : 'Gönder' }}
                  </button>
                </form>

                <div class="mt-4 p-3 bg-light rounded">
                  <h6 class="fw-bold"><i class="bi bi-lightbulb text-warning me-2"></i>İpuçları</h6>
                  <ul class="mb-0 small text-muted">
                    <li>Açık ve net olun</li>
                    <li>Yaşadığınız sorunu detaylı anlatın</li>
                    <li>Önerilerinizi belirtin</li>
                  </ul>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </section>
  `,
  styles: [`
    .feedback-section { min-height: 100vh; padding: 60px 0; background: linear-gradient(135deg, #e3f2fd 0%, #f5f5f5 100%); }
    .feedback-card { border-radius: 20px; }
    .feedback-icon { animation: pulse 2s infinite; }
    @keyframes pulse { 0%, 100% { transform: scale(1); } 50% { transform: scale(1.05); } }
    textarea.form-control { border: 2px solid #e0e0e0; border-radius: 10px; }
    textarea.form-control:focus { border-color: #0dcaf0; box-shadow: 0 0 0 0.2rem rgba(13, 202, 240, 0.25); }
    .btn-info { background: linear-gradient(135deg, #0dcaf0 0%, #0a9bb7 100%); border: none; }
  `]
})
export class FeedbackComponent {
  message: string = '';
  isSubmitting: boolean = false;

  constructor(
    private feedbackService: FeedbackService,
    private authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  onSubmit() {
    const studentNumber = this.authService.getCurrentUser();
    if (!studentNumber) {
      this.router.navigate(['/login']);
      return;
    }
    if (this.authService.isAdmin()) {
      alert('Yönetici olarak geri bildirim gönderemezsiniz.');
      return;
    }

    const feedback = {
      studentNumber: studentNumber,
      message: this.message
    };

    this.isSubmitting = true;
    this.cdr.detectChanges();

    this.feedbackService.submitFeedback(feedback).subscribe({
      next: () => {
        alert('Geri bildiriminiz için teşekkürler!');
        this.message = '';
        this.isSubmitting = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error(err);
        alert('Bir hata oluştu.');
        this.isSubmitting = false;
        this.cdr.detectChanges();
      }
    });
  }
}
