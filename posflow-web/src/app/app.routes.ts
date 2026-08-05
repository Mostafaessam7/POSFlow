import { Routes } from '@angular/router';

import { authGuard } from './core/auth/auth.guard';
import { shiftGuard } from './core/auth/shift.guard';
import { adminGuard } from './core/auth/admin.guard';

// Every route below is lazy - each page's component (and everything
// it alone imports) only downloads when the person actually
// navigates there, instead of all of them loading up front on first
// paint.
export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'login'
  },
  {
    path: 'login',
    loadComponent: () =>
      import('./features/login/login.component')
        .then(m => m.LoginComponent),
    title: 'تسجيل الدخول'
  },
  {
    path: 'forgot-password',
    loadComponent: () =>
      import('./features/login/forgot-password/forgot-password.component')
        .then(m => m.ForgotPasswordComponent),
    title: 'نسيت كلمة المرور'
  },
  {
    path: 'reset-password',
    loadComponent: () =>
      import('./features/login/reset-password/reset-password.component')
        .then(m => m.ResetPasswordComponent),
    title: 'إعادة تعيين كلمة المرور'
  },
  {
    path: 'open-shift',
    loadComponent: () =>
      import('./features/shifts/open-shift/open-shift.component')
        .then(m => m.OpenShiftComponent),
    canActivate: [authGuard],
    title: 'فتح الوردية'
  },
  {
    path: 'products',
    loadComponent: () =>
      import('./features/products/product-list/product-list.component')
        .then(m => m.ProductListComponent),
    canActivate: [authGuard],
    title: 'المنتجات'
  },
  {
    path: 'pos',
    loadComponent: () =>
      import('./features/pos/checkout/checkout.component')
        .then(m => m.CheckoutComponent),
    canActivate: [
      authGuard,
      shiftGuard
    ],
    title: 'نقطة البيع'
  },
  {
    path: 'history',
    loadComponent: () =>
      import('./features/shifts/history/history.component')
        .then(m => m.HistoryComponent),
    canActivate: [authGuard],
    title: 'سجل الورديات'
  },
  {
    path: 'admin/users',
    loadComponent: () =>
      import('./features/admin/users/users.component')
        .then(m => m.UsersComponent),
    canActivate: [authGuard, adminGuard],
    title: 'المستخدمون'
  },
  {
    path: 'admin/branches',
    loadComponent: () =>
      import('./features/admin/branches/branches.component')
        .then(m => m.BranchesComponent),
    canActivate: [authGuard, adminGuard],
    title: 'الفروع'
  },
  {
    path: 'dashboard',
    loadComponent: () =>
      import('./features/dashboard/dashboard.component')
        .then(m => m.DashboardComponent),
    canActivate: [authGuard],
    title: 'لوحة المبيعات'
  },
  {
    path: '**',
    redirectTo: 'login'
  }
];
