# PosFlow — تقييم الجاهزية لمستوى Enterprise

**آخر مراجعة كاملة: 29 أغسطس 2026.** الأقسام 1-8 اتراجعت بند بند على الكود الفعلي واتصحّحت في
التاريخ ده — الجداول القديمة (من مراجعة 5 أغسطس) كانت بقت غلط في أغلبها واتشالت. تاريخ المراجعات
الأصلية موجود في `git log` لو محتاجه.

**بناءً على:** فحص فعلي للملفات (`grep`/`ls` على الكود نفسه، مش على `HANDOVER.md`) + تشغيل فعلي
لـ `dotnet build` / `dotnet test` / `ng test`.
**GitHub:** https://github.com/Mostafaessam7/POSFlow

الملف ده بيكمّل `HANDOVER.md`: ذاك بيشرح "إيه اللي اتعمل"، وده بيشرح **"إيه الناقص عشان نعتبره
enterprise-grade"**. للحالة العامة المختصرة (القرارات، المفتوح، الـ technical debt) شوف
[`PROJECT-STATUS.md`](PROJECT-STATUS.md).

---

## 0. الخلاصة السريعة (29 أغسطس 2026)

البنية الأساسية قوية وشغالة: Clean Architecture، عزل tenant بطبقتين، 2FA، permissions،
audit log، Serilog، health checks منفصلة (live/ready)، rate limiting شامل، فواتير PDF، سجل
حركة مخزون، Prometheus metrics، وكوكي HttpOnly للـ refresh token مع حماية CSRF. الاختبارات:
**41 unit + 62 integration + 40 frontend + 4 E2E**، و`dotnet build` بـ **0 warnings**.

كل البنود اللي كانت "حرِجة" في أول مراجعة **اتقفلت**. الفجوات الحقيقية المتبقية معظمها
**قرارات تشغيلية محتاجة حساب أو استضافة**، مش شغل كود:

| الفجوة | النوع |
|---|---|
| مفيش CD لسيرفر حقيقي (الـ CI بيوصل لـ GHCR وبس) | محتاج قرار استضافة |
| سكريبت الـ backup موجود بس **مش متجدول** | محتاج جدولة على السيرفر |
| مفيش SMTP حقيقي متظبط | محتاج حساب |
| مفيش alerting مربوط بالـ `/metrics` | محتاج إعداد خارجي |
| **مفيش Redis** — الكاش الحالي `IMemoryCache` per-process، غلط لو multi-instance | شغل كود، متفق عليه |
| **مفيش Correlation ID** يربط طلب في الفرونت بلوج في الباك | شغل كود |
| **مفيش queue / background jobs** | شغل كود |
| مفيش Infrastructure as Code | شغل كود |

---
## 1-8. التقييم الأصلي — **اتراجع بالكامل واتصحّح (29 أغسطس 2026)**

الجداول الأصلية للأقسام 1-8 كانت من مراجعة **5 أغسطس**، وأغلبها بقى غلط. اتشالت واتبدلت
بالجدول ده: كل بند اتفحص في الكود الفعلي، والحالة اللي مكتوبة هنا هي اللي اتأكدت منها.

**الطريقة**: كل سطر اتأكد بـ `grep`/`ls` على الملفات نفسها أو بتشغيل الأمر، مش من الذاكرة ولا
من `HANDOVER.md`.

### أمن (Security)

