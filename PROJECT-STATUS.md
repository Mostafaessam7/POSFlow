# حالة المشروع — PosFlow

> آخر تحديث: 2026-08-29. الملف ده بيوصف **المشروع ده لوحده**. كل مشروع في الـ workspace ليه
> ملف زيه وحالته مستقلة تمامًا — متقيسش حاجة هنا على مشروع تاني.
>
> للسجل التاريخي (إيه اتعمل وإمتى) شوف [`HANDOVER.md`](HANDOVER.md).
> [`ENTERPRISE-READINESS.md`](ENTERPRISE-READINESS.md) **لقطة تاريخية من 5 أغسطس** وأغلب جداولها
> بقت غلط — متعتمدش عليها للحالة الحالية.

---

## 1. اللي اتعمل واتقفل

### المنتج
نقطة بيع متعددة الـ Tenants والفروع، شغالة end-to-end: تسجيل دخول (+2FA اختياري TOTP)،
الورديات، المنتجات والتصنيفات، طلبات البيع بدفع مقسّم، الفويد والاسترداد، فاتورة PDF
(QuestPDF)، بحث بالباركود من السيرفر، سجل حركة مخزون، لوحة مبيعات، إدارة مستخدمين وفروع
وعملاء، صلاحيات مرنة، Audit Log تلقائي على العمليات الحساسة، ضريبة قابلة للتعديل وعملة عرض،
وواجهة عربي/إنجليزي RTL + وضع ليلي.

### أمان
- **عزل الـ Tenant بطبقتين مستقلتين**: فلتر يدوي + `HasQueryFilter` في `PosFlowDbContext`.
  الطبقة التانية بتحمي حتى لو أي service نسي الفلتر اليدوي، ومغطاة بـ `TenantIsolationTests`.
  التفصيل في [`docs/adr/0001`](docs/adr/0001-dual-layer-tenant-isolation.md).
- **Refresh Token في كوكي HttpOnly** مع حماية CSRF. النقل ده **إضافي**: العملاء اللي مش
  متصفحات (سكريبتات، الاختبارات) لسه شغالين بنفس الطريقة القديمة، ومغطى بـ
  `CookieAuthTransportTests`.
- **`AllowCredentials()` في الـ CORS**. من غيره المتصفح بيرفض كل رد فيه كوكي، والدخول بيبقى
  مكسور فعليًا. **مفيش اختبار سيرفر يقدر يمسك ده** — القاعدة بينفّذها المتصفح، فكل الـ 103 Test
  كانوا بينجحوا والتطبيق مكسور في متصفح حقيقي.
- **`SecretsValidator`** بيرفض الإقلاع بره Development بالمفتاح المتسجّل في الريبو.
- **قفل الحساب** بعد 5 محاولات فاشلة (15 دقيقة)، مستقل عن الـ IP — دفاع ضد محاولات موزعة على
  حساب واحد، بالإضافة لـ rate limiting بالـ IP.
- **`refresh` و `logout` مستثنيين من حد الـ brute-force** — كانوا بيتقفلوا مع الاستخدام العادي.
- Security headers كاملة (CSP + HSTS + الأساسيات).

### تشغيل
- **Migrations صريحة، مش على الإقلاع** — [`docs/adr/0002`](docs/adr/0002-explicit-migrations-not-auto-on-boot.md).
- **API Versioning**: `api/v1/...` جنب المسار القديم، والإصدار بيتقرا من المسار.
- **ProblemDetails** للأخطاء اللي بيولّدها الـ framework.
- Prometheus `/metrics` + Serilog structured logging.
- CI: build + test + بوابة ثغرات بتوقف الـ build على High/Critical + بناء Docker images ونشرها
  على GHCR.

### واجهة وإتاحة
- **ثيم Enterprise Blue** من `MeCodex/design-system`.
- **الـ confirm dialog بقى بيعمل اللي الـ markup بتاعه كان بيدّعيه**: كان عليه `role="alertdialog"`
  و`aria-modal` و`(keydown.escape)` — بس من غير حبس تركيز، والـ Escape كان على `<div>` مش قابل
  للتركيز فماكانش بيوصله أصلًا. يعني كان **شكله سليم لأي مراجع ولأي فحص axe** وهو مكسور
  بالكيبورد. اتحل بـ `@angular/cdk` ومغطى بـ `e2e/dialog-a11y.spec.ts`.

### تنظيف 2026-08-29
- اتشال `run-api.log` — ملف log تايه في جذر المشروع (كان gitignored، فمكانش بيلوّث الريبو).
- اتقفل تحذير **AV0015** بضبط `ApiVersionReader` على `UrlSegmentApiVersionReader` (المسارات
  URL-segment والحزمة كانت مستخدمة قارئ QueryString الافتراضي — شغالة بالصدفة).
- اتقفل تحذير **xUnit2029** في `CookieAuthTransportTests`: `Assert.Empty(...Where(...))` بقت
  `Assert.DoesNotContain(...)`. `dotnet build` بقى **0 Warnings**.
- الأرقام في الـ README كانت قديمة (73 backend / 36 frontend) — الواقع **103 / 40**.

---

## 2. القرارات المعتمدة

