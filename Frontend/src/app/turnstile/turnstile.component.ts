import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Router } from '@angular/router';
import { catchError, of } from 'rxjs';

interface TurnstileResult {
  type: 'success' | 'rejected' | 'error';
  title: string;
  message: string;
}

interface LogEntry {
  studentNumber: string;
  enteredAt: string;
  success: boolean;
}

@Component({
  selector: 'app-turnstile',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './turnstile.component.html',
  styleUrls: ['./turnstile.component.css']
})
export class TurnstileComponent implements OnInit, OnDestroy {
  private readonly apiBase = 'http://localhost:5010';

  studentNumber = '';
  isLoading = false;
  result: TurnstileResult | null = null;
  logs: LogEntry[] = [];
  logsLoading = false;

  private autoCloseTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(private http: HttpClient, private router: Router) {}

  ngOnInit(): void {
    this.loadLogs();
  }

  ngOnDestroy(): void {
    if (this.autoCloseTimer) clearTimeout(this.autoCloseTimer);
  }

  private getHeaders(): HttpHeaders {
    const token = localStorage.getItem('accessToken') ?? '';
    return new HttpHeaders({ Authorization: `Bearer ${token}` });
  }

  onEnter(): void {
    const trimmed = this.studentNumber.trim();
    if (!trimmed || this.isLoading) return;
    this.submitEntry(trimmed);
  }

  private submitEntry(studentNumber: string): void {
    if (this.autoCloseTimer) clearTimeout(this.autoCloseTimer);
    this.result = null;
    this.isLoading = true;

    this.http
      .post<{ doorOpen: boolean; message: string }>(
        `${this.apiBase}/api/Turnstile/Enter`,
        { studentNumber },
        { headers: this.getHeaders() }
      )
      .pipe(
        catchError(err => {
          const msg = err?.error?.message ?? 'Sisteme bağlanılamadı. Lütfen tekrar deneyin.';
          return of({ doorOpen: false, message: msg, _isError: true } as any);
        })
      )
      .subscribe(res => {
        this.isLoading = false;
        const isError = !!(res as any)._isError;

        if (isError) {
          this.result = { type: 'error', title: 'Bağlantı Hatası', message: res.message };
          this.scheduleClose(4000);
        } else if (res.doorOpen) {
          this.result = { type: 'success', title: 'Giriş Başarılı', message: res.message };
          this.studentNumber = '';
          this.scheduleClose(3000);
        } else {
          this.result = { type: 'rejected', title: 'Giriş Reddedildi', message: res.message };
          this.scheduleClose(4000);
        }
        this.loadLogs();
      });
  }

  private scheduleClose(ms: number): void {
    this.autoCloseTimer = setTimeout(() => {
      this.result = null;
      this.studentNumber = '';
    }, ms);
  }

  loadLogs(): void {
    this.logsLoading = true;
    this.http
      .get<LogEntry[]>(`${this.apiBase}/api/Turnstile/logs?take=10`, {
        headers: this.getHeaders()
      })
      .pipe(catchError(() => of([])))
      .subscribe(data => {
        this.logs = data ?? [];
        this.logsLoading = false;
      });
  }

  formatDate(dateStr: string): string {
    if (!dateStr) return '';
    const d = new Date(dateStr);
    const day = d.getDate();
    const months = ['Oca','Şub','Mar','Nis','May','Haz','Tem','Ağu','Eyl','Eki','Kas','Ara'];
    const mon = months[d.getMonth()];
    const hh = String(d.getHours()).padStart(2, '0');
    const mm = String(d.getMinutes()).padStart(2, '0');
    return `${day} ${mon}, ${hh}:${mm}`;
  }
}
