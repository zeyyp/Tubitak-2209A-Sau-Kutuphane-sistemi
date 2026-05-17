import { Component, OnInit, OnDestroy, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { forkJoin, of, Subscription, interval } from 'rxjs';
import { catchError, startWith, switchMap } from 'rxjs/operators';
import { AuthService } from '../services/auth.service';

@Component({
  selector: 'app-home',
  templateUrl: './home.component.html',
  styleUrls: ['./home.component.css'],
  standalone: true,
  imports: [CommonModule, RouterModule]
})
export class HomeComponent implements OnInit, OnDestroy {
  private readonly API = 'http://localhost:5010/api/Reservation';

  // Auth
  fullName   = '';
  studentNumber = '';
  academicLevel = '';

  // Loading / Error
  loading = true;
  error   = '';

  // Profile
  profile: any = null;

  // Active reservation
  activeReservation: any = null;
  remainingMs = 0;
  countdownDisplay = '';
  private timerSub?: Subscription;

  // Exam weeks
  examWeeks: any[] = [];
  activeExamWeek: any = null;

  // Stats
  stats: any = null;
  occupancyRate = 0;
  availableSeats = 0;
  quietestFloor = '';

  // Cancelling
  cancelling = false;
  cancelError = '';

  constructor(
    private auth: AuthService,
    private http: HttpClient,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    if (typeof window === 'undefined') return;

    this.studentNumber = this.auth.getCurrentUser() ?? '';
    this.fullName      = localStorage.getItem('full_name') ?? this.studentNumber;
    this.academicLevel = this.auth.getAcademicLevel() ?? '';

    if (!this.studentNumber) {
      this.router.navigate(['/login']);
      return;
    }

    this.loadAll();
  }

  ngOnDestroy(): void {
    this.timerSub?.unsubscribe();
  }

  private headers(): HttpHeaders {
    return new HttpHeaders({ Authorization: `Bearer ${this.auth.getToken() ?? ''}` });
  }

  loadAll(): void {
    this.loading = true;
    const hdrs = { headers: this.headers() };

    forkJoin({
      profile: this.http.get<any>(`${this.API}/Profile/${this.studentNumber}`, hdrs).pipe(catchError(() => of(null))),
      reservations: this.http.get<any[]>(`${this.API}/MyReservations?studentNumber=${this.studentNumber}`, hdrs).pipe(catchError(() => of([]))),
      examWeeks: this.http.get<any[]>(`${this.API}/ExamWeeks`, hdrs).pipe(catchError(() => of([]))),
      stats: this.http.get<any>(`${this.API}/Stats`, hdrs).pipe(catchError(() => of(null)))
    }).subscribe({
      next: ({ profile, reservations, examWeeks, stats }) => {
        this.profile   = profile;
        this.examWeeks = examWeeks ?? [];

        // Active reservation: today, not cancelled, not attended-over
        const today = new Date().toISOString().split('T')[0];
        this.activeReservation = (reservations ?? []).find((r: any) => {
          const d = (r.reservationDate ?? r.ReservationDate ?? '').substring(0, 10);
          return d === today && !(r.isCancelled ?? r.IsCancelled);
        }) ?? null;

        if (this.activeReservation) this.startCountdown();

        // Exam week for student's faculty
        if (profile?.facultyId) {
          const todayDate = new Date(today);
          this.activeExamWeek = this.examWeeks.find((e: any) => {
            const start = new Date(e.examWeekStart ?? e.ExamWeekStart);
            const end   = new Date(e.examWeekEnd   ?? e.ExamWeekEnd);
            return e.facultyId === profile.facultyId && todayDate >= start && todayDate <= end;
          }) ?? null;
        }

        // Stats
        this.stats = stats;
        if (stats) {
          const total = stats.totalReservations ?? stats.TotalReservations ?? 0;
          const TOTAL_SEATS = 500;
          this.availableSeats = Math.max(0, TOTAL_SEATS - total);
          this.occupancyRate  = Math.min(100, Math.round((total / TOTAL_SEATS) * 100));
          this.quietestFloor  = this.calcQuietestFloor(stats);
        }

        this.loading = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.error   = 'Veriler yüklenirken bir hata oluştu.';
        this.loading = false;
        this.cdr.detectChanges();
      }
    });
  }

  private startCountdown(): void {
    this.timerSub?.unsubscribe();
    this.timerSub = interval(1000).pipe(startWith(0)).subscribe(() => {
      if (!this.activeReservation) return;
      const endTime = this.activeReservation.endTime ?? this.activeReservation.EndTime ?? '';
      const date    = (this.activeReservation.reservationDate ?? this.activeReservation.ReservationDate ?? '').substring(0, 10);
      if (!endTime || !date) return;

      const end = new Date(`${date}T${endTime}`);
      const now = new Date();
      const diff = end.getTime() - now.getTime();

      if (diff <= 0) {
        this.countdownDisplay = 'Süre doldu';
        this.timerSub?.unsubscribe();
        return;
      }

      const h = Math.floor(diff / 3600000);
      const m = Math.floor((diff % 3600000) / 60000);
      const s = Math.floor((diff % 60000) / 1000);
      this.countdownDisplay = `${h > 0 ? h + 's ' : ''}${m}dk ${s}s`;
      this.cdr.detectChanges();
    });
  }

  private calcQuietestFloor(stats: any): string {
    const byHour: Record<string, number> = stats.reservationsByHour ?? stats.ReservationsByHour ?? {};
    // We use a heuristic: show floor based on overall data
    // Backend doesn't give per-floor data so we show a static label
    if (this.occupancyRate < 50) return '2. Kat';
    return '1. Kat';
  }

  cancelActive(): void {
    if (!this.activeReservation || this.cancelling) return;
    const id = this.activeReservation.id ?? this.activeReservation.Id;
    this.cancelling  = true;
    this.cancelError = '';
    this.http.delete(`${this.API}/Cancel/${id}`, { headers: this.headers() }).subscribe({
      next: () => {
        this.activeReservation = null;
        this.timerSub?.unsubscribe();
        this.cancelling = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.cancelError = 'İptal işlemi başarısız.';
        this.cancelling  = false;
        this.cdr.detectChanges();
      }
    });
  }

  get isBanned(): boolean {
    if (!this.profile?.banUntil && !this.profile?.BanUntil) return false;
    const ban = new Date(this.profile.banUntil ?? this.profile.BanUntil);
    return ban >= new Date();
  }

  get banUntilFormatted(): string {
    const raw = this.profile?.banUntil ?? this.profile?.BanUntil;
    if (!raw) return '';
    return new Date(raw).toLocaleDateString('tr-TR');
  }

  get studentScore(): number {
    return this.profile?.score ?? this.profile?.Score ?? 0;
  }

  get studentTypeLabel(): string {
    const t = this.profile?.studentType ?? this.profile?.StudentType ?? this.academicLevel;
    const map: Record<string, string> = {
      Doktora: 'Doktora', YüksekLisans: 'Yüksek Lisans', Lisans: 'Lisans'
    };
    return map[t] ?? t ?? '—';
  }

  get tableLabel(): string {
    const r = this.activeReservation;
    if (!r) return '';
    return r.tableNumber ?? r.TableNumber ?? `Masa #${r.tableId ?? r.TableId ?? ''}`;
  }

  get floorLabel(): string {
    const r = this.activeReservation;
    if (!r) return '';
    const fid = r.floorId ?? r.FloorId ?? '';
    const labels: Record<number, string> = { 1: '1. Kat', 2: '2. Kat', 3: '3. Kat' };
    return labels[fid] ?? `Kat ${fid}`;
  }

  get timeRange(): string {
    const r = this.activeReservation;
    if (!r) return '';
    const s = r.startTime ?? r.StartTime ?? '';
    const e = r.endTime   ?? r.EndTime   ?? '';
    return `${s} — ${e}`;
  }

  get reservationDateFormatted(): string {
    const raw = this.activeReservation?.reservationDate ?? this.activeReservation?.ReservationDate ?? '';
    if (!raw) return '';
    return new Date(raw).toLocaleDateString('tr-TR');
  }
}
