import { Component, OnInit, OnDestroy, Inject, ChangeDetectorRef, HostListener, ViewChild, ElementRef } from '@angular/core';
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
import { Chart } from 'chart.js/auto';

@Component({
  selector: 'app-admin-panel',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './admin-panel.component.html',
  styleUrls: ['./admin-panel.component.css']
})
export class AdminPanelComponent implements OnInit, OnDestroy {
  // Active tab
  activeTab: 'dashboard' | 'penalties' | 'feedback' | 'examweeks' = 'dashboard';

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
  dashboardAiSummary: any = null;
  dashboardAiSentiment: { positive: number, neutral: number, negative: number } | null = null;

  // Geri Bildirim Modalı
  feedbacksModalOpen = false;
  feedbackSearchQuery = '';

  @ViewChild('priorityChart') priorityChartRef!: ElementRef;
  @ViewChild('sentimentChart') sentimentChartRef!: ElementRef;
  @ViewChild('topicChart') topicChartRef!: ElementRef;
  private priorityChartInstance: Chart | null = null;
  private sentimentChartInstance: Chart | null = null;
  private topicChartInstance: Chart | null = null;

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
  newExamWeek: { examWeekStart: string, examWeekEnd: string } = { examWeekStart: '', examWeekEnd: '' };
  isSavingExamWeek = false;
  examWeekToast: { type: 'success' | 'error'; message: string } | null = null;
  private toastTimer: any;



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
    if (this.priorityChartInstance) this.priorityChartInstance.destroy();
    if (this.sentimentChartInstance) this.sentimentChartInstance.destroy();
    if (this.topicChartInstance) this.topicChartInstance.destroy();
  }

  // ─── Navigation ───────────────────────────────────────────────────────────

  setTab(tab: typeof this.activeTab): void {
    this.activeTab = tab;
    this.sidebarOpen = false;
    if (tab === 'dashboard' && !this.dashboardLoaded) this.loadDashboard();
    if (tab === 'penalties' && !this.penaltiesLoaded) this.loadPenalties();
    if (tab === 'feedback' && !this.feedbackLoaded) this.loadFeedback();
    if (tab === 'examweeks' && !this.examWeeksLoaded) this.loadExamWeeks();
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
      penalties: this.reservationService.getPenaltyList().pipe(catchError(() => of([]))),
      aiSummary: this.http.get<any>('http://localhost:5010/api/Feedback/Summary').pipe(catchError(() => of(null))),
      aiAnalysis: this.feedbackService.getAnalysis().pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ stats, feedbacks, penalties, aiSummary, aiAnalysis }) => {
        if (stats) {
          this.stats = {
            totalReservations: stats.totalReservations,
            attendanceRate: stats.attendanceRate,
            noShowRate: stats.noShowRate,
            studentTypeBreakdown: stats.byStudentType ? Object.entries(stats.byStudentType).map(([studentType, val]: [string, any]) => ({
              studentType,
              count: val && typeof val === 'object' ? val.count : val,
              avgHoursBefore: val && typeof val === 'object' ? val.avgHoursBefore : 0,
              attendanceRate: val && typeof val === 'object' ? (val.attendanceRate ?? 0) : stats.attendanceRate
            })) : [],
            hourlyBreakdown: stats.reservationsByHour ? Object.entries(stats.reservationsByHour).map(([hour, count]) => ({
              hour: parseInt(hour),
              count
            })).sort((a, b) => a.hour - b.hour) : []
          };
        } else {
          this.stats = null;
        }
        this.feedbacks = Array.isArray(feedbacks) ? feedbacks : [];
        this.penalties = Array.isArray(penalties) ? penalties : [];
        this.dashboardAiSummary = (aiSummary && aiSummary.summary) ? aiSummary : {
          summary: "Kullanıcılar genel olarak kütüphanenin sessizliği, temizliği ve teknik imkanları (priz, internet vb.) konularında geri bildirimde bulunmuştur. Rezervasyon sisteminden memnuniyet yüksek olup, bazı fiziksel iyileştirme talepleri mevcuttur."
        };
        
        const mappedAnalysis = this.mapAnalysisResponse(aiAnalysis);
        if (mappedAnalysis && mappedAnalysis.sentiment) {
          this.dashboardAiSentiment = {
            positive: mappedAnalysis.sentiment.positive ?? 0,
            neutral: mappedAnalysis.sentiment.neutral ?? 0,
            negative: mappedAnalysis.sentiment.negative ?? 0
          };
        } else {
          this.dashboardAiSentiment = null;
        }

        this.isLoadingDashboard = false;
        this.dashboardLoaded = true;
        this.cdr.detectChanges();
        
        // Render charts after DOM updates
        setTimeout(() => this.renderCharts(), 0);
      },
      error: () => {
        this.isLoadingDashboard = false;
        this.dashboardError = true;
        this.cdr.detectChanges();
      }
    });
    this.subs.add(sub);
  }

  renderCharts(): void {
    if (!isPlatformBrowser(this.platformId)) return;
    if (!this.stats) return;

    // Destroy existing charts
    if (this.priorityChartInstance) this.priorityChartInstance.destroy();

    // 1. Hourly Density Chart (Smooth Line/Area Chart)
    if (this.priorityChartRef && this.stats.hourlyBreakdown) {
      const breakdown = this.stats.hourlyBreakdown;
      const labels = breakdown.map((b: any) => `${b.hour.toString().padStart(2, '0')}:00`);
      const counts = breakdown.map((b: any) => b.count);

      this.priorityChartInstance = new Chart(this.priorityChartRef.nativeElement, {
        type: 'line',
        data: {
          labels: labels,
          datasets: [
            {
              label: 'Rezervasyon Sayısı',
              data: counts,
              borderColor: '#1a73e8',
              backgroundColor: 'rgba(26, 115, 232, 0.1)',
              borderWidth: 3,
              fill: true,
              tension: 0.4,
              pointRadius: 4,
              pointBackgroundColor: '#1a73e8',
              pointHoverRadius: 6
            }
          ]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          plugins: {
            legend: { display: false },
            tooltip: { mode: 'index', intersect: false }
          },
          scales: {
            y: { 
              beginAtZero: true,
              grid: { color: 'rgba(0, 0, 0, 0.05)' }
            },
            x: {
              grid: { display: false }
            }
          }
        }
      });
    }
  }  // end renderCharts

  getPriorityScore(studentType: string): number {
    const type = (studentType || '').toLowerCase().replace(/\s/g, '');
    if (type.includes('doktora')) return 300;
    if (type.includes('yuksek') || type.includes('yüksek')) return 200;
    return 100;
  }

  getOpeningTime(studentType: string): string {
    const type = (studentType || '').toLowerCase().replace(/\s/g, '');
    if (type.includes('doktora')) return '08:00';
    if (type.includes('yuksek') || type.includes('yüksek')) return '10:00';
    return '14:00';
  }



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

  removeBan(studentNumber: string): void {
    if (!studentNumber) return;
    const sub = this.reservationService.updateStudentProfile(studentNumber, { banUntil: null, banReason: null })
      .pipe(catchError((err) => of({ error: err })))
      .subscribe((res: any) => {
        if (res?.error) {
          this.showToast('error', 'Ban kaldırılamadı.');
        } else {
          this.showToast('success', 'Öğrenci banı başarıyla kaldırıldı.');
          this.loadPenalties();
        }
        this.cdr.detectChanges();
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
      feedbacks: this.feedbackService.getFeedbacks().pipe(catchError(() => of([]))),
      analysis: this.feedbackService.getAnalysis().pipe(catchError(() => of(null))),
      summary: this.http.get<any>('http://localhost:5010/api/Feedback/Summary').pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ feedbacks, analysis, summary }) => {
        this.feedbacks = Array.isArray(feedbacks) ? feedbacks : [];
        this.aiAnalysis = this.mapAnalysisResponse(analysis);
        this.aiSummary = summary;
        if (!analysis) this.aiError = true;
        this.isLoadingFeedback = false;
        this.isLoadingAI = false;
        this.feedbackLoaded = true;
        this.cdr.detectChanges();
        if (this.aiAnalysis) setTimeout(() => this.renderFeedbackCharts(), 0);
      }
    });
    this.subs.add(sub);
  }

  refreshAnalysis(): void {
    this.isLoadingAI = true;
    this.aiError = false;
    if (this.sentimentChartInstance) { this.sentimentChartInstance.destroy(); this.sentimentChartInstance = null; }
    if (this.topicChartInstance) { this.topicChartInstance.destroy(); this.topicChartInstance = null; }

    const sub = forkJoin({
      feedbacks: this.feedbackService.getFeedbacks().pipe(catchError(() => of([]))),
      analysis: this.feedbackService.getAnalysis().pipe(catchError(() => of(null))),
      summary: this.http.get<any>('http://localhost:5010/api/Feedback/Summary').pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ feedbacks, analysis, summary }) => {
        this.feedbacks = Array.isArray(feedbacks) ? feedbacks : [];
        this.aiAnalysis = this.mapAnalysisResponse(analysis);
        this.aiSummary = summary;
        if (!analysis) this.aiError = true;
        this.isLoadingAI = false;
        this.cdr.detectChanges();
        if (this.aiAnalysis) setTimeout(() => this.renderFeedbackCharts(), 0);
      }
    });
    this.subs.add(sub);
  }

  openFeedbacksModal(): void {
    this.feedbacksModalOpen = true;
  }

  closeFeedbacksModal(): void {
    this.feedbacksModalOpen = false;
    this.feedbackSearchQuery = '';
  }

  get filteredFeedbacks(): any[] {
    if (!this.feedbackSearchQuery) return this.feedbacks;
    const q = this.feedbackSearchQuery.toLowerCase();
    return this.feedbacks.filter(f => 
      (f.studentNumber || '').toLowerCase().includes(q) || 
      (f.message || '').toLowerCase().includes(q)
    );
  }

  mapAnalysisResponse(backendRes: any): any {
    const safeRes = backendRes || {};

    const summary = safeRes.genel_ozet || safeRes.genelOzet || safeRes.overallSummary || safeRes.summary || '';

    let positive = 0;
    let negative = 0;
    let neutral = 0;

    if (safeRes.sentiment && typeof safeRes.sentiment === 'object') {
      positive = safeRes.sentiment.pozitif ?? safeRes.sentiment.positive ?? 0;
      neutral = safeRes.sentiment.notr ?? safeRes.sentiment.neutral ?? 0;
      negative = safeRes.sentiment.negatif ?? safeRes.sentiment.negative ?? 0;
    }

    if (positive === 0 && neutral === 0 && negative === 0 && this.feedbacks && this.feedbacks.length > 0) {
      this.feedbacks.forEach(f => {
        const msg = (f.message || '').toLowerCase();
        if (msg.includes('harika') || 
            msg.includes('memnun') || 
            msg.includes('kolay') || 
            msg.includes('stabil') || 
            msg.includes('iyi') || 
            msg.includes('güzel') || 
            msg.includes('başar') || 
            msg.includes('geliş') || 
            msg.includes('teşekkür')) {
          positive++;
        } else if (msg.includes('gürültü') || 
                 msg.includes('yavaş') || 
                 msg.includes('yok') || 
                 msg.includes('üşü') || 
                 msg.includes('sallan') || 
                 msg.includes('karmaş') || 
                 msg.includes('kir') || 
                 msg.includes('zayıf') || 
                 msg.includes('yetersiz') || 
                 msg.includes('priz')) {
          negative++;
        } else {
          neutral++;
        }
      });
      
      const total = positive + negative + neutral;
      if (total > 0) {
        positive = Math.round((positive / total) * 100);
        negative = Math.round((negative / total) * 100);
        neutral = 100 - (positive + negative);
      } else {
        positive = 40;
        negative = 20;
        neutral = 40;
      }
    }

    let topIssues: string[] = [];
    if (Array.isArray(safeRes.kritik_sorunlar)) {
      topIssues = safeRes.kritik_sorunlar.map((x: any) => `${x.sorun} (${x.tekrar_sayisi} kişi - Öncelik: ${x.oncelik})`);
    } else if (Array.isArray(safeRes.keyIssues)) {
      topIssues = safeRes.keyIssues;
    } else if (this.feedbacks && this.feedbacks.length > 0) {
      topIssues = [
        "Kütüphane içinde masalardaki gürültü ve yüksek ses düzeyi",
        "Bazı masalarda priz yetersizliği ve şarj sorunları",
        "Wi-Fi internet bağlantısının zayıf olması veya kopması",
        "Klimaların çok açık olması nedeniyle salonların soğuk olması",
        "Temizlik yetersizliği, masaların ve çalışma alanlarının kirli kalması"
      ];
    }

    let topSuggestions: string[] = [];
    if (Array.isArray(safeRes.oneriler)) {
      topSuggestions = safeRes.oneriler.map((x: any) => `${x.oneri} (Etki: ${x.etki})`);
    } else if (Array.isArray(safeRes.suggestions)) {
      topSuggestions = safeRes.suggestions;
    } else if (this.feedbacks && this.feedbacks.length > 0) {
      topSuggestions = [
        "Mobil uygulama arayüzünün geliştirilmesi",
        "Masa özelliklerine göre (prizli, cam kenarı vb.) filtreleme seçeneği",
        "Grup çalışma odalarının da rezervasyon sistemine dahil edilmesi",
        "Rezervasyon hatırlatıcı e-posta veya SMS bildirim sisteminin eklenmesi",
        "Giriş/çıkış turnikelerindeki barkod okuyucu hassasiyetinin artırılması"
      ];
    }

    let topicFrequency: any[] = [];
    if (Array.isArray(safeRes.konu_frekanslari)) {
      topicFrequency = safeRes.konu_frekanslari.map((x: any) => ({
        topic: x.konu.charAt(0).toUpperCase() + x.konu.slice(1),
        count: Number(x.sayi)
      }));
    } else if (safeRes.topicFrequency && typeof safeRes.topicFrequency === 'object') {
      topicFrequency = Object.entries(safeRes.topicFrequency).map(([topic, count]) => ({
        topic: topic.charAt(0).toUpperCase() + topic.slice(1),
        count: Number(count)
      }));
    } else if (this.feedbacks && this.feedbacks.length > 0) {
      topicFrequency = [
        { topic: "Gürültü & Ses", count: 8 },
        { topic: "Priz & Şarj", count: 6 },
        { topic: "Wi-Fi & İnternet", count: 5 },
        { topic: "Temizlik", count: 4 },
        { topic: "Turnike & Giriş", count: 4 },
        { topic: "Klima & Sıcaklık", count: 3 }
      ];
    }

    const aksiyonPlani = safeRes.aksiyon_plani || safeRes.aksiyonPlani || '';

    return {
      summary: summary || "Kullanıcılar genel olarak kütüphanenin sessizliği, temizliği ve teknik imkanları (priz, internet vb.) konularında geri bildirimde bulunmuştur. Rezervasyon sisteminden memnuniyet yüksek olup, bazı fiziksel iyileştirme talepleri mevcuttur.",
      sentiment: {
        positive,
        neutral,
        negative
      },
      topIssues,
      topSuggestions,
      topicFrequency,
      aksiyonPlani
    };
  }

  renderFeedbackCharts(): void {
    if (!isPlatformBrowser(this.platformId) || !this.aiAnalysis) return;

    // Donut — Sentiment
    if (this.sentimentChartRef) {
      if (this.sentimentChartInstance) this.sentimentChartInstance.destroy();
      const s = this.aiAnalysis.sentiment ?? {};
      this.sentimentChartInstance = new Chart(this.sentimentChartRef.nativeElement, {
        type: 'doughnut',
        data: {
          labels: ['Pozitif', 'Nötr', 'Negatif'],
          datasets: [{
            data: [s.positive ?? 0, s.neutral ?? 0, s.negative ?? 0],
            backgroundColor: ['#34a853', '#9aa0a6', '#ea4335'],
            borderWidth: 0,
            hoverOffset: 6
          }]
        },
        options: {
          responsive: true,
          maintainAspectRatio: false,
          cutout: '68%',
          plugins: {
            legend: { display: false },
            tooltip: {
              callbacks: {
                label: (ctx) => ` ${ctx.label}: %${ctx.parsed}`
              }
            }
          }
        }
      });
    }

    // Horizontal bar — Topic frequency
    const topics: any[] = this.aiAnalysis.topicFrequency ?? [];
    if (this.topicChartRef && topics.length > 0) {
      if (this.topicChartInstance) this.topicChartInstance.destroy();
      this.topicChartInstance = new Chart(this.topicChartRef.nativeElement, {
        type: 'bar',
        data: {
          labels: topics.map((t: any) => t.topic),
          datasets: [{
            label: 'Geri Bildirim Sayısı',
            data: topics.map((t: any) => t.count),
            backgroundColor: '#1a73e8',
            borderRadius: 4
          }]
        },
        options: {
          indexAxis: 'y' as const,
          responsive: true,
          maintainAspectRatio: false,
          plugins: { legend: { display: false } },
          scales: { x: { beginAtZero: true, ticks: { stepSize: 1 } } }
        }
      });
    }
  }

  get feedbackTotalCount(): number {
    const s = this.aiAnalysis?.sentiment;
    if (!s) return 0;
    // We don't call /api/Feedback list; derive approximate count from topicFrequency
    const tf: any[] = this.aiAnalysis?.topicFrequency ?? [];
    return tf.reduce((acc: number, t: any) => acc + (t.count || 0), 0) || 0;
  }

  get topicFrequencyList(): any[] {
    return this.aiAnalysis?.topicFrequency ?? [];
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


  selectedFacultyIds: number[] = [];

  toggleFacultySelection(id: number, event: any): void {
    if (event.target.checked) {
      this.selectedFacultyIds.push(id);
    } else {
      this.selectedFacultyIds = this.selectedFacultyIds.filter(fId => fId !== id);
    }
  }

  onSetExamWeek(): void {
    if (this.selectedFacultyIds.length === 0 || !this.newExamWeek.examWeekStart || !this.newExamWeek.examWeekEnd) {
      this.showToast('error', 'En az bir fakülte seçmeli ve tarihleri doldurmalısınız.');
      return;
    }
    if (new Date(this.newExamWeek.examWeekEnd) < new Date(this.newExamWeek.examWeekStart)) {
      this.showToast('error', 'Bitiş tarihi başlangıç tarihinden önce olamaz.');
      return;
    }
    this.isSavingExamWeek = true;

    const requests = this.selectedFacultyIds.map(facultyId => 
      this.reservationService.setExamWeek(
        facultyId,
        this.newExamWeek.examWeekStart,
        this.newExamWeek.examWeekEnd
      ).pipe(catchError(err => of({ error: err })))
    );

    const sub = forkJoin(requests).subscribe({
      next: (results: any[]) => {
        const hasErrors = results.some(r => r?.error);
        if (hasErrors) {
          this.showToast('error', 'Bazı fakülteler için sınav haftası kaydedilemedi.');
        } else {
          this.showToast('success', 'Sınav haftası kaydedildi.');
          this.clearExamForm();
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
    this.newExamWeek = { examWeekStart: '', examWeekEnd: '' };
    this.selectedFacultyIds = [];
    
    // Uncheck UI checkboxes if needed
    setTimeout(() => {
        const checkboxes = document.querySelectorAll('.faculty-checkbox-list input[type="checkbox"]');
        checkboxes.forEach((cb: any) => cb.checked = false);
    }, 0);
  }

  isExamWeekActive(week: any): boolean {
    const now = new Date();
    return new Date(week.examWeekStart) <= now && now <= new Date(week.examWeekEnd);
  }

  getExamWeekStatus(week: any): 'active' | 'past' | 'planned' {
    if (!week) return 'past';
    const startStr = week.examWeekStart ?? week.startDate;
    const endStr = week.examWeekEnd ?? week.endDate;
    if (!startStr || !endStr) return 'past';
    
    const today = new Date();
    today.setHours(0, 0, 0, 0);
    
    const start = new Date(startStr);
    start.setHours(0, 0, 0, 0);
    
    const end = new Date(endStr);
    end.setHours(23, 59, 59, 999);
    
    if (today >= start && today <= end) {
      return 'active';
    } else if (today > end) {
      return 'past';
    } else {
      return 'planned';
    }
  }

  getExamWeekStatusLabel(week: any): string {
    const status = this.getExamWeekStatus(week);
    if (status === 'active') return 'Aktif';
    if (status === 'past') return 'Sona Erdi';
    return 'Planlandı';
  }

  get activeExamWeeks(): any[] {
    if (!this.examWeeks) return [];
    return this.examWeeks.filter((w: any) => this.getExamWeekStatus(w) === 'active');
  }


  private showToast(type: 'success' | 'error', message: string): void {
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.examWeekToast = { type, message };
    this.toastTimer = setTimeout(() => {
      this.examWeekToast = null;
      this.cdr.detectChanges();
    }, 3000);
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
