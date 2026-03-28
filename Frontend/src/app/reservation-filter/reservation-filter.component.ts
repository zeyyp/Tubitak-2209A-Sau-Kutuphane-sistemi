import { Component, ChangeDetectorRef } from '@angular/core';
import { ReservationService } from '../services/reservation.service';
import { AuthService } from '../services/auth.service';
import { Router } from '@angular/router';
import { finalize, timeout } from 'rxjs';

@Component({
  selector: 'app-reservation-filter',
  templateUrl: './reservation-filter.component.html',
  standalone: false,
  styleUrls: ['./reservation-filter.component.css']
})
export class ReservationFilterComponent {
  filter = {
    date: '',
    startTime: '',
    endTime: '',
    floorId: 1
  };

  // Tarih kısıtlamaları: sadece bugün ve yarın
  minDate = '';
  maxDate = '';
  tomorrowAccessTime = '';
  canAccessTomorrow = false;

  tables: any[] = [];
  lastErrorMessage = '';
  lastErrorReason = '';
  lastSuccessMessage = '';
  lastNotificationType: 'success' | 'warning' | '' = '';
  isSubmitting = false;
  showConfirmationModal = false;
  selectedTable: any = null;

  // Puan bazlı erişim kontrolü
  accessCheckResult: any = null;
  accessDenied = false;
  checkingAccess = true;

  isTableAvailable(table: any): boolean {
    if (!table) {
      return false;
    }

    const raw = table.isAvailable ?? table.IsAvailable ?? table.available ?? table.Available;
    return raw === undefined ? true : !!raw;
  }

