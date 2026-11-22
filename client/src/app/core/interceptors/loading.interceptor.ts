import { Injectable } from '@angular/core';
import {
  HttpRequest,
  HttpHandler,
  HttpEvent,
  HttpInterceptor
} from '@angular/common/http';
import { Observable } from 'rxjs';
import { BusyService } from '../services/busy.service';
import { delay, finalize } from 'rxjs/operators';

@Injectable()
export class LoadingInterceptor implements HttpInterceptor {

  constructor(private busyService: BusyService) { }

  intercept(request: HttpRequest<unknown>, next: HttpHandler): Observable<HttpEvent<unknown>> {
    if (request.method === 'POST' && request.url.includes('orders')) {
      return next.handle(request);
    }
    if (request.method === 'DELETE') {
      return next.handle(request);
    }
    if (request.url.includes('emailexists')) {
      return next.handle(request);
    }
    // The assistant pages show their own "thinking…" and progress indicators. A full-screen
    // spinner would hide the chat for the whole answer, and flash every few seconds while
    // an eval run is being polled.
    if (request.url.includes('/agent/')) {
      return next.handle(request);
    }
    this.busyService.busy();
    return next.handle(request).pipe(
      finalize(() => {
        this.busyService.idle();
      })
    );
  }
}
