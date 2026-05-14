import { NgModule, provideBrowserGlobalErrorListeners, provideZonelessChangeDetection } from '@angular/core';
import { BrowserModule, provideClientHydration, withEventReplay } from '@angular/platform-browser';

import { provideHttpClient, withInterceptorsFromDi, HTTP_INTERCEPTORS } from '@angular/common/http';
import { AuthInterceptor } from './services/auth.interceptor';

import { AppRoutingModule } from './app-routing-module';
import { AppComponent  } from './app';
import { ReservationFilterComponent  } from './reservation-filter/reservation-filter.component';
import { HomeComponent } from './home/home.component';
import { TurnstileComponent } from './turnstile/turnstile.component';
import { LoginComponent } from './login/login.component';
import { FormsModule } from '@angular/forms';
import { FloorComponent } from './floor/floor.component';
import { TableComponent } from './table/table.component';

@NgModule({
  declarations: [
    AppComponent ,
    ReservationFilterComponent,
    HomeComponent,
    TurnstileComponent,
    LoginComponent,
    FloorComponent,
    TableComponent
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