  constructor(
    private reservationService: ReservationService,
    private authService: AuthService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit() {
    // Tarih sınırlarını ayarla (bugün ve yarın)
    this.setDateLimits();

    if (typeof window !== 'undefined' && !this.authService.isLoggedIn()) {
      alert('Rezervasyon yapabilmek için önce giriş yapmalısınız.');
      this.router.navigate(['/login']);
      return;
    }

    // Puan bazlı erişim kontrolü
    this.checkAccessControl();
  }

  setDateLimits() {
    const today = new Date();
    const tomorrow = new Date(today);
    tomorrow.setDate(tomorrow.getDate() + 1);

    // YYYY-MM-DD formatı
    this.minDate = today.toISOString().split('T')[0];
    this.maxDate = tomorrow.toISOString().split('T')[0];

    // Varsayılan olarak bugünü seç
    this.filter.date = this.minDate;
  }

  selectDate(day: 'today' | 'tomorrow') {
    if (day === 'today') {
      this.filter.date = this.minDate;
    } else if (day === 'tomorrow' && this.canAccessTomorrow) {
      this.filter.date = this.maxDate;
    }
    this.cdr.detectChanges();
  }

  validateDate() {
    // Seçilen tarih geçerli aralıkta mı kontrol et
    if (this.filter.date < this.minDate) {
      this.filter.date = this.minDate;
      alert('Geçmiş tarih için rezervasyon yapamazsınız. Bugün seçildi.');
    } else if (this.filter.date > this.maxDate) {
      this.filter.date = this.maxDate;
      alert('En fazla yarın için rezervasyon yapabilirsiniz. Yarın seçildi.');
    }
    this.cdr.detectChanges();
  }

  checkAccessControl() {
    const studentNumber = this.authService.getCurrentUser();
    if (!studentNumber) {
      this.checkingAccess = false;
      return;
    }

    // Admin kontrolü - admin her zaman erişebilir
    if (studentNumber.toLowerCase() === 'admin') {
      this.checkingAccess = false;
      this.accessDenied = false;
      return;
    }

    this.reservationService.checkAccess(studentNumber).subscribe({
      next: (result) => {
        this.accessCheckResult = result;
        // Bugün için her zaman erişim açık, sadece yarın kilitli olabilir
        this.accessDenied = false; // Artık hiçbir zaman sistemi bloke etmiyoruz
        this.checkingAccess = false;

        // Yarın için erişim saatini kaydet
        if (result.allowedTime) {
          this.tomorrowAccessTime = result.allowedTime;
          // Şu anki saat erişim saatinden büyük mü kontrol et
          const now = new Date();
          const [hours, minutes] = result.allowedTime.split(':').map(Number);
          const accessTime = new Date();
          accessTime.setHours(hours, minutes, 0, 0);
          this.canAccessTomorrow = now >= accessTime;
        }

        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Erişim kontrolü hatası:', err);
        // Hata durumunda erişime izin ver (backend çalışmıyorsa vs)
        this.checkingAccess = false;
        this.accessDenied = false;
        this.cdr.detectChanges();
      }
    });
  }

  onSearch() {
    this.lastErrorMessage = '';
    this.lastErrorReason = '';
    this.lastSuccessMessage = '';
    this.lastNotificationType = '';
    this.cdr.detectChanges();

    this.reservationService
      .getTables(this.filter.date, this.filter.startTime, this.filter.endTime, this.filter.floorId)
      .subscribe({
        next: (data) => {
          this.tables = data;
          this.cdr.detectChanges();
        },
        error: (err) => {
          console.error('API hatası:', err);
          alert('Masalar getirilirken hata oluştu.');
          this.cdr.detectChanges();
        }
      });
  }

  onTableSelect(table: any) {
    console.log('Table selected:', table);
    if (this.isSubmitting) {
      console.warn('Already submitting, ignoring click.');
      return;
    }

    if (typeof window !== 'undefined' && !this.authService.isLoggedIn()) {
      alert('Rezervasyon yapmak için lütfen giriş yapınız.');
      this.router.navigate(['/login']);
      return;
    }

    if (!this.isTableAvailable(table)) {
      alert('Bu masa dolu!');
      return;
    }

    this.selectedTable = table;
    this.showConfirmationModal = true;
    this.cdr.detectChanges();
  }

  cancelReservation() {
    this.showConfirmationModal = false;
    this.selectedTable = null;
    this.cdr.detectChanges();
  }

  getTablesForRow(floorId: number, startNum: number, endNum: number): any[] {
    return this.tables.filter(table => {
      const tableNum = this.extractTableNumber(table.tableNumber || table.TableNumber);
      return tableNum >= startNum && tableNum <= endNum;
    });
  }

  extractTableNumber(tableNumber: string): number {
    const match = tableNumber.match(/\d+-(\d+)$/);
    return match ? parseInt(match[1], 10) : 0;
  }

  getTableLabel(table: any): string {
    const tableNumber = table.tableNumber || table.TableNumber;
    const num = this.extractTableNumber(tableNumber);
    return num.toString();
  }

  confirmReservation() {
    if (!this.selectedTable) return;

    const studentNumber = this.authService.getCurrentUser();
    if (!studentNumber) {
      alert('Öğrenci numarası bulunamadı. Lütfen tekrar giriş yapın.');
      this.router.navigate(['/login']);
      return;
    }

    this.showConfirmationModal = false;
    this.isSubmitting = true;
    this.cdr.detectChanges();

    const table = this.selectedTable;
    const selectedTableId = table.id ?? table.Id;
    const reservation = {
      tableId: selectedTableId,
      studentNumber: studentNumber,
      reservationDate: this.filter.date,
      startTime: this.filter.startTime,
      endTime: this.filter.endTime,
      studentType: this.authService.getAcademicLevel() ?? undefined
    };

    this.reservationService.createReservation(reservation)
      .pipe(
        timeout(8000),
        finalize(() => {
          this.isSubmitting = false;
          this.showConfirmationModal = false;
          this.selectedTable = null;
          this.cdr.detectChanges();
        })
      )
      .subscribe({
        next: (res) => {
          const message = res?.message ?? 'Rezervasyon başarıyla oluşturuldu!';
          this.lastSuccessMessage = message;
          this.lastErrorMessage = '';
          this.lastErrorReason = '';
          this.lastNotificationType = 'success';
          this.cdr.detectChanges();

          this.tables = this.tables.map(t => {
            if ((t.id ?? t.Id) === selectedTableId) {
              return { ...t, isAvailable: false, IsAvailable: false };
            }
            return t;
          });

          this.reservationService
            .getTables(this.filter.date, this.filter.startTime, this.filter.endTime, this.filter.floorId)
            .subscribe({
              next: refreshed => {
                this.tables = refreshed;
                this.cdr.detectChanges();
              },
              error: () => {
                // yenileme başarısız olursa mevcut liste kalır
              }
            });
        },
        error: (err) => {
          let message = err?.name === 'TimeoutError'
            ? 'Rezervasyon servisi yanıt vermedi. Lütfen tekrar deneyin.'
            : 'Rezervasyon oluşturulamadı.';
          let reason = '';

          const serverError = err?.error;
          if (typeof serverError === 'string' && serverError.trim().length > 0) {
            message = serverError;
          } else if (serverError && typeof serverError === 'object') {
            if (typeof serverError.message === 'string' && serverError.message.trim().length > 0) {
              message = serverError.message;
            }

            if (typeof serverError.reason === 'string' && serverError.reason.trim().length > 0) {
              reason = serverError.reason;
            }
          }

          this.lastErrorMessage = message;
          this.lastErrorReason = reason;
          this.lastSuccessMessage = '';
          this.lastNotificationType = 'warning';
          this.cdr.detectChanges();
        }
      });
  }
}
