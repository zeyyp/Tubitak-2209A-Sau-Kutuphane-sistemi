import { Component, OnInit, inject, PLATFORM_ID, ChangeDetectorRef } from '@angular/core';
import { CommonModule, DatePipe, isPlatformBrowser } from '@angular/common';
import { RouterLink } from '@angular/router';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { FormsModule } from '@angular/forms';
import { forkJoin, catchError, of, finalize } from 'rxjs';

export interface ProfileInfo {
  banUntil?: string | null;
  banReason?: string | null;
  studentType?: string;
  facultyId?: number;
  department?: string;
}

export interface Reservation {
  id: number;
  tableId: number;
  tableNumber?: string;
  floorId: number | null;
  reservationDate: string;
  startTime: string;
  endTime: string;
  isAttended: boolean;
  penaltyProcessed: boolean;
  status?: string;
}

export interface Feedback {
  id: number;
  message: string;
  createdAt: string;
}

export interface Faculty {
  id: number;
  name: string;
}

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [CommonModule, RouterLink, FormsModule],
  providers: [DatePipe],
  templateUrl: './profile.component.html',
  styleUrls: ['./profile.component.css']
})
export class ProfileComponent implements OnInit {
  private http = inject(HttpClient);
  private cdr = inject(ChangeDetectorRef);

  // Local Storage Data
  studentNumber = '';
  fullName = '';
  academicLevel = '';
  email = '';

  // API Data
  profileInfo: ProfileInfo | null = null;
  allReservations: Reservation[] = [];
  feedbacks: Feedback[] = [];
  faculties: Faculty[] = [];

  // UI States
  isLoading = true;
  activeTab: 'active' | 'history' | 'penalty' | 'feedback' = 'active';
  
  // Computed & Separated Data
  activeReservations: Reservation[] = [];
  historyReservations: Reservation[] = [];
  penaltyReservations: Reservation[] = [];
  
  // Stats
  totalReservations = 0;
  attendedCount = 0;
  noShowCount = 0;
  attendanceRate = 0;

  // Form State
  updateFacultyId: number | null = null;
  updateDepartment = '';
  isUpdating = false;
  toastMessage: { text: string; type: 'success' | 'error' } | null = null;
  
  // Reservation Cancellation State
  cancelingId: number | null = null;
  confirmCancelId: number | null = null;

  floorMap: Record<number, string> = {
    1: '1. Kat',
    2: '2. Kat',
    3: '3. Kat'
  };

  private platformId = inject(PLATFORM_ID);

  ngOnInit(): void {
    if (isPlatformBrowser(this.platformId)) {
      this.studentNumber = localStorage.getItem('current_user') || '';
      this.fullName = localStorage.getItem('full_name') || '';
      this.academicLevel = localStorage.getItem('academic_level') || '';
      this.email = localStorage.getItem('email') || '';
      this.loadData();
    }
  }

  getHeaders() {
    const token = localStorage.getItem('access_token') || '';
    return new HttpHeaders().set('Authorization', `Bearer ${token}`);
  }

  loadData() {
    this.isLoading = true;
    const headers = this.getHeaders();
    
    const profileReq = this.http.get<ProfileInfo>(`http://localhost:5010/api/Reservation/Profile/${this.studentNumber}`, { headers }).pipe(
      catchError(err => {
        console.error('Profile fetch error', err);
        return of({} as ProfileInfo);
      })
    );
    
    const reservationsReq = this.http.get<Reservation[]>(`http://localhost:5010/api/Reservation/MyReservations?studentNumber=${this.studentNumber}`, { headers }).pipe(
      catchError(err => {
        console.error('Reservations fetch error', err);
        return of([] as Reservation[]);
      })
    );
    
    const feedbacksReq = this.http.get<Feedback[]>(`http://localhost:5010/api/Feedback?studentNumber=${this.studentNumber}`, { headers }).pipe(
      catchError(err => {
        console.error('Feedbacks fetch error', err);
        return of([] as Feedback[]);
      })
    );
    
    const facultiesReq = this.http.get<Faculty[]>(`http://localhost:5010/api/Reservation/Faculties`, { headers }).pipe(
      catchError(err => {
        console.error('Faculties fetch error', err);
        return of([] as Faculty[]);
      })
    );

    forkJoin([profileReq, reservationsReq, feedbacksReq, facultiesReq]).subscribe(([profile, reservations, feedbacks, faculties]) => {
      console.log('forkJoin completed!', { profile, reservations, feedbacks, faculties });
      this.profileInfo = profile;
      this.allReservations = reservations;
      this.feedbacks = feedbacks;
      this.faculties = faculties;

      this.updateFacultyId = profile?.facultyId || null;
      this.updateDepartment = profile?.department || '';

      this.processReservations();
      this.calculateStats();
      this.isLoading = false;
      this.cdr.detectChanges();
      console.log('isLoading set to', this.isLoading);
    });
  }

