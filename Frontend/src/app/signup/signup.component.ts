import { Component, OnInit, ChangeDetectorRef } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { AuthService } from '../services/auth.service';
import { ReservationService } from '../services/reservation.service';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators, AbstractControl, ValidationErrors } from '@angular/forms';

@Component({
  selector: 'app-signup',
  standalone: true,
  imports: [CommonModule, ReactiveFormsModule, RouterLink],
  templateUrl: './signup.component.html',
  styleUrls: ['./signup.component.css']
})
export class SignupComponent implements OnInit {
  signupForm: FormGroup;
  faculties: any[] = [];
  availableDepartments: string[] = [];
  isLoadingFaculties = false;
  isSubmitting = false;

  showPassword = false;
  showPasswordConfirm = false;

  errorMessage = '';
  showErrorBanner = false;

  departmentsMap: { [key: string]: string[] } = {
    'Fen Fakültesi': ['FİZİK PR.', 'MATEMATİK PR.', 'KİMYA PR.', 'FİZİK PR. (YL)', 'MATEMATİK PR. (DR)'],
    'Mühendislik Fakültesi': ['MAKİNE MÜHENDİSLİĞİ PR.', 'ELEKTRİK-ELEKTRONİK MÜH. PR.', 'İNŞAAT MÜHENDİSLİĞİ PR.', 'MAKİNE MÜH. (YL)', 'İNŞAAT MÜH. (DR)'],
    'Tıp Fakültesi': ['TIP PR.', 'TEMEL TIP BİLİMLERİ (DR)'],
    'Bilgisayar ve Bilişim Bilimleri Fakültesi': ['BİLGİSAYAR MÜHENDİSLİĞİ PR.', 'BİLİŞİM SİSTEMLERİ MÜH. PR.', 'YAZILIM MÜHENDİSLİĞİ PR.', 'BİLGİSAYAR MÜH. (YL)', 'YAZILIM MÜH. (DR)'],
    'Sağlık Bilimleri Fakültesi': ['HEMŞİRELİK PR.', 'EBELİK PR.', 'FİZYOTERAPİ VE REHABİLİTASYON PR.', 'HEMŞİRELİK (YL)'],
    'Diş Hekimliği Fakültesi': ['DİŞ HEKİMLİĞİ PR.', 'ORTODONTİ (DR)'],
    'Hukuk Fakültesi': ['HUKUK PR.', 'KAMU HUKUKU (YL)', 'ÖZEL HUKUK (DR)'],
    'Eğitim Fakültesi': ['REHBERLİK VE PSİKOLOJİK DANIŞMANLIK PR.', 'ÖZEL EĞİTİM ÖĞRETMENLİĞİ PR.', 'SINIF ÖĞRETMENLİĞİ PR. (YL)'],
    'İnsan ve Toplum Bilimleri Fakültesi': ['TARİH PR.', 'TÜRK DİLİ VE EDEBİYATI PR.', 'SOSYOLOJİ PR.', 'TARİH (YL)', 'SOSYOLOJİ (DR)'],
    'İşletme Fakültesi': ['İŞLETME PR.', 'ULUSLARARASI TİCARET VE FİNANSMAN PR.', 'YÖNETİM BİLİŞİM SİSTEMLERİ PR.', 'İŞLETME (YL)'],
    'İlahiyat Fakültesi': ['İLAHİYAT PR.', 'TEMEL İSLAM BİLİMLERİ (YL)', 'İSLAM TARİHİ VE SANATLARI (DR)'],
    'İletişim Fakültesi': ['GAZETECİLİK PR.', 'HALKLA İLİŞKİLER VE REKLAMCILIK PR.', 'RADYO TELEVİZYON VE SİNEMA PR.', 'İLETİŞİM BİLİMLERİ (YL)'],
    'Sanat Tasarım ve Mimarlık Fakültesi': ['MİMARLIK PR.', 'GÖRSEL İLETİŞİM TASARIMI PR.', 'MİMARLIK (DR)'],
    'Siyasal Bilgiler Fakültesi': ['SİYASET BİLİMİ VE KAMU YÖNETİMİ PR.', 'ULUSLARARASI İLİŞKİLER PR.', 'ULUSLARARASI İLİŞKİLER (YL)'],
    'Teknik Eğitim Fakültesi': ['ELEKTRONİK ÖĞRETMENLİĞİ PR.', 'MAKİNE ÖĞRETMENLİĞİ PR.']
  };

