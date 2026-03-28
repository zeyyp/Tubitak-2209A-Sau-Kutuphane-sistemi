import { Component, ChangeDetectorRef, OnInit } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { ReservationService } from '../services/reservation.service';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

@Component({
  selector: 'app-signup',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './signup.component.html',
  styleUrls: ['./signup.component.css']
})
export class SignupComponent implements OnInit {
  user = {
    studentNumber: '',
    fullName: '',
    email: '',
    password: '',
    academicLevel: 'Lisans',
    facultyId: 0,
    department: ''
  };
  faculties: any[] = [];
  isLoading: boolean = false;
  isLoadingFaculties: boolean = false;

  constructor(
    private authService: AuthService,
    private reservationService: ReservationService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    this.loadFaculties();
  }

  loadFaculties() {
    this.isLoadingFaculties = true;
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

  onSignup() {
    if (this.isLoading) return;

    // Fakülte ve bölüm kontrolü
    if (!this.user.facultyId || this.user.facultyId === 0) {
      alert('Lütfen fakülte seçiniz.');
      return;
    }

    if (!this.user.department || this.user.department.trim() === '') {
      alert('Lütfen bölüm giriniz.');
      return;
    }

    this.isLoading = true;
    this.cdr.detectChanges();

    console.log('Kayıt verisi:', this.user); // Debug için

    this.authService.register(this.user).subscribe({
      next: () => {
        this.isLoading = false;
        this.cdr.detectChanges();
        alert('Kayıt başarılı! Giriş yapabilirsiniz.');
        this.router.navigate(['/login']);
      },
      error: (err) => {
        this.isLoading = false;
        this.cdr.detectChanges();
        console.error(err);
        let errorMessage = 'Kayıt başarısız.\n';
        if (err.error && Array.isArray(err.error)) {
            err.error.forEach((e: any) => errorMessage += `- ${e.description}\n`);
        } else if (err.error && err.error.message) {
            errorMessage += err.error.message;
        } else {
            errorMessage += 'Lütfen bilgilerinizi kontrol ediniz.';
        }
        alert(errorMessage);
      }
    });
  }
}
