import { NgModule } from '@angular/core';
import { BrowserModule } from '@angular/platform-browser';
import { BrowserAnimationsModule } from '@angular/platform-browser/animations';
import { HttpClientModule, HTTP_INTERCEPTORS } from '@angular/common/http';

import { AppRoutingModule } from './app-routing.module';
import { AppComponent } from './app.component';
import { CoreModule } from './core/core.module';
import { ShopModule } from './shop/shop.module';
import { HomeModule } from './home/home.module';
import { ErrorInterceptor } from './core/interceptors/error.interceptor';
import { NgxSpinnerModule } from 'ngx-spinner';
import { LoadingInterceptor } from './core/interceptors/loading.interceptor';
import { JwtInterceptor } from './core/interceptors/jwt.interceptor';

// AppModule is the root Angular module. Think of it as the main assembly point
// where Angular is told which building blocks belong to the application.
@NgModule({
  declarations: [
    // Components declared here belong directly to this module.
    AppComponent
  ],
  imports: [
    // BrowserModule is required for any Angular app running in the browser.
    BrowserModule,
    // AppRoutingModule wires up the top-level route table.
    AppRoutingModule,
    // Needed by libraries such as toastr or spinner that use animations.
    BrowserAnimationsModule,
    // Makes HttpClient available for API calls.
    HttpClientModule,
    // CoreModule contains layout-level pieces like navbar and shared app shell UI.
    CoreModule,
    // HomeModule is loaded eagerly because the home page is the first screen.
    HomeModule,
    // Third-party loading spinner module.
    NgxSpinnerModule
  ],
  providers: [
    // HTTP interceptors sit in the request/response pipeline.
    // multi: true tells Angular to keep all interceptors instead of replacing
    // the previous one.

    // Handles API errors in one central place.
    { provide: HTTP_INTERCEPTORS, useClass: ErrorInterceptor, multi: true },
    // Shows and hides the loading spinner around HTTP calls.
    { provide: HTTP_INTERCEPTORS, useClass: LoadingInterceptor, multi: true },
    // Automatically attaches the JWT token to outgoing API requests.
    { provide: HTTP_INTERCEPTORS, useClass: JwtInterceptor, multi: true },
  ],
  // Angular starts the application by rendering AppComponent first.
  bootstrap: [AppComponent]
})
export class AppModule { }
