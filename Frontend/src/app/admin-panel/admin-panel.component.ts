import { Component, OnInit, OnDestroy, Inject, ChangeDetectorRef, HostListener } from '@angular/core';
import { CommonModule, isPlatformBrowser } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { FeedbackService } from '../services/feedback.service';
import { ReservationService } from '../services/reservation.service';
import { AuthService } from '../services/auth.service';
import { PLATFORM_ID } from '@angular/core';
import { forkJoin, Subscription } from 'rxjs';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'app-admin-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-panel.component.html',
  styleUrls: ['./admin-panel.component.css']
})
export class AdminPanelComponent implements OnInit, OnDestroy {
  // Active tab
  activeTab: 'dashboard' | 'reservations' | 'penalties' | 'feedback' | 'examweeks' | 'turnstile' = 'dashboard';

  // Sidebar mobile state
  sidebarOpen = false;
  profileDropdownOpen = false;

  // Admin info
  adminName = '';

  // Dashboard
  stats: any = null;
  feedbacks: any[] = [];
  penalties: any[] = [];
  isLoadingDashboard = false;
  dashboardError = false;
  dashboardLoaded = false;

  // All Reservations
  reservations: any[] = [];
  filteredReservations: any[] = [];
  reservationSearch = '';
  reservationStatusFilter = '';
  isLoadingReservations = false;
  reservationsError = false;
  reservationsLoaded = false;
  currentPage = 1;
  readonly pageSize = 20;

  // Penalties
  isLoadingPenalties = false;
  penaltiesError = false;
  penaltiesLoaded = false;

  // Feedback & AI
  aiAnalysis: any = null;
  aiSummary: any = null;
  isLoadingFeedback = false;
  isLoadingAI = false;
  feedbackError = false;
  aiError = false;
  feedbackLoaded = false;

  // Exam Weeks
  examWeeks: any[] = [];
  faculties: any[] = [];
  isLoadingExamWeeks = false;
  examWeeksError = false;
  examWeeksLoaded = false;
  newExamWeek = { facultyId: 0, examWeekStart: '', examWeekEnd: '' };
  isSavingExamWeek = false;
  examWeekToast: { type: 'success' | 'error'; message: string } | null = null;
  private toastTimer: any;

  // Turnstile
  turnstileLogs: any[] = [];
  isLoadingTurnstile = false;
  turnstileError = false;
  turnstileLoaded = false;

  private subs = new Subscription();

  constructor(
    private feedbackService: FeedbackService,
    private reservationService: ReservationService,
    private authService: AuthService,
    private http: HttpClient,
    private router: Router,
    private cdr: ChangeDetectorRef,
    @Inject(PLATFORM_ID) private platformId: Object
  ) {}

  ngOnInit(): void {
    if (!isPlatformBrowser(this.platformId)) return;

    const role = localStorage.getItem('user_role');
    if (role !== 'admin') {
      this.router.navigate(['/']);
      return;
    }

    this.adminName = localStorage.getItem('full_name') || 'Admin';
    this.loadDashboard();
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
    if (this.toastTimer) clearTimeout(this.toastTimer);
  }

  // ─── Navigation ───────────────────────────────────────────────────────────

  setTab(tab: typeof this.activeTab): void {
    this.activeTab = tab;
    this.sidebarOpen = false;
    if (tab === 'dashboard' && !this.dashboardLoaded) this.loadDashboard();
    if (tab === 'reservations' && !this.reservationsLoaded) this.loadReservations();
    if (tab === 'penalties' && !this.penaltiesLoaded) this.loadPenalties();
    if (tab === 'feedback' && !this.feedbackLoaded) this.loadFeedback();
    if (tab === 'examweeks' && !this.examWeeksLoaded) this.loadExamWeeks();
    if (tab === 'turnstile' && !this.turnstileLoaded) this.loadTurnstile();
  }

  logout(): void {
    this.authService.logout();
    this.router.navigate(['/login']);
  }

  goToUserInterface(): void {
    this.profileDropdownOpen = false;
    this.router.navigate(['/home']);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    const target = event.target as HTMLElement;
    if (!target.closest('.topbar-profile')) {
      this.profileDropdownOpen = false;
    }
  }

  // ─── Dashboard ────────────────────────────────────────────────────────────

  loadDashboard(): void {
    this.isLoadingDashboard = true;
    this.dashboardError = false;

    const sub = forkJoin({
      stats: this.reservationService.getStats().pipe(catchError(() => of(null))),
      feedbacks: this.feedbackService.getFeedbacks().pipe(catchError(() => of([]))),
      penalties: this.reservationService.getPenaltyList().pipe(catchError(() => of([])))
    }).subscribe({
      next: ({ stats, feedbacks, penalties }) => {
        this.stats = stats;
        this.feedbacks = Array.isArray(feedbacks) ? feedbacks : [];
        this.penalties = Array.isArray(penalties) ? penalties : [];
        this.isLoadingDashboard = false;
        this.dashboardLoaded = true;
        this.cdr.detectChanges();
      },
      error: () => {
        this.isLoadingDashboard = false;
        this.dashboardError = true;
        this.cdr.detectChanges();
      }
    });
    this.subs.add(sub);
  }

