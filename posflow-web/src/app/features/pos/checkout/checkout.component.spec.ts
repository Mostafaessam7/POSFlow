import { TestBed, fakeAsync, tick } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';

import { CheckoutComponent } from './checkout.component';
import { PaymentMethod } from '../order.models';
import { ProductResponse } from '../../products/product.models';

function buildProduct(
  overrides: Partial<ProductResponse> = {}
): ProductResponse {
  return {
    id: 'prod-1',
    nameAr: 'منتج تجريبي',
    nameEn: null,
    barcode: null,
    price: 25,
    isActive: true,
    categoryId: null,
    categoryName: null,
    trackStock: false,
    stockQuantity: 0,
    rowVersion: 'AAAAAAAAAAA=',
    ...overrides
  };
}

describe('CheckoutComponent - cart and payment math', () => {
  let component: CheckoutComponent;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [CheckoutComponent],
      providers: [
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    }).compileComponents();

    const fixture = TestBed.createComponent(CheckoutComponent);
    component = fixture.componentInstance;
  });

  it('starts with an empty cart and a single zeroed cash payment line', () => {
    expect(component.total).toBe(0);
    expect(component.paymentLines.length).toBe(1);
    expect(component.paymentLines[0].method).toBe(PaymentMethod.Cash);
  });

  it('adding a product updates the total and keeps the single payment line synced', () => {
    component.addToCart(buildProduct({ price: 25 }));

    expect(component.total).toBe(25);
    expect(component.paymentLines[0].amount).toBe(25);
  });

  it('adding the same product twice increments its quantity instead of duplicating the line', () => {
    const product = buildProduct({ price: 10 });

    component.addToCart(product);
    component.addToCart(product);

    expect(component.cart.length).toBe(1);
    expect(component.cart[0].quantity).toBe(2);
    expect(component.total).toBe(20);
  });

  it('refuses to add an out-of-stock tracked product', () => {
    const product = buildProduct({ trackStock: true, stockQuantity: 0 });

    component.addToCart(product);

    expect(component.cart.length).toBe(0);
    expect(component.errorMessage).toContain('غير متاح');
  });

  it('refuses to exceed available stock when adding more of the same product', () => {
    const product = buildProduct({ trackStock: true, stockQuantity: 1 });

    component.addToCart(product);
    component.addToCart(product);

    expect(component.cart[0].quantity).toBe(1);
    expect(component.errorMessage).toContain('غير كافية');
  });

  it('computes remainingBalance and changeDue correctly across a split payment', () => {
    component.addToCart(buildProduct({ price: 100 }));

    component.paymentLines[0].amount = 60;
    component.addPaymentLine();
    component.paymentLines[1].amount = 30;

    expect(component.totalPaid).toBe(90);
    expect(component.remainingBalance).toBe(10);
    expect(component.changeDue).toBe(0);

    component.paymentLines[1].amount = 50;

    expect(component.totalPaid).toBe(110);
    expect(component.remainingBalance).toBe(0);
    expect(component.changeDue).toBe(10);
  });

  it('fillRemaining tops a line up to cover exactly what is left', () => {
    component.addToCart(buildProduct({ price: 100 }));
    component.addPaymentLine();

    component.paymentLines[0].amount = 40;
    component.fillRemaining(component.paymentLines[1]);

    expect(component.paymentLines[1].amount).toBe(60);
    expect(component.remainingBalance).toBe(0);
  });

  it('checkout() blocks with an Arabic error when the cart is empty', () => {
    component.checkout();

    expect(component.errorMessage).toBe('السلة فارغة');
  });

  it('checkout() blocks when the amount paid is less than the total', () => {
    component.addToCart(buildProduct({ price: 50 }));
    component.paymentLines[0].amount = 10;

    component.checkout();

    expect(component.errorMessage).toBe('المبلغ المدفوع أقل من الإجمالي');
  });

  it('removing the last cart line resets the single payment line back to zero', () => {
    const product = buildProduct({ price: 40 });
    component.addToCart(product);

    component.removeLine(component.cart[0]);

    expect(component.total).toBe(0);
    expect(component.paymentLines[0].amount).toBe(0);
  });

  it('debounces search filtering instead of filtering on every keystroke', fakeAsync(() => {
    // Sets up the searchTerm$ debounce subscription (loadProducts()'s
    // HTTP call is left unresolved on purpose - not what's under test
    // here, and nothing asserts on it).
    component.ngOnInit();

    component.products = [
      buildProduct({ id: 'a', nameAr: 'شاي أحمر' }),
      buildProduct({ id: 'b', nameAr: 'قهوة تركي' })
    ];

    component.searchTerm = 'شاي';
    component.onSearchChange();

    // The 150ms debounce window hasn't elapsed yet.
    expect(component.filteredProducts.length).toBe(0);

    tick(150);

    expect(component.filteredProducts.length).toBe(1);
    expect(component.filteredProducts[0].id).toBe('a');

    component.ngOnDestroy();
  }));
});
