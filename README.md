# PosFlow

منصة SaaS متعددة العملاء (Tenants) والفروع لإدارة نقاط البيع، مستوحاة وظيفيًا من Odoo وFoodics وGeidea، وتستهدف المطاعم والكافيهات والريتيل في مصر أولًا.

**GitHub:** https://github.com/Mostafaessam7/POSFlow

---

## الـStack

| الجزء | التقنية |
|---|---|
| Backend | .NET 10 + ASP.NET Core Web API، Clean Architecture (`Domain` → `Application` → `Infrastructure` → `Api`) |
| Frontend | Angular 22 (Standalone Components, zoneless, بدون NgModules)، واجهة عربية RTL |
| Database | SQL Server + Entity Framework Core |
| Auth | JWT قصير العمر + Refresh Tokens متجددة + 2FA اختياري (TOTP) |
| Logging | Serilog (console + JSON files + request logging) |
| Monitoring | Prometheus metrics endpoint (`/metrics`) عبر `prometheus-net.AspNetCore` |
| Tests | xUnit (backend, unit + integration) + Vitest (frontend) + Playwright (E2E) + k6 (load test) |
| CI/CD | GitHub Actions — build, test, vulnerability scan, Docker build، ونشر الـ images على GHCR (مفيش نشر تلقائي لسيرفر فعلي بعد) |

## هيكل المستودع

```text
PosFlow/
├── src/
│   ├── PosFlow.Api/              Controllers, Middleware, DI, Swagger
│   ├── PosFlow.Application/      DTOs, Use Cases, Validators, Interfaces
│   ├── PosFlow.Domain/           Entities, Enums, Domain Rules
│   └── PosFlow.Infrastructure/   EF Core, Auth, Email, Seeders
├── tests/
│   ├── PosFlow.Application.Tests/   Unit tests (EF Core InMemory)
│   └── PosFlow.Api.Tests/           Integration tests (WebApplicationFactory)
├── posflow-web/                  تطبيق Angular (POS، Admin، Dashboard)
│   └── e2e/                      Playwright end-to-end tests
├── deploy/                       Runbook + سكريبت النسخ الاحتياطي
├── docs/adr/                     Architecture Decision Records
├── .github/workflows/            CI (build/test/scan) + Docker image publish
├── HANDOVER.md                   سجل تاريخي لما تم بناؤه ومتى (خلاصة لكل جلسة عمل)
├── ENTERPRISE-READINESS.md       تقييم دقيق لجاهزية enterprise — إيه اللي خلص فعليًا وإيه الناقص
└── CONTRIBUTING.md               قواعد الكود، الفروع، Multi-tenancy، الـMigrations
```

## الحالة الحالية (باختصار)

- **الكود بيتبني وبيعدي الاختبارات فعليًا** — تم التحقق مباشرة (27 أغسطس 2026): `dotnet build` نظيف، **73 اختبار backend** (41 unit + 32 integration، `dotnet test`) و**36 اختبار frontend** (`ng test`) كلهم ناجحين. فيه أيضًا 2 Playwright E2E specs و k6 load-test script (`tests/load/`).
- المميزات الأساسية شغالة end-to-end: تسجيل الدخول (+2FA اختياري TOTP)، الورديات، المنتجات والتصنيفات، طلبات البيع مع دفع مقسّم، الفويد/الاسترداد، طباعة فاتورة PDF (QuestPDF)، بحث بالباركود (server-side)، سجل حركة مخزون (Stock Movements)، لوحة تحكم المبيعات، إدارة المستخدمين والفروع والعملاء، نظام صلاحيات (Permissions) مرن، تدقيق (Audit Log) تلقائي على العمليات الحساسة، ضريبة قابلة للتعديل وعملة عرض (تحويل يدوي بأسعار يحددها الأدمن، مفيش ربط بـ API خارجي)، واجهة عربية/إنجليزية قابلة للتبديل + وضع ليلي (Dark Mode).
- عزل الـ Tenant محمي بطبقتين مستقلتين (فلتر يدوي + EF Core Global Query Filter)، مع اختبارات `TenantIsolationTests` تثبت إن الحماية شغالة حتى لو نسي أي service الفلتر اليدوي.
- **قفل الحساب بعد محاولات دخول فاشلة متكررة** (5 محاولات → قفل 15 دقيقة) — بالإضافة لـ rate limiting بالـ IP، دفاع ضد محاولات موزعة على حساب معين.
- مراقبة أساسية: Serilog (structured logging) + Prometheus `/metrics` — بدون Grafana/alerting جاهزين فعليًا، ده متروك لمن يستضيف النظام.
- التفاصيل الكاملة لكل ميزة، ولكل ما زال ناقصًا وليه: [`HANDOVER.md`](HANDOVER.md) (السجل التاريخي) و[`ENTERPRISE-READINESS.md`](ENTERPRISE-READINESS.md) (تقييم الجاهزية المحدّث).

## التشغيل محليًا

### Backend
```bash
dotnet restore PosFlow.slnx
dotnet build PosFlow.slnx
dotnet test PosFlow.slnx
dotnet run --project src/PosFlow.Api/PosFlow.Api.csproj
```
Swagger: `https://localhost:7178/swagger` (Development فقط).

### Frontend
```bash
cd posflow-web
npm install
npm test -- --watch=false
ng serve --proxy-config src/proxy.conf.json
```
`http://localhost:4200`

### عبر Docker
```bash
docker compose up
```
يشغّل الـ API + Angular (خلف nginx) + SQL Server معًا — راجع `docker-compose.yml`.

### الأسرار ومتغيرات البيئة
`appsettings.json` يشحن بدون أسرار حقيقية عن قصد. انسخ `.env.example` واملأ القيم الحقيقية (connection string، JWT key، SMTP) قبل أي تشغيل جاد.

## قبل أي استخدام حقيقي بفلوس فعلية

راجع قسم "خطة مقترحة بالأولوية" في [`ENTERPRISE-READINESS.md`](ENTERPRISE-READINESS.md) — البنود المتبقية (SMTP حقيقي، اختيار استضافة للـ CD، تفعيل Key Vault، جدولة الـ backup) محتاجة قرار أو حساب سحابي من صاحب المشروع، مش حاجة ممكن تتنفذ بالكود لوحده.

## المساهمة

راجع [`CONTRIBUTING.md`](CONTRIBUTING.md) قبل أي Pull Request — خصوصًا قسم Multi-tenancy، ده أخطر نوع باگ ممكن يحصل في نظام SaaS متعدد العملاء.
