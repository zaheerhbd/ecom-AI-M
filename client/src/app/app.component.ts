import { Component, OnInit } from '@angular/core';
import { AccountService } from './account/account.service';
import { BasketService } from './basket/basket.service';

@Component({
  selector: 'app-root',
  templateUrl: './app.component.html',
  styleUrls: ['./app.component.scss']
})
// AppComponent is the root component. Its template acts like the outer shell
// of the Angular application.
export class AppComponent implements OnInit {
  title = 'SkiNet';

  // Angular dependency injection creates these services and passes them in.
  // Services usually hold shared business logic or shared state.
  constructor(private basketService: BasketService, private accountService: AccountService) { }

  // ngOnInit is an Angular lifecycle hook. Angular calls it after creating the
  // component, which makes it a common place for startup logic.
  ngOnInit(): void {
    this.loadBasket();
    this.loadCurrentUser();
  }

  loadCurrentUser() {
    // localStorage is browser storage that survives page refreshes.
    const token = localStorage.getItem('token');
    // HttpClient methods return Observables, so subscribe() is what actually
    // starts the HTTP request.
    this.accountService.loadCurrentUser(token).subscribe(() => {
      console.log('loaded user');
    }, error => {
      console.log(error);
    })
  }

  loadBasket() {
    const basketId = localStorage.getItem('basket_id');
    if (basketId) {
      // If the user already had a basket before refreshing the page, fetch it
      // again from the backend so the app state is restored.
      this.basketService.getBasket(basketId).subscribe(() => {
        console.log('initialised basket');
      }, error => {
        console.log(error);
      })
    }
  }
}
