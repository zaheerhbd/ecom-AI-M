import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { IBrand } from '../shared/models/brand';
import { IPagination, Pagination } from '../shared/models/pagination';
import { IType } from '../shared/models/productType';
import { map } from 'rxjs/operators';
import { ShopParams } from '../shared/models/shopParams';
import { IProduct } from '../shared/models/product';
import { of } from 'rxjs';
import { environment } from 'src/environments/environment';

@Injectable({
  providedIn: 'root'
})
// A service is where Angular apps often keep API logic and reusable business
// logic that should not live inside a component.
export class ShopService {
  baseUrl = environment.apiUrl;
  products: IProduct[] = [];
  brands: IBrand[] = [];
  types: IType[] = [];
  // pagination stores the last paged result returned by the products API.
  pagination = new Pagination();
  // shopParams keeps the current filters/sort/page chosen by the user.
  shopParams = new ShopParams();
  // productCache avoids repeated API calls for the same filter combination.
  productCache = new Map();

  constructor(private http: HttpClient) { }

  getProducts(useCache: boolean) {
    // When filters changed, callers can disable the cache to force a fresh API call.
    if (useCache === false) {
      this.productCache = new Map();
    }

    if (this.productCache.size > 0 && useCache === true) {
      // The cache key is built from the current shop parameter values.
      if (this.productCache.has(Object.values(this.shopParams).join('-'))) {
        this.pagination.data = this.productCache.get(Object.values(this.shopParams).join('-'));
        // of(...) creates an Observable from a normal in-memory value so the
        // component can use the same subscribe pattern as with HTTP calls.
        return of(this.pagination);
      }
    }

    // HttpParams builds the query string sent to the backend, for example:
    // /products?brandId=1&typeId=2&sort=name&pageIndex=1&pageSize=6
    let params = new HttpParams();

    if (this.shopParams.brandId !== 0) {
      params = params.append('brandId', this.shopParams.brandId.toString())
    }

    if (this.shopParams.typeId !== 0) {
      params = params.append('typeId', this.shopParams.typeId.toString())
    }

    if (this.shopParams.search) {
      params = params.append('search', this.shopParams.search)
    }

    params = params.append('sort', this.shopParams.sort);
    params = params.append('pageIndex', this.shopParams.pageNumber.toString());
    params = params.append('pageSize', this.shopParams.pageSize.toString());

    return this.http.get<IPagination>(this.baseUrl + 'products', { observe: 'response', params })
      .pipe(
        map(response => {
          // pipe(...) lets RxJS transform Observable data.
          // map(...) changes the HTTP response into the shape the rest of the
          // app wants to work with.
          this.productCache.set(Object.values(this.shopParams).join('-'), response.body.data);
          this.pagination = response.body;
          return this.pagination;
        })
      )
  }

  setShopParams(params: ShopParams) {
    // Components call this when the user changes a filter, page, or sort order.
    this.shopParams = params;
  }

  getShopParams() {
    return this.shopParams;
  }

  getProduct(id: number) {
    let product: IProduct;
    this.productCache.forEach((products: IProduct[]) => {
      // Try to find the product inside already-loaded pages before making
      // another request to the API.
      product = products.find(p => p.id === id);
    })

    if (product) {
      return of(product);
    }

    return this.http.get<IProduct>(this.baseUrl + 'products/' + id);
  }

  getBrands() {
    if (this.brands.length > 0) {
      // If brands were already loaded once, reuse them from memory.
      return of(this.brands);
    }
    return this.http.get<IBrand[]>(this.baseUrl + 'products/brands').pipe(
      map(response => {
        this.brands = response;
        return response;
      })
    )
  }

  getTypes() {
    if (this.types.length > 0) {
      return of(this.types);
    }
    return this.http.get<IType[]>(this.baseUrl + 'products/types').pipe(
      map(response => {
        this.types = response;
        return response;
      })
    )
  }
}