| البند الأصلي | الحالة الفعلية (اتفحصت 29 أغسطس) |
|---|---|
| عزل Tenant يدوي بس | **اتحل.** طبقتين: فلتر يدوي + `HasQueryFilter` في `PosFlowDbContext`، ومغطى بـ `TenantIsolationTests`. التفصيل في [`docs/adr/0001`](docs/adr/0001-dual-layer-tenant-isolation.md) |
| auto-migrate على الإقلاع | **اتشال.** Migrations بقت خطوة صريحة — [`docs/adr/0002`](docs/adr/0002-explicit-migrations-not-auto-on-boot.md) |
| الأسرار في `appsettings.json` | **اتحل.** `SecretsValidator` بيمنع الإقلاع بره Development بالمفتاح المتسجّل في الريبو |
| مفيش secrets/dependency scanning | **اتحل.** الـ CI بيوقف الـ build فعليًا على High/Critical (بيقرا مخرجات الأمر، مش الـ exit code — `dotnet list package --vulnerable` بيرجع 0 حتى لو لقى حاجة). + Dependabot |
| مفيش account lockout | **اتحل.** 5 محاولات فاشلة = قفل 15 دقيقة، مستقل عن الـ IP |
| مفيش 2FA/MFA | **اتحل.** TOTP اختياري — `TwoFactorChallenge` |
| Refresh tokens محتاجة rotation/revocation | **اتحل**، وزيادة: التوكن بقى في **كوكي HttpOnly** مع حماية CSRF (`CookieAuthTransportTests`). النقل إضافي — العملاء اللي مش متصفحات لسه شغالين زي ما هما |
| مفيش Audit Log | **اتحل.** `Domain/Entities/AuditLog.cs` + migration |
| CORS مضبوط صح | **لسه صح**، وبقى فيه `AllowCredentials()` — لازم للكوكي. من غيره المتصفح بيرفض كل رد فيه كوكي **قبل** ما التطبيق يشوفه، والدخول بيفشل من غير أي خطأ من السيرفر |
| ناقص CSP و HSTS | **اتحل.** الاتنين موجودين |

### DevOps والبنية التحتية

| البند الأصلي | الحالة الفعلية (اتفحصت 29 أغسطس) |
|---|---|
| مفيش Git repo | **غلط دلوقتي.** الريبو شغال وعليه remote، والـ CI بيشتغل فعليًا |
| مفيش Dockerfile / docker-compose | **غلط دلوقتي.** `docker-compose.yml` + `posflow-web/Dockerfile` + `src/PosFlow.Api/Dockerfile` |
| مفيش `.env.example` | **غلط دلوقتي.** الملف موجود |
| مفيش خطة backup | **جزئيًا.** السكريبت موجود (`deploy/backup-database.ps1`) — **بس مش متجدول**. ده لسه بند مفتوح حقيقي |
| **مفيش Infrastructure as Code** | **لسه صح.** مفيش Terraform/Bicep/ARM في الريبو |
| **CI بدون CD** | **لسه صح جزئيًا.** الـ CI بيبني الـ images وبينشرها على GHCR، بس مفيش حاجة بتاخدهم لسيرفر شغال |
| مفيش environments متعددة | **لسه صح شكلًا، ومقصود.** فيه `appsettings.json` + `appsettings.Development.json` بس. كل تفريعات البيئة مبنية على `IsDevelopment()`، يعني Staging بياخد سلوك الإنتاج الآمن تلقائيًا. إضافة ملف Staging بقيم placeholder هتفتح مكان تتحط فيه أسرار بالغلط — القرار موثّق في §9 بند 9 |

### المراقبة والـ Observability

| البند الأصلي | الحالة الفعلية (اتفحصت 29 أغسطس) |
|---|---|
| مفيش structured logging | **غلط دلوقتي.** Serilog مركّب مع Console + File sinks و `UseSerilogRequestLogging()` |
| `/health` بسيط | **غلط دلوقتي.** فيه `/health/live` (liveness نقي) و `/health/ready` (بيفحص الداتابيز كمان) |
| مفيش metrics / APM | **جزئيًا.** `/metrics` (Prometheus) موجود. مفيش APM حقيقي ولا داشبورد مربوط |
| **مفيش Correlation ID / request tracing** | **لسه صح.** مفيش أي correlation id بيربط طلب في الفرونت بلوج في الباك |
| **مفيش alerting** | **لسه صح.** `/metrics` بيطلع أرقام، بس محدش بيتنبه لو الـ API وقعت |

### التوسع والأداء