  constructor(
    private fb: FormBuilder,
    private authService: AuthService,
    private reservationService: ReservationService,
    private router: Router,
    private cdr: ChangeDetectorRef
  ) {
    this.signupForm = this.fb.group({
      fullName: ['', [Validators.required, Validators.minLength(3)]],
      studentNumber: ['', [Validators.required]],
      academicLevel: ['', [Validators.required]],
      facultyId: [0, [Validators.required, Validators.min(1)]],
      department: [{ value: '', disabled: true }, [Validators.required]],
      email: ['', [Validators.email]],
      password: ['', [Validators.required, Validators.minLength(8), Validators.pattern('^(?=.*[A-Z])(?=.*\\d).+$')]],
      passwordConfirm: ['', [Validators.required]]
    }, { validators: this.matchPasswords });
  }

  ngOnInit(): void {
    this.loadFaculties();

    this.signupForm.get('facultyId')?.valueChanges.subscribe(value => {
      const deptControl = this.signupForm.get('department');
      if (value && value > 0) {
        const selectedFaculty = this.faculties.find(f => f.id == value);
        if (selectedFaculty && this.departmentsMap[selectedFaculty.name]) {
            this.availableDepartments = this.departmentsMap[selectedFaculty.name];
        } else {
            this.availableDepartments = [];
        }
        deptControl?.enable();
        deptControl?.setValue('');
      } else {
        this.availableDepartments = [];
        deptControl?.disable();
        deptControl?.setValue('');
      }
      this.cdr.detectChanges();
    });

    this.signupForm.get('department')?.valueChanges.subscribe(value => {
      if (value) {
        const academicLvlControl = this.signupForm.get('academicLevel');
        if (value.includes('(YL)')) {
          academicLvlControl?.setValue('YüksekLisans');
        } else if (value.includes('(DR)')) {
          academicLvlControl?.setValue('Doktora');
        } else if (value.includes('PR.')) {
          academicLvlControl?.setValue('Lisans');
        }
        this.cdr.detectChanges();
      }
    });
  }

  matchPasswords(group: AbstractControl): ValidationErrors | null {
    const password = group.get('password')?.value;
    const confirm = group.get('passwordConfirm')?.value;
    if (!password || !confirm) return null;
    return password === confirm ? null : { passwordsMismatch: true };
  }

  loadFaculties() {
    this.isLoadingFaculties = true;
    this.cdr.detectChanges();
    this.reservationService.getFaculties().subscribe({
      next: (data) => {
        this.faculties = data;
        this.isLoadingFaculties = false;
        this.cdr.detectChanges();
      },
      error: () => {
        this.isLoadingFaculties = false;
        this.cdr.detectChanges();
      }
    });
  }

  togglePassword() {
    this.showPassword = !this.showPassword;
  }

  togglePasswordConfirm() {
    this.showPasswordConfirm = !this.showPasswordConfirm;
  }

  displayError(message: string) {
    this.errorMessage = message;
    this.showErrorBanner = true;
    this.cdr.detectChanges();
    setTimeout(() => {
      this.showErrorBanner = false;
      this.cdr.detectChanges();
    }, 5000);
  }

  onSubmit() {
    if (this.signupForm.invalid) {
      this.signupForm.markAllAsTouched();
      return;
    }

    this.isSubmitting = true;
    this.cdr.detectChanges();
    const formValue = this.signupForm.getRawValue();

    const registerData = {
      studentNumber: formValue.studentNumber,
      fullName: formValue.fullName,
      academicLevel: formValue.academicLevel,
      email: formValue.email || '',
      password: formValue.password,
      facultyId: Number(formValue.facultyId),
      department: formValue.department
    };

    this.authService.register(registerData).subscribe({
      next: () => {
        this.authService.login(formValue.studentNumber, formValue.password).subscribe({
          next: () => {
            this.isSubmitting = false;
            this.cdr.detectChanges();
            this.router.navigate(['/']);
          },
          error: (err) => {
            console.error('Otomatik giriş başarısız:', err);
            this.isSubmitting = false;
            this.displayError('Kayıt başarılı ancak otomatik giriş yapılamadı. Lütfen manuel giriş yapın.');
            this.cdr.detectChanges();
            setTimeout(() => this.router.navigate(['/login']), 2000);
          }
        });
      },
      error: (err) => {
        this.isSubmitting = false;
        if (err.status === 409) {
          this.displayError('Bu öğrenci numarası zaten kayıtlı. Giriş yapmayı deneyin.');
        } else {
          this.displayError('Kayıt sırasında bir hata oluştu. Lütfen tekrar deneyin.');
        }
        this.cdr.detectChanges();
      }
    });
  }
}