  get studentTypeKeys(): string[] {
    return this.stats?.byStudentType ? Object.keys(this.stats.byStudentType) : [];
  }

  get studentTypeTotal(): number {
    if (!this.stats?.byStudentType) return 1;
    return Object.values(this.stats.byStudentType as Record<string, number>).reduce((a, b) => a + b, 0) || 1;
  }

  getStudentTypePercent(key: string): number {
    if (!this.stats?.byStudentType) return 0;
    return Math.round((this.stats.byStudentType[key] / this.studentTypeTotal) * 100);
  }

  getStudentTypeColor(key: string): string {
    const colors: Record<string, string> = {
      'Lisans': '#2563eb',
      'Yüksek Lisans': '#7c3aed',
      'Doktora': '#059669'
    };
    return colors[key] ?? '#64748b';
  }

  get hourKeys(): string[] {
    return this.stats?.reservationsByHour ? Object.keys(this.stats.reservationsByHour).sort((a, b) => +a - +b) : [];
  }

  get maxHourCount(): number {
    if (!this.stats?.reservationsByHour) return 1;
    return Math.max(...Object.values(this.stats.reservationsByHour as Record<string, number>)) || 1;
  }

  getHourPercent(key: string): number {
    return Math.round((this.stats.reservationsByHour[key] / this.maxHourCount) * 100);
  }

  // ─── Reservations ─────────────────────────────────────────────────────────

  loadReservations(): void {
    this.isLoadingReservations = true;
    this.reservationsError = false;

    const sub = this.reservationService.getAllReservations().pipe(catchError(() => of(null))).subscribe({
      next: (data) => {
        if (data === null) {
          this.reservationsError = true;
        } else {
          this.reservations = Array.isArray(data) ? data : [];
          this.applyFilters();
          this.reservationsLoaded = true;
        }
        this.isLoadingReservations = false;
        this.cdr.detectChanges();
      }
    });
    this.subs.add(sub);
  }

  applyFilters(): void {
    let result = [...this.reservations];
    if (this.reservationSearch.trim()) {
      const q = this.reservationSearch.trim().toLowerCase();
      result = result.filter(r => r.studentNumber?.toLowerCase().includes(q));
    }
    if (this.reservationStatusFilter) {
      result = result.filter(r => {
        if (this.reservationStatusFilter === 'attended') return r.isAttended === true;
        if (this.reservationStatusFilter === 'noshow') return r.penaltyProcessed === true;
        if (this.reservationStatusFilter === 'pending') return r.isAttended !== true && r.penaltyProcessed !== true;
        return true;
      });
    }
    this.filteredReservations = result;
    this.currentPage = 1;
  }

  resetFilters(): void {
    this.reservationSearch = '';
    this.reservationStatusFilter = '';
    this.applyFilters();
  }

  get pagedReservations(): any[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredReservations.slice(start, start + this.pageSize);
  }

  get totalPages(): number {
    return Math.ceil(this.filteredReservations.length / this.pageSize) || 1;
  }

  prevPage(): void { if (this.currentPage > 1) this.currentPage--; }
  nextPage(): void { if (this.currentPage < this.totalPages) this.currentPage++; }

  getReservationStatus(r: any): 'attended' | 'penalty' | 'pending' {
    if (r.isAttended === true) return 'attended';
    if (r.penaltyProcessed === true) return 'penalty';
    return 'pending';
  }

  // ─── Penalties ────────────────────────────────────────────────────────────

  loadPenalties(): void {
    this.isLoadingPenalties = true;
    this.penaltiesError = false;

    const sub = this.reservationService.getPenaltyList().pipe(catchError(() => of(null))).subscribe({
      next: (data) => {
        if (data === null) {
          this.penaltiesError = true;
        } else {
          this.penalties = Array.isArray(data) ? data : [];
          this.penaltiesLoaded = true;
        }
        this.isLoadingPenalties = false;
        this.cdr.detectChanges();
      }
    });
    this.subs.add(sub);
  }

  // ─── Feedback & AI ────────────────────────────────────────────────────────

