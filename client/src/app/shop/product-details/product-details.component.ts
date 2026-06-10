import { Component, OnInit } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { BasketService } from 'src/app/basket/basket.service';
import { IProduct } from 'src/app/shared/models/product';
import { BreadcrumbService } from 'xng-breadcrumb';

@Component({
  selector: 'app-product-details',
  templateUrl: './product-details.component.html',
  styleUrls: ['./product-details.component.scss']
})
export class ProductDetailsComponent implements OnInit {
  product: IProduct;
  quantity = 1;

  constructor(private activatedRoute: ActivatedRoute,
    private bcService: BreadcrumbService, private basketService: BasketService) {
    this.bcService.set('@productDetails', ' ')
  }

  ngOnInit(): void {
    // Because the route uses a resolver, Angular has already loaded the product
    // before this component is shown.
    this.loadResolvedProduct();
  }

  loadResolvedProduct() {
    // snapshot.data contains values returned by route resolvers.
    // "product" matches the key used in the route:
    // resolve: { product: ProductResolver }
    this.product = this.activatedRoute.snapshot.data['product'];

    // The breadcrumb name can now be set immediately because the resolved
    // product data already exists.
    this.bcService.set('@productDetails', this.product.name);
  }

  addItemToBasket() {
    this.basketService.addItemToBasket(this.product, this.quantity);
  }

  incrementQuantity() {
    this.quantity++;
  }

  decrementQuantity() {
    if (this.quantity > 1) {
      this.quantity--;
    }
  }

}