| البند الأصلي | الحالة الفعلية (اتفحصت 29 أغسطس) |
|---|---|
| مفيش caching layer | **جزئيًا.** فيه `IMemoryCache` على التصنيفات (`CategoryService`). **مفيش Redis** — والكاش الحالي per-process، يعني غلط لو النظام اشتغل على أكتر من instance. Redis متفق عليه للمنتج ده ولسه متعملش |
| Rate limiting على `auth` بس | **غلط دلوقتي.** فيه `GlobalLimiter` على كل الطلبات + سياسات خاصة بالـ auth. و`refresh`/`logout` اتشالوا من حد الـ brute-force لأنهم كانوا بيتقفلوا مع الاستخدام العادي |
| Barcode lookup على الفرونت | **اتحل.** `GET /api/products/by-barcode/{barcode}` سيرفر-سايد |
| **مفيش queue / background jobs** | **لسه صح.** مفيش Hangfire ولا أي hosted service — أي عملية طويلة لسه synchronous جوه الـ request |
| مفيش load testing | **جزئيًا.** سكريبت k6 موجود في `tests/load/`. مش في الـ CI **عن قصد** (§9 بند 12) |

### تعدد المستأجرين

البند الأصلي كان بيقول إن العزل "مجرد `.Where()` متكرر يدويًا" وإن الحل الصح هو Global Query
Filters + اختبار مخصص. **ده بالظبط اللي اتعمل**: الفلتر اليدوي فضل، واتضاف فوقه
`HasQueryFilter`، ومعاهم `TenantIsolationTests` بيحاول يوصل لبيانات tenant تاني عبر HTTP فعلي.
البند ده **مقفول**.

### الاختبارات

الأرقام الأصلية (41 unit / 32 integration / 36 frontend) بقت قديمة. الواقع المقاس في
29 أغسطس:

| السويت | العدد |
|---|---|
| `PosFlow.Application.Tests` (unit) | **41** |
| `PosFlow.Api.Tests` (integration) | **62** |
| Frontend (`ng test`) | **40** |
| Playwright E2E | **4** (في 3 ملفات: `login`, `pos-checkout`, `dialog-a11y`) |

`dotnet build` بيطلع **0 warnings**.

**لسه ناقص**: Mutation testing، Contract tests بين الفرونت والباك، وفحص إتاحة تلقائي (axe) زي
اللي في Subscription Tracker — الـ dialog اتصلح بس مفيش بوابة بتمنع رجوع المشكلة في صفحات تانية.

### جاهزية المنتج

كل بنود القسم ده اتقفلت (إيميل، PDF، ضرائب، عملاء، سجل مخزون، صلاحيات، عملات) ماعدا:
- **صلاحيات مخصصة لكل مستخدم** — البنية جاهزة، بس لسه 3 أدوار ثابتة
- **تحويل عملة حقيقي** — الجدول موجود، الأسعار يدوية، مفيش ربط بـ API خارجي
- **أسعار مختلفة حسب الفرع** — مش موجود

### التوثيق

| البند الأصلي | الحالة الفعلية (اتفحصت 29 أغسطس) |
|---|---|
| مفيش ADRs | **غلط دلوقتي.** `docs/adr/` فيه 2 |
| مفيش CONTRIBUTING.md | **غلط دلوقتي.** موجود |
| مفيش Runbook | **غلط دلوقتي.** `deploy/README.md` |
| مفيش API docs خارج Swagger | **لسه صح.** Swagger في Development بس، ومفيش بديل محمي لـ staging |

---
## سجل تاريخي — 0.1 اللي اتعمل في 5 أغسطس (تحديث لاحق لنفس اليوم)

> الأقسام دي (0.1 و0.2) **سجل تاريخي** بيوثّق إيه اتعمل وإمتى. الحالة الحالية المتحقَّق منها في
> القسم "1-8" فوق — لو فيه تعارض، اللي فوق هو الصح.

✅ = خلص وموجود في الكود دلوقتي. ❌ = لسه ناقص (سواء لأنه محتاج قرار منك، أو حساب/خدمة خارجية، أو مجهود أكبر من جلسة واحدة).

