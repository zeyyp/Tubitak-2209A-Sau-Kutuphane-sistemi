import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { HttpClient } from '@angular/common/http';
import { catchError, of } from 'rxjs';

@Component({
  selector: 'app-landing',
  templateUrl: './landing.component.html',
  styleUrls: ['./landing.component.css'],
  standalone: true,
  imports: [CommonModule, RouterModule]
})
export class LandingComponent implements OnInit {
  private readonly STATS_URL = 'http://localhost:5010/api/Reservation/Stats';
  private readonly TOTAL_SEATS = 500;

  // Stats widget
  statsLoading = true;
  statsVisible = false;
  occupancyRate = 0;
  availableSeats = 0;
  quietestFloor = '';

  // FAQ accordion — parallel boolean array
  faqItems = [
    {
      q: 'Rezervasyon yaptığım masaya ne kadar sürede gitmeliyim?',
      a: 'Rezervasyon başlangıç saatinden itibaren 15 dakika içinde turnike girişi yapmalısınız. Aksi halde sistem otomatik no-show kaydeder ve 2 gün ban uygulanır.',
      open: false
    },
    {
      q: 'Akademik öncelik puanı nedir?',
      a: 'Doktora: 300, Yüksek Lisans: 200, Sınav haftasındaki Lisans: 150, Normal Lisans: 100 puan. Yüksek puanlı öğrenciler ertesi gün rezervasyonlarını daha erken açar.',
      open: false
    },
    {
      q: 'Rezervasyonumu nasıl iptal edebilirim?',
      a: "Profil sayfanızdan veya Ana Sayfa'daki aktif rezervasyon kartından iptal edebilirsiniz.",
      open: false
    },
    {
      q: 'Günlük rezervasyon sınırı var mı?',
      a: 'Aynı anda en fazla 2 aktif rezervasyonunuz olabilir. Her rezervasyon en az 1, en fazla 4 saat olabilir.',
      open: false
    }
  ];

  constructor(private http: HttpClient, private cdr: ChangeDetectorRef) {}

  ngOnInit(): void {
    this.loadStats();
  }

  loadStats(): void {
    this.statsLoading = true;
    this.http.get<any>(this.STATS_URL).pipe(
      catchError(() => of(null))
    ).subscribe(data => {
      if (!data) {
        this.statsLoading = false;
        this.statsVisible = false;
        this.cdr.detectChanges();
        return;
      }
      const total = data.totalReservations ?? data.TotalReservations ?? 0;
      this.occupancyRate  = Math.min(100, Math.round((total / this.TOTAL_SEATS) * 100));
      this.availableSeats = Math.max(0, this.TOTAL_SEATS - total);
      this.quietestFloor  = this.calcQuietest(data);
      this.statsLoading   = false;
      this.statsVisible   = true;
      this.cdr.detectChanges();
    });
  }

  private calcQuietest(data: any): string {
    const byHour: Record<string, number> = data.reservationsByHour ?? data.ReservationsByHour ?? {};
    // Backend groups by hour but not by floor; use occupancy heuristic
    if (this.occupancyRate < 40) return '3. Kat';
    if (this.occupancyRate < 70) return '2. Kat';
    return '1. Kat';
  }

  toggleFaq(index: number): void {
    this.faqItems[index].open = !this.faqItems[index].open;
  }

  scrollTo(id: string): void {
    const el = document.getElementById(id);
    if (el) el.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }
}
