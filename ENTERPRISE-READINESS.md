# PosFlow — تقييم الجاهزية لمستوى Enterprise

**تاريخ المراجعة:** 5 أغسطس 2026، **آخر تحديث: 27 أغسطس 2026** (مراجعة كاملة للكود الفعلي بعد أكتر من 20 commit إضافي منذ آخر مراجعة — راجع `git log` على `main`)
**بناءً على:** فحص فعلي للكود في `src/`, `posflow-web/`, `tests/`, `.github/` (وليس فقط على `HANDOVER.md`) + تشغيل فعلي لـ `dotnet build`/`dotnet test`/`npm test`
**GitHub:** https://github.com/Mostafaessam7/POSFlow

> **تحديث 27 أغسطس:** الجداول تحت (أقسام 1-8) بتوثّق الصورة زي ما كانت وقت المراجعة الأولى في 5 أغسطس، وفيها بنود بقت قديمة (اتحلت لاحقًا) — علّمنا كل بند اتحل بـ "✅ (تحديث 27 أغسطس)" جنبه. **قسم §0.1 تحت هو مصدر الحقيقة للحالة الحالية**، وبعده §0.2 بيغطي إضافات مرحلة أغسطس التانية (بعد 14 أغسطس) اللي معملهاش عليها إشارة في الجداول الأصلية خالص: طباعة PDF، بحث بالباركود من السيرفر، سجل حركة مخزون، تحويل عملة يدوي، Prometheus metrics، اختبار حمل k6، ودعم لغتين + وضع ليلي في الفرونت إند.

هذا الملف يكمّل `HANDOVER.md` — ذاك يشرح "إيه اللي اتعمل"، وده يشرح **"إيه الناقص عشان نعتبره enterprise-grade"**، مرتب حسب الأولوية.

---

## 0. الخلاصة السريعة

المشروع بنيته الأساسية كويسة فعلاً (Clean Architecture، multi-tenant من الأول، FluentValidation، rate limiting، health checks، global exception handler). لكنه لسه **prototype قوي**، مش enterprise بعد. أكبر فجوة متبقية:

- **الريبو لسه مش متربط بـ remote في كل بيئة عمل.** الكود موجود على GitHub فعليًا (https://github.com/Mostafaessam7/POSFlow)، لكن لو لقيت `git status` بيقول "not a git repository" في نسخة معينة من المجلد، معناه إنها نسخة محلية لسه محتاجة `git init` + ربط الـ remote — راجع تعليمات الإعداد في `README.md`. من غير ده الـ CI في `.github/workflows/ci.yml` مش هيشتغل فعليًا لأنه محتاج push حقيقي على GitHub.

**الفجوة الحرجة التانية كانت عزل الـ Tenant اليدوي — دي **اتحلت** فعليًا (تحويلها لـ EF Core Global Query Filter + اختبار تلقائي)، شوف تفصيلها في §0.1 و§5 تحت.**

---

## 1. أمن (Security) — أولوية قصوى

| المشكلة | التفاصيل | الحل المقترح |
|---|---|---|
| **عزل Tenant يدوي** | مفيش EF Core `HasQueryFilter` global filter على `TenantId` في `PosFlowDbContext.cs`. كل service بيفلتر يدوي. | ضيف `modelBuilder.Entity<T>().HasQueryFilter(e => e.TenantId == _currentTenantId)` على كل entity فيها TenantId، عشان تبقى طبقة حماية إضافية تلقائية مش معتمدة على تذكّر المبرمج. |
| **auto-migrate على الإقلاع** | `Program.cs` بينادي `DatabaseSeeder.SeedAsync` اللي بيعمل `Database.MigrateAsync()` **في كل مرة يشتغل فيها الـ app**، بما فيها production. | في enterprise، الـ migrations لازم تتشغل كخطوة منفصلة في الـ deployment pipeline (`dotnet ef database update` أو migration bundle)، مش أوتوماتيك عند كل boot — عشان تتجنب race conditions لو شغال أكتر من instance، ولازم يكون فيه مراجعة/موافقة قبل أي schema change في production. |
| **الأسرار في appsettings.json** | `Jwt:Key` قيمة افتراضية ثابتة في الملف (`PosFlow-Development-Key-Change-Me-2026...`)، والـ connection string كمان. | لازم Azure Key Vault / AWS Secrets Manager / environment variables، وممنوع أي سر حقيقي يتحط في ملف بيتراجع في git. |
| **مفيش secrets scanning / dependency scanning** | الـ CI الحالي بيعمل build + test بس. | ضيف `dotnet list package --vulnerable`, `npm audit`, وأداة زي Gitleaks/Trivy/Dependabot. |
| **مفيش account lockout بعد محاولات فاشلة** | ✅ اتحل — `AppUser.FailedLoginAttempts`/`LockoutEndUtc`، 5 محاولات فاشلة متتالية = قفل 15 دقيقة، بغض النظر عن الـ IP (دفاع ضد محاولات موزعة على حساب معين، بالإضافة لـ rate limiting بالـ IP الموجود أصلاً). مغطى باختبارات `AuthServiceLockoutTests`. | تنبيه إيميل للمستخدم عند القفل لسه مش موجود — تحسين مستقبلي بسيط. |
| **مفيش 2FA/MFA** | مفيش أي طبقة تحقق ثانية، حتى للـ Admin. | مهم جدًا لأي نظام بيتعامل مع مبيعات وفلوس فعلية، الأقل للـ Admin role. |
| **Refresh tokens** | موجودة، بس لازم تتأكد إن فيه rotation + revocation list (خصوصًا لو جهاز اتسرق). | راجع `AuthService.cs` تتأكد إن كل refresh بيلغي التوكن القديم فعليًا مش بس بيصدر جديد. |
| **مفيش Audit Log** | مفيش جدول يسجل "مين عمل إيه وإمتى" (تعديل سعر، حذف منتج، void لأوردر، تغيير صلاحيات مستخدم). | أساسي لأي نظام مالي/enterprise لأسباب قانونية ومحاسبية. |
| **CORS** | مضبوط صح (fails closed) — نقطة إيجابية، سيبها زي ما هي. |  |
| **Security headers** | موجودة أساسيات (`X-Content-Type-Options`, `X-Frame-Options`, HTTPS redirect) — كويس، بس ناقص `Content-Security-Policy` و `Strict-Transport-Security` (HSTS). |  |

---

## 2. DevOps والبنية التحتية

| المشكلة | التفاصيل |
|---|---|
| **مفيش Git repo** | أهم حاجة قبل أي حاجة تانية. من غيره: مفيش branching, مفيش PR review, الـ CI في `.github/workflows` ميت (محتاج GitHub فعلي). |
| **مفيش Dockerfile / docker-compose** | مفيش أي تعريف container لا للـ API ولا للـ Angular app ولا لـ SQL Server. يعني تشغيل المشروع محلي بس، ومفيش طريقة موحدة للنشر. |
| **مفيش Infrastructure as Code** | مفيش Terraform/Bicep/ARM — كل حاجة "هتتظبط يدوي" على السيرفر. |
| **CI بدون CD** | فيه build+test بس، مفيش أي خطوة نشر (deploy) حتى لبيئة staging. |
| **مفيش environments متعددة واضحة** | فيه `appsettings.Development.json` بس، مفيش `appsettings.Staging.json` / `Production.json` منفصلين بوضوح. |
| **مفيش خطة نسخ احتياطي (backup)** | مذكور صراحة في `HANDOVER.md` §6 إنها "لسه متعمولاش" — حرفيًا صفر backup strategy لقاعدة بيانات فيها مبيعات فعلية. |
| **مفيش .env / secrets management عملي** | لا يوجد ملف `.env.example` ولا توثيق لمتغيرات البيئة المطلوبة فعليًا للنشر. |

---

## 3. المراقبة والـ Observability

| المشكلة | التفاصيل |
|---|---|
| **مفيش structured logging** | لا يوجد Serilog/NLog، الاعتماد على الـ default `ILogger` بدون sinks حقيقية (زي Seq/ELK/Application Insights). صعب تتبع مشكلة في production من غير كده. |
| **مفيش Correlation ID / Request tracing** | لو حصل خطأ، مفيش ID موحد تتبعه بين الـ request في الـ frontend والـ log في الـ backend. |
| **مفيش metrics / APM** | ✅ (تحديث 27 أغسطس) — فيه دلوقتي `/metrics` (Prometheus text format عبر `prometheus-net.AspNetCore`)، جاهز لأي Prometheus/Grafana ذاتي الاستضافة. مفيش لسه Application Insights أو أي APM SaaS، ومفيش alerting مربوط بيه فعليًا. |
| **`/health` بسيط** | بيتأكد بس إن الداتابيز شغالة — كويس كبداية بس محتاج تفصيل أكتر (readiness vs liveness) في بيئة container/k8s. |
| **مفيش alerting** | حتى لو حصل exception غير متوقع أو الـ API وقعت، مفيش حد هيتنبه فورًا. |

---

## 4. التوسع والأداء (Scalability & Performance)

| المشكلة | التفاصيل |
|---|---|
| **مفيش caching layer** | مفيش Redis/in-memory cache لأي حاجة (مثلاً قائمة المنتجات اللي بتتقرا كتير في نفس الشيفت). |
| **Rate limiting على `auth` بس** | باقي الـ endpoints (خصوصًا `checkout`) مفيهاش أي حماية من abuse أو حِمل مفاجئ. |
| **مفيش queue/background jobs** | أي عملية طويلة (تقارير، إيميلات، مستقبلًا تصدير Excel) بتتنفذ synchronous جوه الـ request. مفيش Hangfire/Azure Functions/queue. |
| **Barcode lookup** | ✅ (تحديث 27 أغسطس) — بقى فيه `GET /api/products/by-barcode/{barcode}` سيرفر-سايد (`ProductsController`)، مربوط في شاشة الـ POS بدل الفلترة على الفرونت إند. |
| **مفيش Load testing** | ✅ جزئي (تحديث 27 أغسطس) — فيه سكريبت k6 (`tests/load/posflow-load-test.js`) بيغطي تصفح الكتالوج والـ checkout، لكن مش جزء من الـ CI ولا اتشغل على بيئة شبيهة بالإنتاج فعليًا. |

---

## 5. تعدد المستأجرين (Multi-tenancy) — نقطة مهمة جدًا هنا تحديدًا

- البنية صح من ناحية الـ schema (كل entity فيه `TenantId`، وفيه composite indexes زي `(TenantId, Code)`).
- لكن **العزل نفسه غير مُطبّق كطبقة مستقلة** — زي ما اتشرح فوق، هو مجرد `.Where()` متكرر يدويًا في كل service. ده تقني debt خطير: أي endpoint جديد يُضاف من غير الانتباه لده = تسريب بيانات.
- **الحل الصح لمستوى enterprise:** EF Core Global Query Filters + integration test مخصص اسمه حاجة زي `TenantIsolationTests` يتأكد إن مفيش endpoint واحد بيرجع بيانات tenant تاني، حتى لو الـ service نسي الفلتر.

---

## 6. الاختبارات (Testing)

| موجود | ناقص |
|---|---|
| Unit tests (Application layer) — 41 اختبار | ✅ **E2E tests** (Playwright) موجودة دلوقتي (تحديث 27 أغسطس) — `posflow-web/e2e/` (login + سيناريو بيع)، لسه قليلة عن قصد (أهم مسارين بس) |
| Integration tests (API, WebApplicationFactory) — 32 اختبار | ✅ جزئي **Load/performance tests** — سكريبت k6 موجود، مش جزء من CI |
| Frontend unit tests (guards, checkout logic) — 36 اختبار | ✅ **Security tests** موجودة (تحديث 27 أغسطس) — `TenantIsolationTests` + اختبارات HTTP فعلية لمحاولة وصول tenant تاني عبر الـ API |
| CI يشغّل الكل عند كل push (فعليًا شغال، الريبو مربوط بـ GitHub) | **Mutation testing** أو أي قياس فعلي لجودة الاختبارات (مش بس coverage %) — لسه ناقص |
| | **Contract tests** بين الـ frontend والـ backend — لسه ناقص |
| | ⚠️ فيه **ثغرة أمنية high-severity حالية** في حزمة SQLite الترانزيتيفية بمشروع الاختبارات (`SQLitePCLRaw.lib.e_sqlite3` 2.1.11, `GHSA-2m69-gcr7-jv3q`) — بتظهر في `dotnet list package --vulnerable` لكن مش بتوقف الـ CI |

---

## 7. جاهزية المنتج (Business Features)

القايمة دي منقولة ومُرتّبة من `HANDOVER.md` §5 لأنها فعلاً فجوات حقيقية لأي POS enterprise:

- **الإيميل مش شغال فعليًا بدون إعداد** — ✅ (تحديث 27 أغسطس) الكود جاهز (`SmtpEmailSender`)، بيشتغل تلقائيًا لو `Smtp:Host` متظبط؛ من غيره بيرجع لـ `LoggingEmailSender` (يكتب في الـ log بس).
- **مفيش طباعة فواتير / PDF export.** ✅ اتحل (تحديث 27 أغسطس) — `GET /api/orders/{id}/receipt-pdf` عبر QuestPDF، زرار تحميل في شاشة الـ POS.
- **مفيش نظام خصومات على مستوى الفاتورة ولا إعدادات ضرائب** — الخصم لسه على مستوى السطر بس (لسه صح). الضريبة ✅ اتحلت من قبل (`Tenant.TaxRatePercent` بيتطبق فعليًا في الـ checkout).
- **مفيش سجلات عملاء (customers)** — ✅ اتحل من قبل — CRUD كامل + ربط اختياري بالفاتورة + نقاط ولاء بسيطة.
- **مفيش سجل تدقيق على المخزون** (Stock Audit Trail) — ✅ اتحل (تحديث 27 أغسطس) — جدول `StockMovement` append-only (Sale/OrderVoided/ManualAdjustment/StockReceived) بيتسجل تلقائي من الـ checkout والـ void وتعديل المنتج اليدوي، ومتاح عبر `GET /api/products/{id}/stock-movements`.
- **مفيش أدوار صلاحيات دقيقة (granular permissions)** — ✅ جزئي اتحل من قبل — فيه كتالوج صلاحيات (`Permissions`) وpolicy-based authorization بدل `[Authorize(Roles=...)]`، لكن لسه بس 3 أدوار ثابتة (Admin/Manager/Cashier) كل واحد بمجموعة صلاحيات جاهزة — مفيش تخصيص صلاحيات لكل مستخدم لوحده.
- **مفيش تعدد عملات** أو دعم أسعار مختلفة حسب الفرع. ✅ جزئي اتحل (تحديث 27 أغسطس) — جدول `ExchangeRate` يدوي لكل tenant + endpoint `/convert`، لكنه **تحويل عرض فقط بأسعار الأدمن يدخلها بنفسه، مفيش ربط بـ API أسعار صرف خارجي**، ومفيش أسعار مختلفة حسب الفرع.

---

## 8. التوثيق (Documentation)

- `HANDOVER.md` قوي كملخص عام، لكن مفيش:
  - **API documentation** خارج الـ Swagger نفسه (اللي أصلاً متاح في Development بس — منطقي أمنيًا، بس محتاج بديل زي Swagger محمي بـ auth في staging على الأقل).
  - **Architecture Decision Records (ADRs)** — ليه اتاخد قرارات معينة (مثلاً: ليه TenantId مش global filter؟).
  - **Runbook** للعمليات (إيه اللي تعمله لو الداتابيز وقعت، لو فيه spike في الطلبات، إلخ).
  - **دليل مساهمة (CONTRIBUTING.md)** وقواعد coding standards موثقة (فيه `.editorconfig` بس مفيش شرح مكتوب).

---

## 0.1 اللي اتعمل فعليًا (تحديث لاحق لنفس اليوم)

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
| CI: vulnerability scanning | ✅ | `dotnet list package --vulnerable` + `npm audit` في الـ workflow |
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

## 0.2 إضافات مرحلة أغسطس التانية (14-26 أغسطس 2026)، مش موثّقة في الجداول الأصلية فوق

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
4. **عالج الثغرة الأمنية الحالية** في `SQLitePCLRaw.lib.e_sqlite3` 2.1.11 (`GHSA-2m69-gcr7-jv3q`) — موجودة دلوقتي فعليًا في مشروع الاختبارات، والـ CI بيبلّغ عنها بس مش بيوقف الـ build بسببها.

### مهم (خلال أول 1-2 شهر تشغيل)
5. **Alerting فعلي** — `/metrics` موجود لكن مفيش حد بيتنبه فورًا لو حصل spike في الأخطاء أو الـ API وقعت.
6. **Redis بدل IMemoryCache** لو النظام هيشتغل على أكتر من instance واحدة (الكاش الحالي على التصنيفات بس، وهيكون غلط لو multi-instance).
7. **تفعيل Azure Key Vault فعليًا** (الكود جاهز، محتاج حساب Azure وتحديد `KeyVault:Uri`).
8. **تحويل عملة حقيقي** لو هتحتاج ربط بسعر صرف فعلي بدل الإدخال اليدوي — الجدول موجود بس مفيش API خارجي مربوط.
9. **`appsettings.Staging.json`** منفصل بوضوح لو محتاج بيئة staging مميزة عن production.

### تحسين (بعد الاستقرار)
10. **نظام صلاحيات مخصص لكل مستخدم** — البنية التحتية (catalog + policy-based auth) جاهزة، لكن لسه 3 أدوار ثابتة بس، مفيش تخصيص فردي.
11. **Mutation testing / Contract tests** بين الـ frontend والـ backend — لسه ناقص بالكامل.
12. **تشغيل k6 load test فعليًا** على بيئة شبيهة بالإنتاج، مش لوكال بس.
13. **APM حقيقي** (Application Insights أو Prometheus+Grafana مربوطين فعليًا، مش بس الـ endpoint موجود).

---

## ملاحظة أخيرة

المشروع اتقدم بشكل ملموس من أول مراجعة في 5 أغسطس لحد 27 أغسطس — Clean Architecture، multi-tenant isolation بطبقتين، 2FA، permissions، audit log، PDF receipts، barcode lookup، stock ledger، i18n + dark mode، Prometheus metrics، وk6 load test، كلها موجودة وشغالة وبتعدي 73 اختبار backend + 36 frontend + E2E specs. الفجوة الحقيقية المتبقية مش في الكود نفسه، لكن في **قرارات تشغيلية محتاجة صاحب المشروع**: اختيار استضافة فعلية للـ CD، تفعيل الخدمات السحابية (Key Vault، SMTP حقيقي)، جدولة الـ backup، ومعالجة الثغرة الأمنية الحالية في تبعية SQLite بمشروع الاختبارات.