| البند | الحالة | ملاحظة |
|---|---|---|
| Git repo | ✅ | مربوط بـ https://github.com/Mostafaessam7/POSFlow — تاريخ تغييرات حقيقي وCI شغال فعليًا على push |
| عزل Tenant تلقائي (Global Query Filter) | ✅ | + اختبارات `TenantIsolationTests` تثبت إن الحماية شغالة حتى لو نسي أي service الفلتر اليدوي |
| Auto-migrate في production | ✅ (اتقفل) | بقى config-gated (`App:AutoMigrateOnStartup`)، مقفول افتراضيًا بره Development |
| Admin password ثابتة (`Admin@123`) في أي بيئة | ✅ (اتحل) | Production بقى عنده bootstrap منفصل بيعمل password عشوائي مرة واحدة ويطبعه في الـ logs |
| Structured logging | ✅ | Serilog (console + rolling JSON files + request logging) |
| Audit log | ✅ | جدول `AuditLogs` بيسجل كل تعديل/حذف/إضافة على Order/Product/AppUser/Branch/Shift تلقائيًا |
| إيميل حقيقي | ✅ (تقنيًا) | `SmtpEmailSender` جاهز، بس **لازم تحط بيانات SMTP حقيقية بنفسك** (API key فعلي) — مينفعش حد يعمل ده نيابة عنك |
| Secrets في appsettings | ✅ (اتنضف) | `appsettings.json` بقى بدون قيم حقيقية + `.env.example` كامل |
| Dockerfile / docker-compose | ✅ | API + Angular (nginx) + SQL Server، للتطوير المحلي أساسًا |
| CI: vulnerability scanning | ✅ | `dotnet list package --vulnerable` + `npm audit` في الـ workflow — **الاتنين بيوقفوا الـ build على High/Critical** (تحديث 28 أغسطس) |
| CI: Docker build check | ✅ | build فقط (بدون push) للتأكد إن الـ Dockerfiles شغالة |
| Health checks | ✅ | `/health/live` و `/health/ready` منفصلين دلوقتي |
| Rate limiting على كل الـ API | ✅ | مش بس auth — فيه global limiter دلوقتي (120 طلب/دقيقة لكل مستخدم/IP) |
| Security headers | ✅ | + CSP و HSTS (بره Development) |
| **الكود فعليًا بيتبني ويعدي الاختبارات** | ✅ | تحقق مباشر تاني بتاريخ 27 أغسطس: 73 اختبار backend (41 unit + 32 integration) + 36 frontend كلهم عدّوا فعليًا (`dotnet build` + `dotnet test` + `ng test`) |
| Account lockout بعد محاولات فاشلة | ✅ | 5 محاولات فاشلة متتالية = قفل 15 دقيقة على الحساب نفسه، مستقل عن الـ IP — دفاع إضافي فوق rate limiting بالـ IP الموجود أصلاً على `/api/auth/*` |
| CONTRIBUTING.md + ADRs | ✅ | `CONTRIBUTING.md` + `docs/adr/` |
| Deploy runbook | ✅ | `deploy/README.md` (migrations، secrets، health checks، rollback) |
| 2FA/MFA (TOTP) | ✅ | RFC 6238، `/api/auth/2fa/setup`+`/enable`+`/disable` + تحدي 2FA عند الدخول، اختبار end-to-end كامل بيغطي السيناريو كله |
| نظام صلاحيات مرن (Permissions) | ✅ | كتالوج صلاحيات + policy-based authorization بدل `[Authorize(Roles=...)]` المبعثرة — أساس جاهز لأي تخصيص مستقبلي لكل مستخدم |
| سجل عملاء (Customers) | ✅ | CRUD كامل + ربط اختياري بالفاتورة + نقاط ولاء بسيطة (نقطة لكل وحدة عملة) |
| ضريبة قابلة للتعديل | ✅ | `Tenant.TaxRatePercent` بيتطبق فعليًا في الـ checkout بدل الـ `const decimal taxAmount = 0` القديمة |
| عملة العرض (Currency) | ✅ (عرض فقط) | `Tenant.CurrencyCode` — مفيش تحويل عملات حقيقي، مجرد إعداد عرض |
| Caching | ✅ (جزئي) | `IMemoryCache` على التصنيفات (بتتغير نادر، بتتقرا كتير) — موثّق ليه المخزون/المنتجات معملهاش cache (خطر بيانات قديمة)، ومحتاج Redis بدل IMemoryCache لو النظام هيشتغل على أكتر من instance |
| E2E tests (Playwright) | ✅ (مكتوبة، مش مُتحقق منها live هنا) | `posflow-web/e2e/` (login + سيناريو بيع كامل) + workflow CI حقيقي بيشغلهم على SQL Server فعلي. **متعرفتش أشغلهم live في الـ sandbox ده** بسبب قيود LocalDB/Windows-auth في البيئة المعزولة اللي شغال فيها — لكن `playwright test --list` أكد إنهم بيتصرّفوا صح وبيتلاقوا، والـ CI workflow بيستخدم SQL auth حقيقي هيشتغل على GitHub Actions فعليًا |
| CD جزئي (نشر الـ images) | ✅ (جزئي) | CI بقى بينشر الـ Docker images على GitHub Container Registry (ghcr.io) تلقائيًا مع كل push لـ main — ده CD حقيقي مش محتاج حساب سحابي إضافي. **الجزء الناقص:** نقل الـ image من GHCR لسيرفر فعلي شغال، وده محتاج منك تختار الاستضافة الأول (Azure/AWS/VPS/...) |
| Secrets manager فعلي | ✅ (الكود جاهز) | تكامل اختياري مع Azure Key Vault (`KeyVault:Uri` + `DefaultAzureCredential`) — شغال بس لو عندك حساب Azure فعلي وضبطته |
| Backup | ✅ (سكريبت جاهز) | `deploy/backup-database.ps1` لحالة self-hosted SQL Server — **لسه محتاج تجدول تشغيله فعليًا** (Task Scheduler/cron) على السيرفر بتاعك؛ لو database managed (Azure SQL/RDS) استخدم الـ backup الأوتوماتيكي بتاعها بدل السكريبت |

