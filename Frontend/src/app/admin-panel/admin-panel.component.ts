import { Component, OnInit, Inject, ChangeDetectorRef } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { FeedbackService } from '../services/feedback.service';
import { ReservationService } from '../services/reservation.service';
import { PLATFORM_ID } from '@angular/core';

@Component({
  selector: 'app-admin-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-panel.component.html',
  styleUrls: ['./admin-panel.component.css']
})
export class AdminPanelComponent implements OnInit {
  feedbacks: any[] = [];
  aiAnalysis: any = null;
  reservations: any[] = [];
  penalties: any[] = [];
  examWeeks: any[] = [];
  faculties: any[] = [];
  isLoadingFeedbacks = false;
  isLoadingAIAnalysis = false;
  isLoadingReservations = false;
  isLoadingPenalties = false;
  isLoadingExamWeeks = false;
  isLoadingFaculties = false;
  penaltiesError = '';
  aiAnalysisError = '';

  // Exam week form
  newExamWeek = {
    facultyId: 0,
    examWeekStart: '',
    examWeekEnd: ''
  };
  examWeekMessage = '';
  examWeekError = '';
  isSavingExamWeek = false;

  constructor(
    private feedbackService: FeedbackService,
    private reservationService: ReservationService,
    private cdr: ChangeDetectorRef,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) {
      return;
    }
    this.loadData();
  }

  loadData() {
    this.isLoadingFeedbacks = true;
    this.isLoadingReservations = true;
    this.isLoadingPenalties = true;
    this.isLoadingExamWeeks = true;
    this.isLoadingFaculties = true;
    this.penaltiesError = '';
    this.cdr.detectChanges();

    this.feedbackService.getFeedbacks().subscribe({
      next: (data) => {
        this.feedbacks = Array.isArray(data) ? data : [];
        this.isLoadingFeedbacks = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.feedbacks = [];
        this.isLoadingFeedbacks = false;
        this.cdr.detectChanges();
      }
    });

    this.reservationService.getAllReservations().subscribe({
      next: (data) => {
        this.reservations = Array.isArray(data) ? data : [];
        this.isLoadingReservations = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.reservations = [];
        this.isLoadingReservations = false;
        this.cdr.detectChanges();
      }
    });

    this.reservationService.getPenaltyList().subscribe({
      next: (data) => {
        this.penalties = Array.isArray(data) ? data : [];
        this.isLoadingPenalties = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.penalties = [];
        this.penaltiesError = 'Ceza listesi alınamadı.';
        this.isLoadingPenalties = false;
        this.cdr.detectChanges();
      }
    });

    this.reservationService.getExamWeeks().subscribe({
      next: (data) => {
        this.examWeeks = Array.isArray(data) ? data : [];
        this.isLoadingExamWeeks = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.examWeeks = [];
        this.isLoadingExamWeeks = false;
        this.cdr.detectChanges();
      }
    });

    this.reservationService.getFaculties().subscribe({
      next: (data) => {
        this.faculties = Array.isArray(data) ? data : [];
        this.isLoadingFaculties = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.faculties = [];
        this.isLoadingFaculties = false;
        this.cdr.detectChanges();
      }
    });
  }

  onSetExamWeek() {
    if (!this.newExamWeek.facultyId || !this.newExamWeek.examWeekStart || !this.newExamWeek.examWeekEnd) {
      this.examWeekError = 'Tüm alanlar zorunludur.';
      this.examWeekMessage = '';
      return;
    }

    this.isSavingExamWeek = true;
    this.examWeekError = '';
    this.examWeekMessage = '';

    this.reservationService.setExamWeek(
      this.newExamWeek.facultyId,
      this.newExamWeek.examWeekStart,
      this.newExamWeek.examWeekEnd
    ).subscribe({
      next: (response) => {
        this.examWeekMessage = response.message || 'Sınav haftası başarıyla ayarlandı.';
        this.isSavingExamWeek = false;
        this.newExamWeek = { facultyId: 0, examWeekStart: '', examWeekEnd: '' };
        this.loadData(); // Refresh exam weeks list
        this.cdr.detectChanges();
      },
      error: (error) => {
        this.examWeekError = error.error?.message || 'Sınav haftası ayarlanırken hata oluştu.';
        this.isSavingExamWeek = false;
        this.cdr.detectChanges();
      }
    });
  }

  // Helper method for template to use Object.keys()
  objectKeys(obj: any): string[] {
    return obj ? Object.keys(obj) : [];
  }

  // AI Analizi Manuel Yenileme
  refreshAIAnalysis() {
    this.isLoadingAIAnalysis = true;
    this.aiAnalysisError = '';
    this.cdr.detectChanges();

    this.feedbackService.getAnalysis().subscribe({
      next: (data) => {
        this.aiAnalysis = data;
        this.isLoadingAIAnalysis = false;
        this.cdr.detectChanges();
      },
      error: (err) => {
        this.aiAnalysis = null;
        this.aiAnalysisError = 'AI analizi şu an kullanılamıyor. Lütfen daha sonra tekrar deneyin.';
        this.isLoadingAIAnalysis = false;
        console.error('AI Analysis error:', err);
        this.cdr.detectChanges();
      }
    });
  }
}