  loadFeedback(): void {
    this.isLoadingFeedback = true;
    this.feedbackError = false;
    this.isLoadingAI = true;
    this.aiError = false;

    const sub = forkJoin({
      analysis: this.feedbackService.getAnalysis().pipe(catchError(() => of(null))),
      summary: this.http.get<any>('http://localhost:5010/api/Feedback/Summary').pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ analysis, summary }) => {
        this.aiAnalysis = analysis;
        this.aiSummary = summary;
        if (!analysis) this.aiError = true;
        this.isLoadingFeedback = false;
        this.isLoadingAI = false;
        this.feedbackLoaded = true;
        this.cdr.detectChanges();
      }
    });
    this.subs.add(sub);
  }

  refreshAnalysis(): void {
    this.isLoadingAI = true;
    this.aiError = false;

    const sub = forkJoin({
      analysis: this.feedbackService.getAnalysis().pipe(catchError(() => of(null))),
      summary: this.http.get<any>('http://localhost:5010/api/Feedback/Summary').pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ analysis, summary }) => {
        this.aiAnalysis = analysis;
        this.aiSummary = summary;
        if (!analysis) this.aiError = true;
        this.isLoadingAI = false;
        this.cdr.detectChanges();
      }
    });
    this.subs.add(sub);
  }

  get topicKeys(): string[] {
    return this.aiAnalysis?.topicCounts ? Object.keys(this.aiAnalysis.topicCounts) : [];
  }

  // ─── Exam Weeks ───────────────────────────────────────────────────────────

  loadExamWeeks(): void {
    this.isLoadingExamWeeks = true;
    this.examWeeksError = false;

    const sub = forkJoin({
      weeks: this.reservationService.getExamWeeks().pipe(catchError(() => of(null))),
      faculties: this.reservationService.getFaculties().pipe(catchError(() => of([])))
    }).subscribe({
      next: ({ weeks, faculties }) => {
        if (weeks === null) {
          this.examWeeksError = true;
        } else {
          this.examWeeks = Array.isArray(weeks) ? weeks : [];
          this.examWeeksLoaded = true;
        }
        this.faculties = Array.isArray(faculties) ? faculties : [];
        this.isLoadingExamWeeks = false;
        this.cdr.detectChanges();
      }
    });
    this.subs.add(sub);
  }

  onSetExamWeek(): void {
    if (!this.newExamWeek.facultyId || !this.newExamWeek.examWeekStart || !this.newExamWeek.examWeekEnd) {
      this.showToast('error', 'Tüm alanlar zorunludur.');
      return;
    }
    if (new Date(this.newExamWeek.examWeekEnd) < new Date(this.newExamWeek.examWeekStart)) {
      this.showToast('error', 'Bitiş tarihi başlangıç tarihinden önce olamaz.');
      return;
    }
    this.isSavingExamWeek = true;

    const sub = this.reservationService.setExamWeek(
      this.newExamWeek.facultyId,
      this.newExamWeek.examWeekStart,
      this.newExamWeek.examWeekEnd
    ).pipe(catchError(err => of({ error: err }))).subscribe({
      next: (res: any) => {
        if (res?.error) {
          this.showToast('error', res.error?.error?.message || 'Sınav haftası kaydedilemedi.');
        } else {
          this.showToast('success', 'Sınav haftası kaydedildi.');
          this.newExamWeek = { facultyId: 0, examWeekStart: '', examWeekEnd: '' };
          this.examWeeksLoaded = false;
          this.loadExamWeeks();
        }
        this.isSavingExamWeek = false;
        this.cdr.detectChanges();
      }
    });
    this.subs.add(sub);
  }

  clearExamForm(): void {
    this.newExamWeek = { facultyId: 0, examWeekStart: '', examWeekEnd: '' };
  }

  isExamWeekActive(week: any): boolean {
    const now = new Date();
    return new Date(week.examWeekStart) <= now && now <= new Date(week.examWeekEnd);
  }

  private showToast(type: 'success' | 'error', message: string): void {
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.examWeekToast = { type, message };
    this.toastTimer = setTimeout(() => {
      this.examWeekToast = null;
      this.cdr.detectChanges();
    }, 3000);
  }

  // ─── Turnstile ────────────────────────────────────────────────────────────

  loadTurnstile(): void {
    this.isLoadingTurnstile = true;
    this.turnstileError = false;

    const sub = this.http.get<any[]>('http://localhost:5010/api/Turnstile/logs?take=50')
      .pipe(catchError(() => of(null)))
      .subscribe({
        next: (data) => {
          if (data === null) {
            this.turnstileError = true;
          } else {
            this.turnstileLogs = Array.isArray(data) ? data : [];
            this.turnstileLoaded = true;
          }
          this.isLoadingTurnstile = false;
          this.cdr.detectChanges();
        }
      });
    this.subs.add(sub);
  }

  reloadTurnstile(): void {
    this.turnstileLoaded = false;
    this.loadTurnstile();
  }

  // ─── Helpers ──────────────────────────────────────────────────────────────

  formatDate(dateStr: string): string {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleDateString('tr-TR', {
      day: 'numeric', month: 'long', year: 'numeric'
    });
  }

  formatDateTime(dateStr: string): string {
    if (!dateStr) return '—';
    return new Date(dateStr).toLocaleDateString('tr-TR', {
      day: 'numeric', month: 'long', year: 'numeric', hour: '2-digit', minute: '2-digit'
    });
  }

  objectKeys(obj: any): string[] {
    return obj ? Object.keys(obj) : [];
  }

}
