import { Injectable } from '@angular/core';
import { CanActivate, ActivatedRouteSnapshot, RouterStateSnapshot, UrlTree, Router } from '@angular/router';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { AccountService } from 'src/app/account/account.service';

@Injectable({
  providedIn: 'root'
})
// A guard is a gatekeeper for routes.
// Angular asks this class "is the user allowed to open this route?"
// before navigation finishes.
export class AuthGuard implements CanActivate {
  // The guard needs:
  // 1. AccountService to know whether a user is logged in
  // 2. Router so it can redirect unauthorized users to the login page
  constructor(private accountService: AccountService, private router: Router) {}

  // canActivate is the method Angular calls automatically when a route uses:
  // canActivate: [AuthGuard]
  //
  // If this method returns:
  // - true: Angular allows navigation
  // - false: Angular blocks navigation
  // - an Observable<boolean>: Angular waits for the Observable result
  //
  // route: information about the route being opened
  // state: information about the full target URL, such as "/checkout"
  canActivate(
    route: ActivatedRouteSnapshot,
    state: RouterStateSnapshot): Observable<boolean> {
    // currentUser$ is an Observable from AccountService.
    // The guard subscribes to that stream using pipe(...).
    return this.accountService.currentUser$.pipe(
      // map(...) transforms the emitted user object into true/false permission logic.
      map(auth => {
        // If auth contains a user object, the user is logged in.
        if (auth) {
          // Returning true tells Angular to continue navigation.
          return true;
        }

        // If there is no logged-in user, redirect to the login page.
        // queryParams adds data to the URL like:
        // /account/login?returnUrl=/checkout
        //
        // returnUrl is useful because after login the app can send the user
        // back to the page they originally wanted.
        this.router.navigate(['account/login'], {queryParams: {returnUrl: state.url}})

        // Returning false tells Angular not to activate the protected route.
        return false;
      })
    )
  }
}