  processReservations() {
    const now = new Date();
    // Reset arrays
    this.activeReservations = [];
    this.historyReservations = [];
    this.penaltyReservations = [];

    this.allReservations.forEach(res => {
      const resDate = new Date(res.reservationDate);
      
      const isPast = resDate < new Date(now.toDateString());
      const isCancelled = res.status === 'Cancelled';
      
      if (!isPast && !isCancelled) {
        this.activeReservations.push(res);
      } else {
        this.historyReservations.push(res);
      }
      
      if (res.penaltyProcessed) {
        this.penaltyReservations.push(res);
      }
    });

    this.historyReservations.sort((a, b) => new Date(b.reservationDate).getTime() - new Date(a.reservationDate).getTime());
    this.activeReservations.sort((a, b) => new Date(a.reservationDate).getTime() - new Date(b.reservationDate).getTime());
  }

  calculateStats() {
    this.totalReservations = this.allReservations.length;
    this.attendedCount = this.allReservations.filter(r => r.isAttended).length;
    this.noShowCount = this.allReservations.filter(r => !r.isAttended && r.penaltyProcessed).length;
    
    if (this.totalReservations > 0) {
      this.attendanceRate = Math.round((this.attendedCount / this.totalReservations) * 100);
    } else {
      this.attendanceRate = 0;
    }
  }

  getAvatarInitials(): string {
    if (!this.fullName) return 'U';
    const parts = this.fullName.trim().split(' ');
    if (parts.length >= 2) {
      return (parts[0][0] + parts[parts.length - 1][0]).toUpperCase();
    }
    return this.fullName.substring(0, 2).toUpperCase();
  }

  getAvatarColor(): string {
    if (!this.fullName) return '#3b82f6';
    const colors = ['#3b82f6', '#8b5cf6', '#10b981', '#0ea5e9', '#6366f1', '#f59e0b', '#ec4899'];
    const charCode = this.fullName.charCodeAt(0) || 0;
    return colors[charCode % colors.length];
  }

  getBaseScore(): number {
    switch (this.academicLevel) {
      case 'Doktora': return 300;
      case 'YüksekLisans': return 200;
      case 'Lisans': return 100;
      default: return 100;
    }
  }

  getLevelColor(): string {
    switch (this.academicLevel) {
      case 'Doktora': return '#8b5cf6'; // mor
      case 'YüksekLisans': return '#3b82f6'; // mavi
      case 'Lisans': return '#10b981'; // yeşil
      default: return '#6c757d'; // gri
    }
  }

  getOpeningTime(): { time: string, color: string } {
    switch (this.academicLevel) {
      case 'Doktora': return { time: "08:00'den itibaren rezervasyon açık", color: '#8b5cf6' };
      case 'YüksekLisans': return { time: "10:00'den itibaren", color: '#3b82f6' };
      case 'Lisans': return { time: "14:00'den itibaren", color: '#10b981' };
      default: return { time: "14:00'den itibaren", color: '#10b981' };
    }
  }

  getFacultyName(id?: number): string {
    if (!id) return '—';
    const f = this.faculties.find(x => x.id == id);
    return f ? f.name : '—';
  }

  isBanned(): boolean {
    if (!this.profileInfo?.banUntil) return false;
    return new Date(this.profileInfo.banUntil) > new Date();
  }

  getBanUntilDate(): Date | null {
    if (!this.profileInfo?.banUntil) return null;
    return new Date(this.profileInfo.banUntil);
  }

  getPenaltyEndDate(reservationDate: string): Date {
    const d = new Date(reservationDate);
    d.setDate(d.getDate() + 2);
    return d;
  }

  cancelReservation(id: number) {
    this.cancelingId = id;
    this.cdr.detectChanges();
    this.http.delete(`http://localhost:5010/api/Reservation/Cancel/${id}`, { headers: this.getHeaders() })
      .pipe(
        finalize(() => {
          this.cancelingId = null;
          this.confirmCancelId = null;
          this.cdr.detectChanges();
        })
      )
      .subscribe({
        next: () => {
          this.activeReservations = this.activeReservations.filter(r => r.id !== id);
          const idx = this.allReservations.findIndex(r => r.id === id);
          if (idx !== -1) {
            this.allReservations[idx].status = 'Cancelled';
          }
          this.calculateStats();
          this.cdr.detectChanges();
        },
        error: (err) => {
          console.error('Cancel error', err);
          this.showToast('İptal işlemi başarısız oldu.', 'error');
        }
      });
  }

  updateProfile() {
    if (!this.updateFacultyId) return;
    this.isUpdating = true;
    
    const payload = {
      studentNumber: this.studentNumber,
      facultyId: Number(this.updateFacultyId),
      department: this.updateDepartment
    };

    this.http.post(`http://localhost:5010/api/Reservation/UpdateStudentDepartment`, payload, { headers: this.getHeaders() })
      .pipe(
        finalize(() => this.isUpdating = false)
      )
      .subscribe({
        next: () => {
          this.showToast('Bilgileriniz güncellendi.', 'success');
          if (this.profileInfo) {
            this.profileInfo.facultyId = payload.facultyId;
            this.profileInfo.department = payload.department;
          }
        },
        error: (err) => {
          console.error('Update error', err);
          this.showToast('Güncelleme başarısız.', 'error');
        }
      });
  }

  showToast(text: string, type: 'success' | 'error') {
    this.toastMessage = { text, type };
    setTimeout(() => {
      this.toastMessage = null;
    }, 3000);
  }
}
