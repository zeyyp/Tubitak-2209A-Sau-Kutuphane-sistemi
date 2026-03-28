import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap, BehaviorSubject } from 'rxjs';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private readonly USER_KEY = 'current_user';
  private readonly ACCESS_TOKEN_KEY = 'access_token';
  private readonly REFRESH_TOKEN_KEY = 'refresh_token';
  private readonly TOKEN_EXPIRY_KEY = 'token_expiry';
  private apiUrl = 'http://localhost:5010/api/Auth';

  private isRefreshing = false;

  constructor(private http: HttpClient) { }

  login(studentNumber: string, password: string): Observable<any> {
    return this.http.post(`${this.apiUrl}/login`, { studentNumber, password }).pipe(
      tap((response: any) => {
        if (response && response.studentNumber) {
          localStorage.setItem(this.USER_KEY, response.studentNumber);

          // JWT Token'ları kaydet
          if (response.accessToken) {
            localStorage.setItem(this.ACCESS_TOKEN_KEY, response.accessToken);
          }
          if (response.refreshToken) {
            localStorage.setItem(this.REFRESH_TOKEN_KEY, response.refreshToken);
          }
          if (response.accessTokenExpiresAt) {
            localStorage.setItem(this.TOKEN_EXPIRY_KEY, response.accessTokenExpiresAt);
          }

          if (response.role) {
            localStorage.setItem('user_role', response.role);
          }
          if (response.academicLevel) {
            localStorage.setItem('academic_level', response.academicLevel);
          }
        }
      })
    );
  }

  register(user: any): Observable<any> {
    return this.http.post(`${this.apiUrl}/register`, user);
  }

  logout(): void {
    // Backend'e revoke isteği gönder
    const refreshToken = this.getRefreshToken();
    if (refreshToken) {
      this.http.post(`${this.apiUrl}/revoke`, { refreshToken }).subscribe({
        error: () => {} // Hata olsa bile devam et
      });
    }

    // Tüm token ve kullanıcı bilgilerini temizle
    localStorage.removeItem(this.USER_KEY);
    localStorage.removeItem(this.ACCESS_TOKEN_KEY);
    localStorage.removeItem(this.REFRESH_TOKEN_KEY);
    localStorage.removeItem(this.TOKEN_EXPIRY_KEY);
    localStorage.removeItem('user_role');
    localStorage.removeItem('academic_level');
  }

  getCurrentUser(): string | null {
    if (typeof localStorage !== 'undefined') {
      return localStorage.getItem(this.USER_KEY);
    }
    return null;
  }

  getUserRole(): string | null {
    if (typeof localStorage !== 'undefined') {
      return localStorage.getItem('user_role');
    }
    return null;
  }

  getAcademicLevel(): string | null {
    if (typeof localStorage !== 'undefined') {
      return localStorage.getItem('academic_level');
    }
    return null;
  }

  isAdmin(): boolean {
    return this.getUserRole() === 'admin';
  }

  isLoggedIn(): boolean {
    return !!this.getCurrentUser() && !!this.getToken();
  }

  // JWT Access Token'ı döndür
  getToken(): string | null {
    if (typeof localStorage !== 'undefined') {
      return localStorage.getItem(this.ACCESS_TOKEN_KEY);
    }
    return null;
  }

  // Refresh Token'ı döndür
  getRefreshToken(): string | null {
    if (typeof localStorage !== 'undefined') {
      return localStorage.getItem(this.REFRESH_TOKEN_KEY);
    }
    return null;
  }

  // Token süresinin dolup dolmadığını kontrol et
  isTokenExpired(): boolean {
    const expiry = localStorage.getItem(this.TOKEN_EXPIRY_KEY);
    if (!expiry) return true;

    const expiryDate = new Date(expiry);
    // 1 dakika öncesinde yenile (güvenlik marjı)
    return new Date() >= new Date(expiryDate.getTime() - 60000);
  }

  // Token yenileme
  refreshToken(): Observable<any> {
    const refreshToken = this.getRefreshToken();
    if (!refreshToken) {
      throw new Error('No refresh token available');
    }

    return this.http.post(`${this.apiUrl}/refresh`, { refreshToken }).pipe(
      tap((response: any) => {
        if (response.accessToken) {
          localStorage.setItem(this.ACCESS_TOKEN_KEY, response.accessToken);
        }
        if (response.refreshToken) {
          localStorage.setItem(this.REFRESH_TOKEN_KEY, response.refreshToken);
        }
        if (response.accessTokenExpiresAt) {
          localStorage.setItem(this.TOKEN_EXPIRY_KEY, response.accessTokenExpiresAt);
        }
      })
    );
  }
}
