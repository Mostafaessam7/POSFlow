import { ConfirmDialogService } from './confirm-dialog.service';

describe('ConfirmDialogService', () => {
  let service: ConfirmDialogService;

  beforeEach(() => {
    service = new ConfirmDialogService();
  });

  it('starts with no pending request', () => {
    expect(service.pending()).toBeNull();
  });

  it('confirm() populates pending() with the given message and defaults', () => {
    void service.confirm('هل أنت متأكد؟');

    const pending = service.pending();
    expect(pending).not.toBeNull();
    expect(pending!.request.message).toBe('هل أنت متأكد؟');
    expect(pending!.request.withInput).toBe(false);
    expect(pending!.request.danger).toBe(false);
  });

  it('confirm() resolves true when confirmed', async () => {
    const resultPromise = service.confirm('متأكد؟');

    service.respondConfirm();

    expect(await resultPromise).toBe(true);
    expect(service.pending()).toBeNull();
  });

  it('confirm() resolves false when cancelled', async () => {
    const resultPromise = service.confirm('متأكد؟');

    service.respondCancel();

    expect(await resultPromise).toBe(false);
  });

  it('confirm() respects custom options', () => {
    void service.confirm('احذف؟', {
      title: 'حذف',
      confirmLabel: 'احذف',
      cancelLabel: 'تراجع',
      danger: true
    });

    const request = service.pending()!.request;
    expect(request.title).toBe('حذف');
    expect(request.confirmLabel).toBe('احذف');
    expect(request.cancelLabel).toBe('تراجع');
    expect(request.danger).toBe(true);
  });

  it('prompt() populates pending() with withInput true', () => {
    void service.prompt('السبب؟');

    expect(service.pending()!.request.withInput).toBe(true);
  });

  it('prompt() resolves the trimmed input value on confirm', async () => {
    const resultPromise = service.prompt('السبب؟');

    service.inputValue = '  منتج تالف  ';
    service.respondConfirm();

    expect(await resultPromise).toBe('منتج تالف');
  });

  it('prompt() resolves null when confirmed with an empty/whitespace-only value', async () => {
    const resultPromise = service.prompt('السبب؟');

    service.inputValue = '   ';
    service.respondConfirm();

    expect(await resultPromise).toBeNull();
  });

  it('prompt() resolves null when cancelled', async () => {
    const resultPromise = service.prompt('السبب؟');

    service.inputValue = 'كان هيتكتب بس اتلغى';
    service.respondCancel();

    expect(await resultPromise).toBeNull();
  });

  it('respondConfirm()/respondCancel() are no-ops when nothing is pending', () => {
    expect(() => service.respondConfirm()).not.toThrow();
    expect(() => service.respondCancel()).not.toThrow();
  });

  it('starting a new confirm() while one is pending resets inputValue', () => {
    void service.prompt('الأول؟');
    service.inputValue = 'قيمة قديمة';

    void service.prompt('الثاني؟');

    expect(service.inputValue).toBe('');
  });
});
