import { ToastService } from './toast.service';

describe('ToastService', () => {
  let service: ToastService;

  beforeEach(() => {
    vi.useFakeTimers();
    service = new ToastService();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('starts with no toasts', () => {
    expect(service.toasts()).toEqual([]);
  });

  it('success() adds a toast of type success', () => {
    service.success('تم الحفظ');

    const toasts = service.toasts();
    expect(toasts.length).toBe(1);
    expect(toasts[0].type).toBe('success');
    expect(toasts[0].message).toBe('تم الحفظ');
  });

  it('error() adds a toast of type error', () => {
    service.error('حدث خطأ');

    expect(service.toasts()[0].type).toBe('error');
  });

  it('assigns increasing unique ids to successive toasts', () => {
    service.success('الأول');
    service.success('الثاني');

    const [first, second] = service.toasts();
    expect(first.id).not.toBe(second.id);
  });

  it('dismiss() removes only the matching toast', () => {
    service.success('يبقى');
    service.success('يتشال');

    const idToRemove = service.toasts()[1].id;
    service.dismiss(idToRemove);

    const remaining = service.toasts();
    expect(remaining.length).toBe(1);
    expect(remaining[0].message).toBe('يبقى');
  });

  it('auto-dismisses a toast after 4 seconds', () => {
    service.success('رسالة مؤقتة');
    expect(service.toasts().length).toBe(1);

    vi.advanceTimersByTime(4001);

    expect(service.toasts().length).toBe(0);
  });
});
