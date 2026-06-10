import { Injectable } from '@angular/core';
import { ActivatedRouteSnapshot, Resolve, RouterStateSnapshot } from '@angular/router';
import { Observable } from 'rxjs';
import { IProduct } from '../shared/models/product';
import { ShopService } from './shop.service';

@Injectable({
  providedIn: 'root'
})
// A dresolver loads data before Angular finishes opening a route.
// Think of it as "fetch this data first, then show the page".
//
// Why use a resolver?
// - The component can start with data already available
// - The route waits for the API call before rendering the page
// - It keeps data-loading logic close to the route definition
export class ProductResolver implements Resolve<IProduct> {
  constructor(private shopService: ShopService) {}

  // Angular calls resolve(...) automatically when a route contains:
  // resolve: { product: ProductResolver }
  //
  // route gives access to route parameters, for example ":id"
  // state contains information about the target URL
  resolve(route: ActivatedRouteSnapshot, state: RouterStateSnapshot): Observable<IProduct> {
    // Read the "id" value from the URL.
    // Example: /shop/12  ->  id = 12
    const productId = +route.paramMap.get('id');

    // Return the Observable for the product request.
    // Angular will wait for this Observable to finish before it activates
    // ProductDetailsComponent.
    return this.shopService.getProduct(productId);
  }
}
