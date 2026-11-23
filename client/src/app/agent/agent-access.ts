import { HttpErrorResponse } from '@angular/common/http';

/**
 * The runs and evals endpoints are open in Development but need the Admin role in production.
 * Turns an HTTP error from those endpoints into a message for the page.
 */
export function describeAdminError(error: HttpErrorResponse): string {
  if (error.status === 401) {
    return 'Please log in with an admin account to see this page.';
  }
  if (error.status === 403) {
    return 'This page is for admins only. Your account does not have the Admin role.';
  }
  if (error.status === 0) {
    return 'Could not reach the server. Is the API running?';
  }
  if (error.error && error.error.message) {
    return error.error.message;
  }
  return 'Something went wrong (HTTP ' + error.status + ').';
}
