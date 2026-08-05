import {
  Component,
  HostListener,
  OnDestroy,
  OnInit,
  inject
} from '@angular/core';

import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';

import {
  Subject,
  debounceTime,
  finalize
} from 'rxjs';

import { ProductResponse } from '../../products/product.models';
import { ProductService } from '../../products/product.service';
import { OrderService } from '../order.service';

import {
  CreateOrderRequest,
  OrderResponse,
  PaymentMethod
} from '../order.models';

interface CartLine {
  productId: string;
  nameAr: string;
  unitPrice: number;
  quantity: number;
}

interface PaymentLine {
  method: PaymentMethod;
  amount: number;
  referenceNumber: string;
}

@Component({
  selector: 'app-checkout',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule
  ],
  templateUrl: './checkout.component.html',
  styleUrl: './checkout.component.scss'
})
export class CheckoutComponent
  implements OnInit, OnDestroy {

  private readonly productService =
    inject(ProductService);

  private readonly orderService =
    inject(OrderService);

  private readonly router =
    inject(Router);

  private readonly searchTerm$ = new Subject<void>();

  readonly PaymentMethod = PaymentMethod;

  products: ProductResponse[] = [];
  filteredProducts: ProductResponse[] = [];
  searchTerm = '';

  cart: CartLine[] = [];

  paymentLines: PaymentLine[] = [
    this.createPaymentLine(PaymentMethod.Cash)
  ];

  isLoadingProducts = true;
  isCheckingOut = false;

  errorMessage = '';
  lastReceipt: OrderResponse | null = null;

  ngOnInit(): void {
    this.loadProducts();

    // Debounced so fast typing in the search box doesn't re-filter
    // the whole catalog on every keystroke.
    this.searchTerm$
      .pipe(debounceTime(150))
      .subscribe(() => this.runSearch());
  }

  ngOnDestroy(): void {
    this.searchTerm$.complete();
  }

  @HostListener('document:keydown.enter', ['$event'])
  onEnterKey(event: Event): void {
    const target = event.target as HTMLElement | null;
    const isTypingInField =
      target?.tagName === 'INPUT' || target?.tagName === 'TEXTAREA';

    // Enter completes the sale from anywhere on the page EXCEPT while
    // actively typing in a field (search box, amount inputs, etc.) -
    // those need Enter to behave normally (or do nothing).
    if (isTypingInField || this.lastReceipt || this.cart.length === 0) {
      return;
    }

    this.checkout();
  }

  loadProducts(): void {
    this.isLoadingProducts = true;

    this.productService
      .getAll(false, null, 1, 500)
      .pipe(
        finalize(() => {
          this.isLoadingProducts = false;
        })
      )
      .subscribe({
        next: result => {
          this.products = result.items;
          this.runSearch();
        },

        error: () => {
          this.errorMessage = 'تعذر تحميل المنتجات';
        }
      });
  }

  onSearchChange(): void {
    this.searchTerm$.next();
  }

  private runSearch(): void {
    const term = this.searchTerm.trim().toLowerCase();

    this.filteredProducts = !term
      ? this.products
      : this.products.filter(product =>
          product.nameAr.toLowerCase().includes(term) ||
          (product.nameEn ?? '').toLowerCase().includes(term) ||
          (product.barcode ?? '').includes(term)
        );
  }

  addToCart(product: ProductResponse): void {
    if (product.trackStock && product.stockQuantity <= 0) {
      this.errorMessage = `"${product.nameAr}" غير متاح في المخزون`;
      return;
    }

    const existing = this.cart.find(
      line => line.productId === product.id
    );

    if (existing) {
      if (
        product.trackStock &&
        existing.quantity + 1 > product.stockQuantity
      ) {
        this.errorMessage = `الكمية المتاحة من "${product.nameAr}" غير كافية`;
        return;
      }

      existing.quantity += 1;
    } else {
      this.cart.push({
        productId: product.id,
        nameAr: product.nameAr,
        unitPrice: product.price,
        quantity: 1
      });
    }

    this.syncSinglePaymentLine();
  }

  increment(line: CartLine): void {
    line.quantity += 1;
    this.syncSinglePaymentLine();
  }

  decrement(line: CartLine): void {
    if (line.quantity <= 1) {
      this.removeLine(line);
      return;
    }

    line.quantity -= 1;
    this.syncSinglePaymentLine();
  }

  removeLine(line: CartLine): void {
    this.cart = this.cart.filter(l => l !== line);
    this.syncSinglePaymentLine();
  }

  clearCart(): void {
    this.cart = [];
    this.paymentLines = [this.createPaymentLine(PaymentMethod.Cash)];
  }

  get total(): number {
    return this.cart.reduce(
      (sum, line) => sum + line.unitPrice * line.quantity,
      0
    );
  }

  get totalPaid(): number {
    return this.paymentLines.reduce(
      (sum, line) => sum + (line.amount || 0),
      0
    );
  }

  get remainingBalance(): number {
    return Math.max(0, this.total - this.totalPaid);
  }

  get changeDue(): number {
    return Math.max(0, this.totalPaid - this.total);
  }

  setLineMethod(line: PaymentLine, method: PaymentMethod): void {
    line.method = method;
  }

  addPaymentLine(): void {
    const method = this.paymentLines.some(l => l.method === PaymentMethod.Cash)
      ? PaymentMethod.Card
      : PaymentMethod.Cash;

    const line = this.createPaymentLine(method);
    line.amount = this.remainingBalance;

    this.paymentLines.push(line);
  }

  removePaymentLine(line: PaymentLine): void {
    if (this.paymentLines.length === 1) {
      return;
    }

    this.paymentLines = this.paymentLines.filter(l => l !== line);
  }

  fillRemaining(line: PaymentLine): void {
    const othersPaid = this.paymentLines
      .filter(l => l !== line)
      .reduce((sum, l) => sum + (l.amount || 0), 0);

    line.amount = Math.max(0, this.total - othersPaid);
  }

  private syncSinglePaymentLine(): void {
    if (this.paymentLines.length === 1) {
      this.paymentLines[0].amount = this.total;
    }
  }

  private createPaymentLine(method: PaymentMethod): PaymentLine {
    return {
      method,
      amount: this.total,
      referenceNumber: ''
    };
  }

  checkout(): void {
    this.errorMessage = '';

    if (this.cart.length === 0) {
      this.errorMessage = 'السلة فارغة';
      return;
    }

    const activePayments = this.paymentLines.filter(
      line => line.amount > 0
    );

    if (activePayments.length === 0) {
      this.errorMessage = 'أدخل مبلغ الدفع';
      return;
    }

    if (this.totalPaid < this.total) {
      this.errorMessage = 'المبلغ المدفوع أقل من الإجمالي';
      return;
    }

    const request: CreateOrderRequest = {
      lines: this.cart.map(line => ({
        productId: line.productId,
        quantity: line.quantity,
        discountAmount: 0
      })),

      payments: activePayments.map(line => ({
        method: line.method,
        amount: line.amount,
        referenceNumber: line.referenceNumber.trim() || null
      }))
    };

    this.isCheckingOut = true;

    this.orderService
      .checkout(request)
      .pipe(
        finalize(() => {
          this.isCheckingOut = false;
        })
      )
      .subscribe({
        next: order => {
          this.lastReceipt = order;
          this.cart = [];
          this.paymentLines = [this.createPaymentLine(PaymentMethod.Cash)];
        },

        error: error => {
          this.errorMessage =
            error?.error?.message ??
            'تعذر إتمام عملية البيع';

          if (error?.status === 409) {
            this.router.navigateByUrl('/open-shift');
          }
        }
      });
  }

  startNewSale(): void {
    this.lastReceipt = null;
  }

  goToOpenShift(): void {
    this.router.navigateByUrl('/open-shift');
  }

  goToHistory(): void {
    this.router.navigateByUrl('/history');
  }
}