| القرار | التفاصيل |
|---|---|
| **PosFlow و POS منتجين منفصلين** | **متتدمجش**. PosFlow هو منتج نقطة البيع الناضج؛ POS بيفضل مستقل بخارطة طريق خاصة بيه |
| **Azure** هدف النشر الأساسي | لسه متوصّلش |
| **Azure Key Vault** لأسرار الإنتاج | لسه متوصّلش |
| **Redis** مطلوب هنا | من التلاتة اللي اتفق عليهم (PosFlow / Gym Manager / RealEstateCRM). لسه متركّبش |
| **App Insights + Sentry** | لسه متركّبش. الموجود دلوقتي Prometheus `/metrics` + Serilog |
| **ثيم Enterprise Blue** | هوية بصرية خاصة بالمنتج فوق أرضية Design System مشتركة |
| **الزوايا الحادة وخط Tajawal بيفضلوا** | الواجهة عربية، والقرار ده مقصود مش سهو |

---

## 3. اللي لسه مفتوح

- **مفيش CD لسيرفر حقيقي** — الـ CI بيبني الـ images وبينشرها على GHCR وبس. اختيار
  الاستضافة قرار صاحب المشروع.
- **Azure Key Vault** — الأسرار دلوقتي من متغيرات بيئة و`SecretsValidator` بيرفض الـ placeholders،
  بس مفيش Key Vault متوصّل.
- **Redis** — متفق عليه للمنتج ده، لسه متعملش.
- **Application Insights + Sentry** — مفيش أي منهم.
- **SMTP حقيقي** — الإعداد موجود بس محتاج حساب فعلي.
- **جدولة الـ backup** — السكريبت موجود (`deploy/backup-database.ps1`) بس مش متجدول.
- **Grafana / alerting** — `/metrics` بيطلع أرقام، بس مفيش داشبورد ولا تنبيهات.

---

## 4. Known issues / Technical debt

- **تغطية E2E ضعيفة**: 4 اختبارات بس (`login`, `pos-checkout`, `dialog-a11y`). أخطر مسار في
  النظام (الدفع المقسّم، الفويد، الاسترداد) مغطى بـ integration tests بس، مش من متصفح حقيقي.
- **مفيش فحص إتاحة تلقائي (axe)** زي اللي في Subscription Tracker. الـ dialog اتصلح، لكن مفيش
  بوابة بتمنع رجوع المشكلة في صفحات تانية.
- **`npx vitest run` بيدّي فشل وهمي** — بيتخطّى إعدادات Angular وبيلقط ملفات Playwright من
  `e2e/`. الأمر الصح `ng test`. اتوثّق في الـ README عشان محدش يضيّع وقت في تشخيص غلط.
- **تحويل العملة يدوي** — الأسعار بيحطها الأدمن، مفيش ربط بأي API أسعار صرف. مقصود دلوقتي،
  بس مش مناسب لو المنتج اشتغل بعملات كتير.

---

## 5. حاجات اتأجّلت عن قصد

| الحاجة | ليه |
|---|---|
| **k6 مش في الـ CI** | عتبة p95 بتتقلب على runners مشتركة، فبتفشل بشكل عشوائي وبتخلي الناس تتجاهل الـ CI. السكريبت موجود في `tests/load/` للتشغيل اليدوي |
| **مفيش إعادة كتابة لكل الـ controllers بـ ProblemDetails** | 31 معالج أخطاء في الفرونت اند بيتجاهلوا جسم الرد أصلًا، فالمكسب صفر مقابل تغيير واسع في كود شغال |
| **دمج PosFlow مع POS** | قرار صريح: منتجين منفصلين بجمهور وخارطة طريق مختلفين |
| **مكتبة `@angular/material`** | اتاخد الـ CDK بس (a11y primitives) لأنه بيصلّح عيب متقاس من غير أي تغيير بصري. استبدال مكوّنات شغالة ومتربطة بالـ tokens مكسبه مش واضح |

---

## تحديث 2026-08-30 — Redis و Key Vault و App Insights و Sentry

| البند | الحالة |
|---|---|
| **Redis** | ✅ اتعمل. `CategoryService` بقى على `IDistributedCache`: Redis لو `ConnectionStrings:Redis` متظبط، وin-memory لو مش متظبط |
| **Azure Key Vault** | ✅ متوصّل. `KeyVault__Uri` بيفعّله؛ من غيره مفيش حاجة بتتسجّل. فوق `SecretsValidator` عشان القيم اللي جاية من الـ vault تتحسب متظبطة |
| **Application Insights** | ✅ متوصّل. `APPLICATIONINSIGHTS_CONNECTION_STRING` بيفعّله. مش بديل لـ Prometheus ولا Serilog — ده الـ APM اللي الاتنين دول مش بيغطوه |
| **Sentry (فرونت اند)** | ✅ متوصّل. `environment.sentryDsn` بيفعّله. بيتحمّل ديناميك، فمش بيزوّد الـ initial bundle لو مش متظبط |

**اللي باقي عليك**: تحط القيم دي فعليًا. الأربعة كلهم **خاملين** لحد ما تتظبط، فمفيش أي تغيير في
السلوك الحالي من غيرها.

**لسه مفتوح وشغل كود**: Correlation ID، queue/background jobs، Infrastructure as Code، alerting
مربوط بالـ `/metrics`، وفحص إتاحة تلقائي (axe).
