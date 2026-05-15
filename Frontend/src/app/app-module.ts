import { NgModule, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { BrowserModule, provideClientHydration, withEventReplay } from '@angular/platform-browser';

import { provideHttpClient, withInterceptorsFromDi, HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthInterceptor } from './services/auth.interceptor';

import { AppRoutingModule } from './app-routing-module';
import { AppComponent  } from './app';
import { TurnstileComponent } from './turnstile/turnstile.component';
import { LoginComponent } from './login/login.component';
import { FormsModule } from '@angular/forms';
import { FloorComponent } from './floor/floor.component';
import { TableComponent } from './table/table.component';
import { ReservationFilterComponent } from './reservation-filter/reservation-filter.component';

@NgModule({
  declarations: [
    AppComponent ,
    TurnstileComponent,
    LoginComponent,
    FloorComponent,
    TableComponent,
    ReservationFilterComponent
  ],
  imports: [
    BrowserModule,
    AppRoutingModule,
    FormsModule
  ],
  providers: [
    provideHttpClient(withInterceptorsFromDi()),
    provideBrowserGlobalErrorListeners(),
    provideZonelessChangeDetection(),
    provideClientHydration(withEventReplay()),
    { provide: HTTP_INTERCEPTORS, useClass: AuthInterceptor, multi: true }
  ],
  bootstrap: [AppComponent]
})
export class AppModule { }