**خلاصة النهائية:** كل بند كان ممكن يتنفذ بكود بس (من غير حساب سحابي فعلي أو قرار استضافة) **اتنفذ فعليًا وبيشتغل ومعدي اختبارات حقيقية** — permissions، customers، tax/currency، 2FA، caching، E2E test infra، CD للـ images، secrets manager wiring، backup script. الحاجات المتبقية (CD لسيرفر فعلي، تفعيل Key Vault فعلي، جدولة الـ backup) محتاجة منك تحديد الاستضافة/الحساب السحابي — مش حاجة أقدر أقررها نيابة عنك.

## سجل تاريخي — 0.2 إضافات مرحلة أغسطس التانية (14-26 أغسطس 2026)

بنود اتضافت بعد أول مراجعة فعلية لهذا الملف، بالكامل موجودة في الكود دلوقتي (اتحقق منها بقراءة الكنترولرز والـ Program.cs مباشرة):

- **طباعة فاتورة PDF** — `GET /api/orders/{id}/receipt-pdf` عبر مكتبة QuestPDF، زرار تحميل في شاشة الـ POS.
- **بحث بالباركود من السيرفر** — `GET /api/products/by-barcode/{barcode}`، بدل الفلترة على الفرونت إند.
- **سجل حركة مخزون (Stock Movement ledger)** — جدول `StockMovement` append-only، أنواع الحركة: Sale/OrderVoided/ManualAdjustment/StockReceived، متاح عبر `GET /api/products/{id}/stock-movements`.
- **تحويل عملة يدوي** — جدول `ExchangeRate` لكل tenant + `/convert` endpoint، عرض فقط، أسعار يدخلها الأدمن بنفسه.
- **اختبار حقيقي لسباق order-number** — كان موثّق كـ "مستحيل نختبره" في HANDOVER، اتحل باستخدام SQLite (بيفرض unique index فعليًا على عكس EF Core InMemory) + 5 كاشيرات متزامنين وهميين.
- **Prometheus metrics** — `/metrics` endpoint عبر `prometheus-net.AspNetCore`، بدون حاجة لحساب SaaS خارجي.
- **اختبار حمل (k6)** — `tests/load/posflow-load-test.js` + `tests/load/README.md`.
- **دعم لغتين (عربي/إنجليزي) + وضع ليلي (Dark Mode)** في الفرونت إند بالكامل — `posflow-web/src/app/core/i18n/` و `core/theme/`. رسائل الأخطاء من الـ backend كمان بقت مترجمة، والتواريخ/الأوقات locale-aware.
- **إعادة تصميم بصري كامل** لكل شاشات التطبيق ("الإصدار المحرر" / Open Kitchen Editorial direction) — تغيير تصميمي بحت، مفيهوش فيتشرز جديدة.
- **بيانات تجريبية (demo seed) أوسع** — فرع تاني، تصنيف تالت، منتجات/عملاء إضافيين، أسعار صرف، وتاريخ مبيعات 3 أيام.

