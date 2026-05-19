import { Component, signal } from '@angular/core';
import { AuthService } from './services/auth.service';
import { Router } from '@angular/router';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  standalone: false,
  styleUrl: './app.component.css'
})
export class AppComponent  {
  protected readonly title = signal('AngularFrontEnd');

  constructor(public authService: AuthService, private router: Router) {}

  isAdminRoute(): boolean {
    return this.router.url.startsWith('/admin');
  }

  isLoginPage(): boolean {
    return this.router.url.startsWith('/login');
  }

  logout() {
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}
