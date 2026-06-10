import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import { AuthGuard } from './core/guards/auth.guard';
import { NotFoundComponent } from './core/not-found/not-found.component';
import { ServerErrorComponent } from './core/server-error/server-error.component';
import { TestErrorComponent } from './core/test-error/test-error.component';
import { AiPlaygroundComponent } from './core/ai-playground/ai-playground.component';
import { HomeComponent } from './home/home.component';

// Angular routing decides which component or feature module should be shown
// when the browser URL changes.
const routes: Routes = [
  // When the URL is exactly "/", show the home page component.
  { path: '', component: HomeComponent, data: { breadcrumb: 'Home' } },
  { path: 'ai-lab', component: AiPlaygroundComponent, data: { breadcrumb: 'AI-Lab' } },
  { path: 'test-error', component: TestErrorComponent, data: { breadcrumb: 'Test Errors' } },
  { path: 'server-error', component: ServerErrorComponent, data: { breadcrumb: 'Server Error' } },
  { path: 'not-found', component: NotFoundComponent, data: { breadcrumb: 'Not found' } },
  {
    // loadChildren means "lazy load this feature module only when the user
    // visits this route". This keeps the first page load smaller and faster.
    // Angular will download shop.module and all of its related code on demand.
    path: 'shop', loadChildren: () => import('./shop/shop.module').then(mod => mod.ShopModule),
    data: { breadcrumb: 'Shop' }
  },
  {
    // This works the same way for the basket feature.
    path: 'basket', loadChildren: () => import('./basket/basket.module').then(mod => mod.BasketModule),
    data: { breadcrumb: 'Basket' }
  },
  {
    path: 'checkout', 
    // canActivate runs a guard before navigation. Here AuthGuard decides
    // whether the user is allowed to open checkout.
    canActivate: [AuthGuard],
    loadChildren: () => import('./checkout/checkout.module').then(mod => mod.CheckoutModule),
    data: { breadcrumb: 'Checkout' }
  },
  {
    path: 'orders', 
    // Orders are also protected, so anonymous users cannot open them.
    canActivate: [AuthGuard],
    loadChildren: () => import('./orders/orders.module').then(mod => mod.OrdersModule),
    data: { breadcrumb: 'Orders' }
  },
  {
    // The breadcrumb library is told to skip this route because account pages
    // like login/register usually do not need a breadcrumb label in the UI.
    path: 'account', loadChildren: () => import('./account/account.module').then(mod => mod.AccountModule),
    data: { breadcrumb: {skip: true} }
  },
  // "**" is the wildcard route. If no earlier route matches, redirect to
  // the not-found page.
  { path: '**', redirectTo: 'not-found', pathMatch: 'full' }
];

@NgModule({
  // forRoot registers the application's main router configuration once.
  imports: [RouterModule.forRoot(routes)],
  exports: [RouterModule]
})
export class AppRoutingModule { }