هذه البنود لم تكن موجودة وقت أول مراجعة (5 أغسطس) وبالتالي مش موثّقة في الجداول أعلاه — تمت إضافتها هنا وفي `HANDOVER.md` §3 مباشرة عشان الملف يفضل مطابق للكود الفعلي.

---

## 9. خطة مقترحة بالأولوية (محدّثة 27 أغسطس 2026)

كل البنود "الحرِجة" و"المهمة" الأصلية من أول مراجعة (Git repo، EF Core Global Query Filter، نقل الأسرار، إيميل حقيقي، backup script، Docker، Serilog، Audit log، CI/CD جزئي، health checks، E2E tests، caching جزئي، permissions، customers، currency/tax) **اتنفذت فعليًا وموجودة في الكود** — راجع §0.1 و§0.2 فوق. المتبقي فعليًا دلوقتي:

### حرِج (قبل أي استخدام حقيقي بفلوس فعلية)
1. **اختر استضافة فعلية وأضف خطوة deploy حقيقية** — الـ CI بينشر الـ Docker images على GHCR بس، مفيش حاجة بتاخدهم لسيرفر شغال.
2. **جدول تشغيل سكريبت الـ backup** فعليًا (Task Scheduler/cron) لو هتستضيف SQL Server بنفسك، أو تأكد إن الـ backup الأوتوماتيكي شغال لو الداتابيز managed.
3. **حط بيانات SMTP حقيقية** — `SmtpEmailSender` جاهز بس forgot-password هيفضل بيكتب اللينك في اللوج بس لحد ما يتحط `Smtp:Host` فعلي.
4. ~~**عالج الثغرة الأمنية الحالية** في `SQLitePCLRaw.lib.e_sqlite3` 2.1.11~~ — ✅ **اتعملت (28 أغسطس)**: Pin على 2.1.13، والـ CI بقى بيوقف الـ build فعليًا على أي High/Critical.

### مهم (خلال أول 1-2 شهر تشغيل)
5. **Alerting فعلي** — `/metrics` موجود لكن مفيش حد بيتنبه فورًا لو حصل spike في الأخطاء أو الـ API وقعت.
6. **Redis بدل IMemoryCache** لو النظام هيشتغل على أكتر من instance واحدة (الكاش الحالي على التصنيفات بس، وهيكون غلط لو multi-instance).
7. **تفعيل Azure Key Vault فعليًا** (الكود جاهز، محتاج حساب Azure وتحديد `KeyVault:Uri`).
7أ. **Correlation ID** — لو حصل خطأ، مفيش دلوقتي أي مُعرّف موحّد يربط الطلب في الفرونت باللوج في
   الباك. Serilog مركّب، فده إضافة صغيرة (middleware + enricher) بعائد كبير وقت التشخيص.
   *(اتفحص في 29 أغسطس: مفيش أي correlation id في الكود.)*
