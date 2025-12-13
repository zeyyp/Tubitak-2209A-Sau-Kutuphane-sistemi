import { Component, OnInit, Inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReservationService } from '../services/reservation.service';
import { AuthService } from '../services/auth.service';
import { FeedbackService } from '../services/feedback.service';
import { Router } from '@angular/router';
import { PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.css']
})
export class ProfileComponent implements OnInit {
  studentNumber: string = '';
  reservations: any[] = [];
  feedbacks: any[] = [];
  isLoadingReservations: boolean = true;
  isLoadingFeedbacks: boolean = true;
  isLoadingProfile: boolean = true;
  profileError: string = '';
  penaltyInfo: any = null;
  academicLevel: string = '';

  constructor(
    private reservationService: ReservationService,
    private authService: AuthService,
    private feedbackService: FeedbackService,
    private router: Router,
    private cdr: ChangeDetectorRef,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  ngOnInit() {
    if (isPlatformBrowser(this.platformId)) {
      const user = this.authService.getCurrentUser();
      if (!user) {
        this.router.navigate(['/login']);
        return;
      }
      this.studentNumber = user;
      this.academicLevel = localStorage.getItem('academicLevel') || '';
      this.loadReservations();
      this.loadFeedbacks();
      this.loadPenaltyInfo();
    }
  }

  loadReservations() {
    this.reservationService.getStudentReservations(this.studentNumber).subscribe({
      next: (data: any) => {
        this.reservations = data;
        this.isLoadingReservations = false;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        console.error(err);
        this.isLoadingReservations = false;
        this.cdr.detectChanges();
      }
    });
  }

  loadFeedbacks() {
    this.feedbackService.getStudentFeedbacks(this.studentNumber).subscribe({
      next: (data: any) => {
        this.feedbacks = data;
        this.isLoadingFeedbacks = false;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        console.error(err);
        this.isLoadingFeedbacks = false;
        this.cdr.detectChanges();
      }
    });
  }

  loadPenaltyInfo() {
    this.reservationService.getStudentPenalty(this.studentNumber).subscribe({
      next: (data: any) => {
        this.penaltyInfo = data;
        this.isLoadingProfile = false;
        this.cdr.detectChanges();
      },
      error: (err: any) => {
        console.error(err);
        this.profileError = 'Ceza bilgisi yüklenirken bir hata oluştu.';
        this.isLoadingProfile = false;
        this.cdr.detectChanges();
      }
    });
  }

  cancelReservation(reservationId: number) {
    if (confirm('Bu rezervasyonu iptal etmek istediğinize emin misiniz?')) {
      this.reservationService.cancelReservation(reservationId).subscribe({
        next: () => {
          alert('Rezervasyon iptal edildi.');
          this.loadReservations();
        },
        error: (err: any) => {
          console.error(err);
          alert('Rezervasyon iptal edilirken bir hata oluştu.');
        }
      });
    }
  }
}