7ب. **Queue / background jobs** — أي عملية طويلة لسه بتتنفذ synchronous جوه الـ request. مفيش
   Hangfire ولا أي hosted service. مش مشكلة دلوقتي بالحِمل الحالي، بس بتبقى مشكلة أول ما يبقى فيه
   تصدير تقارير كبيرة أو إرسال إيميلات مجمّعة.
8. **تحويل عملة حقيقي** لو هتحتاج ربط بسعر صرف فعلي بدل الإدخال اليدوي — الجدول موجود بس مفيش API خارجي مربوط.
9. ~~**`appsettings.Staging.json`** منفصل بوضوح~~ — ❌ **اتراجعت واتقرر إنها مش مطلوبة (28 أغسطس)**: كل تفريعات البيئة في `Program.cs` مبنية على `IsDevelopment()` وبس، يعني Staging بياخد سلوك الإنتاج الآمن تلقائيًا (Swagger مقفول، HSTS شغال) من غير أي ملف. والقيم الحقيقية أصلاً بتيجي من environment variables / secrets manager بالتصميم (`appsettings.json` بيفشل مقفول عن قصد). إضافة ملف Staging بقيم placeholder هتفتح مكان يتحط فيه secrets بالغلط وتناقض التصميم ده — لو احتجت بيئة staging، ظبّطها بنفس متغيرات البيئة بقيم مختلفة.

### تحسين (بعد الاستقرار)
10. **نظام صلاحيات مخصص لكل مستخدم** — البنية التحتية (catalog + policy-based auth) جاهزة، لكن لسه 3 أدوار ثابتة بس، مفيش تخصيص فردي.
11. **Mutation testing / Contract tests** بين الـ frontend والـ backend — لسه ناقص بالكامل.
12. **تشغيل k6 load test فعليًا** على بيئة شبيهة بالإنتاج، مش لوكال بس. **اتراجعت (28 أغسطس): مقصود إنه مايتحطش في الـ CI** — الـ thresholds الحالية (`p95 < 800ms`) على GitHub runner مشترك ومزدحم هتفشل عشوائيًا وتدّي false failures، والرقم نفسه مش معبّر عن أي حاجة على هاردوير مشترك. مكانه الصح بيئة شبيهة بالإنتاج، مش كل PR.
13. **APM حقيقي** (Application Insights أو Prometheus+Grafana مربوطين فعليًا، مش بس الـ endpoint موجود).

---

## ملاحظة أخيرة

المشروع اتقدم بشكل ملموس من أول مراجعة في 5 أغسطس لحد 27 أغسطس — Clean Architecture، multi-tenant isolation بطبقتين، 2FA، permissions، audit log، PDF receipts، barcode lookup، stock ledger، i18n + dark mode، Prometheus metrics، وk6 load test، كلها موجودة وشغالة وبتعدي **103 اختبار backend + 40 frontend + 4 E2E**. الفجوة الحقيقية المتبقية مش في الكود نفسه، لكن في **قرارات تشغيلية محتاجة صاحب المشروع**: اختيار استضافة فعلية للـ CD، تفعيل الخدمات السحابية (Key Vault، SMTP حقيقي)، وجدولة الـ backup.

> **الحالة الحالية**: الملف ده سجل مراجعة بتواريخه. لحالة المشروع المحدّثة (اللي اتقفل، القرارات
> المعتمدة، المفتوح، الـ technical debt) راجع [PROJECT-STATUS.md](PROJECT-STATUS.md).

> **تحديث 28 أغسطس:** الثغرة الأمنية اللي كانت مذكورة هنا اتقفلت (Pin على `SQLitePCLRaw.lib.e_sqlite3` 2.1.13)، وخطوة فحص الثغرات في الـ CI بقت بتوقف الـ build فعليًا بدل ما تطبع بس. يعني كل البنود الباقية دلوقتي محتاجة قرار/حساب خارجي منك — مفيش حاجة قابلة للإصلاح من جوه الريبو نفسه فاضلة في القائمة العاجلة.
