# PosFlow — وثيقة تسليم واستكمال المشروع لأي مساعد ذكاء اصطناعي

**نوع الوثيقة:** Project Context + Product Specification + Technical Handoff  
**آخر تحديث:** 11 يوليو 2026  
**اللغة الأساسية:** العربية، مع أسماء تقنية وكود باللغة الإنجليزية  
**المالك:** صاحب مشروع PosFlow

> الهدف من هذه الوثيقة أن يستطيع أي مساعد ذكاء اصطناعي أو مطور جديد فهم المشروع، حالته الحالية، القرارات التي تم اتخاذها، والخطة المستقبلية، ثم يكمل من آخر نقطة بدون إعادة اختراع المعمارية أو تغيير الـStack.

## طريقة استخدام هذه الوثيقة مع أي AI

أرسل الملف إلى المساعد الجديد، ثم استخدم الرسالة التالية:

```text
اقرأ ملف PosFlow_AI_Handoff_AR.md بالكامل قبل اقتراح أي كود.
التزم بالـStack والقرارات المعمارية المكتوبة فيه.
ابدأ من قسم "الحالة الحالية وآخر نقطة وصلنا إليها".
لا تفترض أن خطوة غير معلّم عليها كمكتملة قد تم تنفيذها.
اطلب الملفات الضرورية فقط، ثم اعطني خطوة واحدة قابلة للتنفيذ في كل مرة، مع مسار كل ملف والكود كاملًا عند الحاجة.
أنا أستخدم Visual Studio وPackage Manager Console لأوامر NuGet وEF Core، وTerminal لأوامر Node وAngular.
```

---

# 1. ملخص المشروع

**PosFlow** منصة SaaS متعددة العملاء والفروع لإدارة نقاط البيع وتشغيل المطاعم والكافيهات والريتيل. الرؤية طويلة المدى هي جمع أهم القدرات الموجودة في ثلاث فئات من الأنظمة:

| مصدر الإلهام | القدرات المستهدفة |
|---|---|
| Odoo | المنتجات، المخزون، المشتريات، الحسابات، العملاء، الصلاحيات، التقارير، والتكاملات |
| Foodics | تجربة POS للمطاعم، الطاولات، المطبخ KDS، الإضافات والوجبات، الفروع، الولاء والدليفري |
| Geidea | تكامل أجهزة الدفع، المدفوعات الإلكترونية، الاسترداد، الإلغاء، التسويات وروابط الدفع |

المشروع لا يهدف لنسخ أي منتج حرفيًا. الهدف بناء منتج مستقل بواجهة وتجربة ومعمارية خاصة به، مع الاستفادة من الوظائف المطلوبة في السوق.

## الشريحة المستهدفة أولًا

- مطاعم وكافيهات صغيرة ومتوسطة.
- نشاط له فرع واحد أو عدة فروع.
- السوق المصري في البداية.
- قابل للتوسع لاحقًا للريتيل والسلاسل الأكبر.

## القيمة الأساسية للمنتج

- شاشة بيع سريعة وبسيطة.
- تشغيل المطعم من الطلب إلى المطبخ ثم الدفع.
- إدارة مركزية للفروع والمنتجات والمستخدمين.
- مخزون وتكلفة وربحية لاحقًا.
- إمكانية العمل عند ضعف أو انقطاع الإنترنت في مرحلة لاحقة.
- تكامل قانوني وآمن مع المدفوعات والإيصال الإلكتروني المصري.

---

# 2. الـStack الثابت للمشروع

هذه اختيارات مؤكدة، ولا يتم تغييرها إلا بطلب صريح من مالك المشروع:

| الجزء | التقنية |
|---|---|
| Backend | C# + ASP.NET Core Web API |
| Frontend | Angular + TypeScript + SCSS |
| Database | SQL Server |
| ORM | Entity Framework Core |
| IDE | Visual Studio |
| API Documentation | Swagger/OpenAPI — يجب اختيار تنفيذ متوافق واحد وعدم خلط الحزم |
| Real-time مستقبلًا | SignalR |
| Background Jobs مستقبلًا | Hangfire أو بديل متوافق مع .NET |
| Local POS Storage مستقبلًا | IndexedDB داخل Angular |
| Hardware Bridge مستقبلًا | C# Device Agent على جهاز الكاشير |

## سياسة الإصدارات

- يجب تثبيت إصدارات متوافقة بين .NET وEF Core وSwagger/OpenAPI.
- لا يتم ترقية Package بشكل عشوائي أثناء حل خطأ.
- قبل إضافة أو تحديث Package، يراجع المساعد ملفات `.csproj` والإصدار المستهدف في `TargetFramework`.
- يجب حفظ كل الإصدارات داخل ملفات المشروع وعدم الاعتماد على إصدار عالمي غير موثق.

---

# 3. المعمارية المعتمدة

سنبدأ بنمط **Modular Monolith + Clean Architecture**. لا نبدأ بـMicroservices في الـMVP.

```text
Angular Applications
        |
        | HTTPS / JSON
        v
PosFlow.Api
        |
        v
PosFlow.Application
        |
        v
PosFlow.Domain
        ^
        |
PosFlow.Infrastructure ---> SQL Server / External Providers
```

## مسؤولية كل مشروع

| المشروع | المسؤولية |
|---|---|
| `PosFlow.Domain` | Entities، Enums، Value Objects، Domain Rules، Domain Events. لا يعتمد على باقي المشاريع |
| `PosFlow.Application` | Use Cases، DTOs، Validators، Interfaces، Commands/Queries، قواعد التطبيق |
| `PosFlow.Infrastructure` | EF Core، SQL Server، Repositories عند الحاجة، Identity، integrations، background jobs |
| `PosFlow.Api` | Controllers/Endpoints، Middleware، Authentication setup، DI، Swagger، HTTP concerns |
| `posflow-web` | واجهة Angular للكاشير ولوحة الإدارة، ثم KDS وتطبيقات أخرى لاحقًا |

## قواعد الاعتماد

```text
Domain          -> لا يعتمد على أي Project آخر
Application     -> يعتمد على Domain
Infrastructure  -> يعتمد على Application وDomain
Api             -> يعتمد على Application وInfrastructure
Angular         -> يتعامل مع Api فقط
```

## قرار مهم

لا يوضع منطق الأعمال داخل Controller أو Angular. الحسابات النهائية، الصلاحيات، والتحقق التجاري تتم في الـBackend.

---

# 4. الهيكل المستهدف للمستودع

```text
PosFlow/
├── PosFlow.sln
├── src/
│   ├── PosFlow.Api/
│   ├── PosFlow.Application/
│   ├── PosFlow.Domain/
│   └── PosFlow.Infrastructure/
├── tests/
│   ├── PosFlow.Domain.Tests/
│   ├── PosFlow.Application.Tests/
│   └── PosFlow.Api.IntegrationTests/
├── frontend/
│   └── posflow-web/
├── docs/
│   └── PosFlow_AI_Handoff_AR.md
└── README.md
```

في المرحلة الحالية قد لا تكون مجلدات `tests` و`frontend` و`docs` قد أُنشئت بعد.

---

# 5. الحالة الحالية وآخر نقطة وصلنا إليها

هذا القسم هو نقطة البداية لأي AI جديد.

## مؤكد من المحادثة والصور

- [x] تم إنشاء Solution باسم `PosFlow`.
- [x] الـSolution يحتوي على أربعة Projects:
  - `PosFlow.Api`
  - `PosFlow.Application`
  - `PosFlow.Domain`
  - `PosFlow.Infrastructure`
- [x] تم بدء إنشاء مجلد `Entities` داخل `PosFlow.Domain`.
- [x] ظهرت ملفات لكيانات مثل `Tenant`, `Product`, `Shift`, `OrderLine`, `Payment`، ويرجح وجود `Branch`, `Order`, `BaseEntity` كذلك.
- [x] يوجد `PosFlowDbContext.cs` داخل `PosFlow.Infrastructure/Persistence`.
- [x] `Program.cs` في `PosFlow.Api` يحتوي على تسجيل Controllers وDbContext وSwagger.
- [x] المستخدم يفضل العمل داخل Visual Studio، مع **Package Manager Console** لأوامر NuGet وEF Core.
- [x] تم تجربة `node -v` وظهر أن Node.js غير معروف للنظام.

## مشاكل ظهرت ولم يتم تأكيد حلها

- [ ] تعارض Packages بين Swagger/OpenAPI أدى إلى أخطاء داخل ملف مولّد اسمه قريب من `OpenApiXmlCommentSupport.generated.cs`.
- [ ] لم يتم تأكيد أن `Build` أصبح ناجحًا بعد حل التعارض.
- [ ] لم يتم تأكيد نجاح `Add-Migration InitialCreate`.
- [ ] لم يتم تأكيد نجاح `Update-Database` أو إنشاء قاعدة `PosFlowDb`.
- [ ] لم يتم تأكيد إنشاء مشروع Angular.
- [ ] Node.js غير مثبت أو غير مضاف إلى `PATH` بحسب آخر رسالة.

## أول خطوة عند استكمال العمل

يجب على المساعد الجديد أن يطلب هذه الملفات أو صورها قبل تنفيذ تغييرات كبيرة:

```text
src/PosFlow.Api/PosFlow.Api.csproj
src/PosFlow.Infrastructure/PosFlow.Infrastructure.csproj
src/PosFlow.Api/Program.cs
src/PosFlow.Api/appsettings.json أو appsettings.Development.json
src/PosFlow.Infrastructure/Persistence/PosFlowDbContext.cs
قائمة Packages الحالية أو نتيجة Get-Package
```

ثم ينفذ بالترتيب:

1. توحيد Packages وحل Build.
2. التأكد من Connection String.
3. إنشاء Migration وقاعدة البيانات.
4. بناء أول API للمنتجات أو الورديات.
5. تثبيت Node.js LTS ثم إنشاء Angular، بعد استقرار الـBackend الأساسي.

---

# 6. الكيانات الحالية المتوقعة

> هذه القائمة مبنية على الكود الذي تم اقتراحه ولقطات Visual Studio. يجب مقارنة التعريفات بالملفات الفعلية قبل تعديل قاعدة البيانات.

| Entity | الغرض | أهم الحقول المتوقعة |
|---|---|---|
| `BaseEntity` | خصائص مشتركة | `Id`, `CreatedAtUtc`, `UpdatedAtUtc`, `RowVersion` |
| `Tenant` | الشركة العميلة داخل SaaS | `Name`, `IsActive`, `Branches` |
| `Branch` | فرع تابع لشركة | `TenantId`, `Name`, `Code`, `IsActive` |
| `Product` | صنف يباع | `TenantId`, `NameAr`, `NameEn`, `Barcode`, `Price`, `IsActive` |
| `Shift` | وردية الكاشير | `TenantId`, `BranchId`, `UserId`, `OpeningCash`, `ClosingCash`, timestamps, status |
| `Order` | طلب/فاتورة بيع | tenant/branch/shift, number, status, subtotal, discount, tax, total |
| `OrderLine` | صنف داخل طلب | product snapshot, quantity, unit price, discount, tax, line total |
| `Payment` | دفعة على الطلب | method, amount, reference number |

## ملاحظات ونواقص متوقعة

- يوجد `UserId` في `Shift` لكن Entity المستخدم ونظام الهوية لم يتم تأكيدهما بعد.
- لا يجب الاعتماد على `Product.Price` فقط على المدى الطويل؛ ستوجد Price Lists وBranch Pricing.
- `OrderLine` يجب أن يحفظ Snapshot لاسم وسعر وضريبة الصنف لحماية الفاتورة التاريخية.
- رقم الطلب يجب أن يولده السيرفر وفق الفرع والجهاز/الوردية، وليس Angular.
- كل مبلغ يستخدم `decimal` في C# و`DECIMAL(19,4)` في SQL Server.

---

# 7. قواعد قاعدة البيانات

## قواعد عامة

- المفتاح الأساسي: `uniqueidentifier` / `Guid`.
- الوقت: UTC باستخدام `datetime2`.
- الأموال: `decimal(19,4)`، ممنوع `float` و`double`.
- التزامن: `rowversion` للكيانات القابلة للتعديل المتزامن.
- كل سجل تشغيلي يجب أن يحمل `TenantId`، و`BranchId` عند ارتباطه بفرع.
- لا تثق في `TenantId` أو `BranchId` القادم من Body؛ يستخرج من هوية المستخدم والسياق المصرح له.
- لا تستخدم Cascade Delete في البيانات المالية الحساسة بدون قرار واعٍ.
- الطلبات والمدفوعات والحركات المالية لا يتم حذفها فعليًا؛ تستخدم حالات Cancel/Void/Refund وسجل تدقيق.

## الـSchema الحالي

الكود الأولي استخدم Default Schema باسم:

```text
pos
```

يمكن الإبقاء عليه في الـMVP. بعد استقرار الموديولات يمكن تقسيم الجداول إلى Schemas مثل:

```text
auth, catalog, sales, restaurant, inventory, purchasing, payments, loyalty, accounting, integration, audit
```

لا يتم تنفيذ التقسيم الآن إذا كان سيعطل أول نسخة.

## عزل العملاء Multi-Tenancy

المرحلة الأولى تستخدم **Shared Database + Shared Schema + TenantId**.

شروط الأمان:

- إنشاء خدمة `ICurrentTenant` أو `ITenantContext`.
- تطبيق Global Query Filters بحذر، مع Integration Tests.
- كل Command/Query يتحقق من أن السجل تابع للـTenant الحالي.
- يمنع أي Endpoint من قراءة أو تعديل سجل Tenant آخر حتى لو تم تخمين الـGuid.

---

# 8. هدف الـMVP الأول

رحلة العمل الأساسية:

```text
Login
→ Open Shift
→ View/Search Products
→ Add Items to Cart
→ Create Order
→ Cash Payment
→ Complete Order
→ Print/View Receipt
→ Close Shift
```

## داخل نطاق الـMVP

- شركة `Tenant` وفرع واحد على الأقل.
- مستخدم أو كاشير وصلاحيات أساسية.
- منتجات وتصنيفات بسيطة.
- فتح وردية بمبلغ افتتاحي.
- إنشاء طلب وإضافة أصناف وكميات.
- حساب Subtotal وDiscount وTax وTotal في السيرفر.
- دفع نقدي كامل.
- إتمام الطلب.
- إغلاق الوردية وإظهار المتوقع والفعلي والفرق.
- Swagger يعمل.
- قاعدة البيانات تعمل مع Migration.
- واجهة Angular أساسية بعد استقرار APIs.

## خارج نطاق أول MVP

- Offline Mode الكامل.
- Geidea integration الحقيقي.
- الإيصال الإلكتروني المصري.
- مخزون بالوصفات.
- محاسبة كاملة.
- ولاء وكوبونات متقدمة.
- KDS متقدم.
- تطبيقات توصيل متعددة.
- Microservices.

## Definition of Done للـMVP

- [ ] Build ناجح بدون Errors.
- [ ] Migration قابلة للتطبيق من قاعدة جديدة.
- [ ] Swagger يفتح ويعرض Endpoints.
- [ ] لا يمكن فتح أكثر من وردية نشطة لنفس المستخدم/الجهاز وفق القاعدة المعتمدة.
- [ ] لا يمكن إنشاء Order بدون وردية مفتوحة.
- [ ] السعر والإجمالي يحسبهما السيرفر.
- [ ] لا يمكن دفع مبلغ سالب أو إكمال طلب غير مدفوع.
- [ ] يتم منع الوصول بين Tenants.
- [ ] اختبارات رحلة البيع الأساسية ناجحة.

---

# 9. الـAPI المستهدف للـMVP

كل Endpoints تحت Version واضح:

```text
/api/v1
```

## Authentication

```http
POST /api/v1/auth/login
POST /api/v1/auth/refresh
GET  /api/v1/auth/me
```

## Products

```http
GET    /api/v1/products?search=&page=1&pageSize=20
GET    /api/v1/products/{id}
POST   /api/v1/products
PUT    /api/v1/products/{id}
PATCH  /api/v1/products/{id}/status
```

## Shifts

```http
GET  /api/v1/shifts/current
POST /api/v1/shifts/open
POST /api/v1/shifts/{id}/cash-movements
POST /api/v1/shifts/{id}/close
```

## Orders

```http
POST /api/v1/orders
GET  /api/v1/orders/{id}
GET  /api/v1/orders?from=&to=&status=&page=1&pageSize=20
POST /api/v1/orders/{id}/lines
PUT  /api/v1/orders/{id}/lines/{lineId}
DELETE /api/v1/orders/{id}/lines/{lineId}
POST /api/v1/orders/{id}/payments
POST /api/v1/orders/{id}/complete
POST /api/v1/orders/{id}/cancel
```

## قواعد الـAPI

- Controllers خفيفة، ولا تحتوي على Business Logic كبير.
- استخدام DTOs؛ ممنوع إرجاع EF Entities مباشرة.
- Validation برسائل واضحة.
- الأخطاء بصيغة `ProblemDetails`.
- كل Endpoint محمي بسياسة Authorization مناسبة.
- عمليات الإنشاء الحساسة تدعم `Idempotency-Key` لاحقًا.
- Pagination لأي قائمة يمكن أن تكبر.
- التواريخ ترجع ISO 8601 UTC.

---

# 10. قواعد منطق البيع

هذه القواعد مهمة ولا يجب أن تكون في Angular فقط:

1. السيرفر يجلب السعر الفعلي من قاعدة البيانات.
2. السيرفر يعيد حساب كل Line Total والإجماليات.
3. لا يقبل سعرًا نهائيًا موثوقًا من العميل.
4. كمية الصنف يجب أن تكون أكبر من صفر، إلا في مستندات المرتجع المصممة لذلك.
5. الطلب المكتمل لا يعدل مباشرة؛ يستخدم Cancel/Refund وفق الصلاحيات.
6. مجموع المدفوعات يجب أن يطابق المطلوب وفق سياسة التقريب.
7. الدفع النقدي يمكن أن يحتوي على `TenderedAmount` و`ChangeAmount` مستقبلًا.
8. الضرائب والخصومات تسجل كقيم Snapshot على الطلب والسطر.
9. أي Discount كبير أو Cancel قد يحتاج Manager PIN مستقبلًا.
10. إتمام الطلب وتسجيل الدفع يجب أن يتم داخل Transaction مناسبة.

---

# 11. Angular — الخطة والبنية

Angular لم يتم تأكيد إنشائه حتى آخر حالة. Node.js يجب تثبيته أولًا.

## الفرق بين الـConsoles

| المهمة | المكان الصحيح |
|---|---|
| `Install-Package`, `Add-Migration`, `Update-Database` | Package Manager Console |
| `node`, `npm`, `ng` | Terminal أو PowerShell، وليس NuGet Console |

## الهيكل المستهدف

```text
frontend/posflow-web/src/app/
├── core/
│   ├── auth/
│   ├── guards/
│   ├── interceptors/
│   ├── services/
│   └── models/
├── features/
│   ├── login/
│   ├── shifts/
│   ├── products/
│   ├── checkout/
│   └── orders/
└── shared/
    ├── components/
    ├── directives/
    └── pipes/
```

## أول صفحات

- Login.
- Open Shift.
- POS Checkout.
- Product Management البسيطة.
- Order Details.
- Close Shift.

## قواعد الواجهة

- دعم العربية وRTL من البداية.
- تصميم Touch-friendly للكاشير.
- أزرار وأرقام واضحة.
- منع الضغط المتكرر أثناء حفظ الطلب.
- إظهار حالة الاتصال والأخطاء بوضوح.
- عدم تنفيذ الحساب التجاري النهائي في الواجهة.
- API base URL من Environment، وليس hard-coded داخل Components.

---

# 12. الخطة المستقبلية بالتفصيل

## المرحلة 0 — Foundation وإصلاح البيئة

**الهدف:** مشروع يبني ويعمل وقاعدة بيانات قابلة للإنشاء.

- حل تعارض Swagger/OpenAPI.
- تثبيت Packages المتوافقة.
- Connection String آمنة.
- Migration أولى.
- Seed اختياري لTenant/Branch/Products.
- Logging أساسي.
- Health Check.

## المرحلة 1 — Online POS MVP

**الهدف:** أول رحلة بيع كاملة Online.

- Identity/Login.
- Tenant/Branch context.
- Products/Categories.
- Shifts.
- Orders/Order Lines.
- Cash Payment.
- Complete/Cancel Order.
- Daily Sales Summary.
- Angular POS أولي.

## المرحلة 2 — Restaurant Operations

**الهدف:** الاقتراب من قدرات تشغيل المطاعم.

- Dining Areas وTables.
- Dine-in / Takeaway / Delivery order types.
- Modifiers وOptions.
- Combos/Meal Deals.
- Course/Seat notes عند الحاجة.
- Split Bill وMerge/Transfer Table.
- Kitchen Tickets.
- KDS باستخدام SignalR.
- Kitchen Stations وطابعات مختلفة.
- حالة: New → Preparing → Ready → Served.

## المرحلة 3 — Inventory and Purchasing

**الهدف:** قدرات مستوحاة من ERP بدون بناء Odoo كاملًا.

- Warehouses.
- Stock Ledger / Stock Movements.
- Transfers.
- Stock Counts.
- Waste.
- Suppliers.
- Purchase Orders.
- Goods Receipts.
- Reorder Levels.
- Recipes وIngredients.
- خصم مكونات الوصفة عند البيع.
- Theoretical vs Actual Consumption.
- Cost of Goods Sold.

## المرحلة 4 — Offline-first وHardware

**الهدف:** استمرار البيع عند انقطاع الإنترنت.

- IndexedDB للمنتجات والأسعار والطلبات المحلية.
- Sync Queue.
- UUID ثابت لكل عملية.
- Idempotency.
- Retry وConflict Strategy.
- C# Device Agent للتعامل مع:
  - Thermal Printer
  - Cash Drawer
  - Barcode Scanner عند الحاجة
  - Scale
  - Customer Display
  - Payment Terminal
- Remote device registration/logout.

## المرحلة 5 — Payments Layer

**الهدف:** قدرات دفع شبيهة بالفئة التي تقدمها Geidea بدون ربط النظام بمزود واحد.

```text
IPaymentProvider
├── GeideaPaymentProvider
├── BankTerminalProvider
├── WalletProvider
├── QRProvider
└── MockPaymentProvider for testing
```

الوظائف:

- Initiate.
- Status Inquiry.
- Capture عند الحاجة.
- Void.
- Full/Partial Refund.
- Payment Link.
- Webhook verification.
- Settlement reconciliation.

**قاعدة أمنية:** لا يتم تخزين PAN كامل أو CVV أو PIN أو Track Data داخل PosFlow أو الـLogs.

## المرحلة 6 — Egypt Tax and E-Receipt

**الهدف:** دعم الالتزامات المصرية عبر Adapter مستقل.

- تسجيل/ربط جهاز POS وفق المتطلبات.
- Mapping للأكواد والضرائب.
- إنشاء UUID وQR.
- إرسال Receipt وReturn Receipt.
- حالات Queued / Submitted / Accepted / Rejected.
- حفظ أسباب الرفض ومحاولات الإرسال.
- Retry آمن.
- Queue عند الانقطاع وفق المسموح.
- Audit كامل.

التنفيذ النهائي يحتاج مراجعة المتطلبات الرسمية وقت التنفيذ ومستشار تكامل/ضرائب.

## المرحلة 7 — CRM, Loyalty and Omnichannel

- Customer Profiles.
- Addresses.
- Points and Tiers.
- Coupons.
- Gift Cards.
- Promotions Engine.
- QR Menu and Ordering.
- Online Ordering.
- Delivery Integrations.
- E-commerce connectors.

## المرحلة 8 — Accounting and ERP Expansion

- Double-entry accounting.
- Chart of Accounts.
- Automatic journal entries from sales, payments, purchases and stock.
- Cash/Bank accounts.
- Expenses.
- Accounts Receivable/Payable.
- Bank reconciliation.
- P&L and Balance Sheet.
- Multi-company and consolidated reporting لاحقًا.

## المرحلة 9 — Platform and Marketplace

- Public API.
- Webhooks.
- Developer portal.
- Integration marketplace.
- Plugin permissions.
- Advanced analytics.
- Demand forecasting بعد توفر بيانات كافية.
- Enterprise controls and observability.

---

# 13. الموديولات والكيانات المستقبلية

| Module | كيانات مستقبلية مهمة |
|---|---|
| Identity | User, Role, Permission, UserBranch, RefreshToken, Device |
| Catalog | Category, ProductVariant, UnitOfMeasure, PriceList, PriceListItem, Tax, Modifier, Combo |
| Sales | Order, OrderLine, Discount, Payment, Refund, Receipt, Shift, CashMovement |
| Restaurant | DiningArea, Table, KitchenStation, KitchenTicket, KitchenTicketLine |
| Inventory | Warehouse, StockItem, StockMovement, StockCount, WasteTransaction, Recipe, RecipeItem |
| Purchasing | Supplier, PurchaseOrder, PurchaseOrderLine, GoodsReceipt |
| Loyalty | Customer, LoyaltyAccount, LoyaltyTransaction, Coupon, GiftCard |
| Payments | PaymentIntent, ProviderTransaction, Refund, Settlement, WebhookEvent |
| Tax | TaxDocument, TaxSubmissionAttempt, DeviceRegistration |
| Accounting | Account, Journal, JournalEntry, JournalLine, FiscalPeriod |
| Audit | AuditLog, SecurityEvent, IntegrationLog |

لا يتم إنشاء كل هذه الجداول الآن. تضاف عندما تصل المرحلة الخاصة بها.

---

# 14. الأمان والصلاحيات

- ASP.NET Core Identity أو تصميم آمن مكافئ للمستخدمين.
- Password hashing قياسي، ممنوع تخزين Password نصيًا.
- JWT قصير العمر + Refresh Tokens آمنة.
- MFA للمديرين مستقبلًا.
- Roles وPermissions على مستوى Tenant/Branch.
- Manager override للخصومات والإلغاء والمرتجع.
- حماية من IDOR/BOLA: التحقق من ملكية كل سجل.
- Rate Limiting للـLogin وEndpoints الحساسة.
- Audit Log للعمليات المالية والإدارية.
- Secrets داخل User Secrets/Environment/Secret Store، وليس Git.
- Logging بدون كلمات مرور أو Tokens أو بيانات بطاقات.
- HTTPS فقط في الإنتاج.
- Webhooks تتحقق من signature وتمنع replay.

---

# 15. الاختبارات المطلوبة

## Unit Tests

- حساب Order totals.
- الخصومات والضرائب والتقريب.
- حالات الطلب المسموحة.
- قواعد فتح/إغلاق الوردية.
- حالات Refund/Void مستقبلًا.

## Integration Tests

- إنشاء طلب وحفظه في SQL Server test environment.
- منع الوصول بين Tenant A وTenant B.
- Concurrency باستخدام RowVersion.
- Transaction عند الدفع والإكمال.
- Validation وProblemDetails.

## End-to-End لاحقًا

- Login → Open Shift → Sale → Payment → Complete → Close Shift.
- Kitchen flow.
- Offline sync.
- Payment provider mock.

---

# 16. Logging, Monitoring and Audit

- Application Logs منظمة باستخدام Correlation ID.
- لا تعتمد على Logs بدل Audit Trail.
- `AuditLog` يسجل: من، ماذا، متى، الفرع، الجهاز، القيمة القديمة والجديدة عند الحاجة.
- المدفوعات والتكاملات تحفظ Provider Reference بدون بيانات بطاقة حساسة.
- Health Checks للـAPI وSQL Server.
- Background jobs لها retries محدودة وDead-letter/failed view.
- لاحقًا Metrics: requests, failures, order latency, sync queue, payment failures, tax rejections.

---

# 17. اتفاقيات الكود

## C#

- Nullable Reference Types مفعلة.
- `async/await` لعمليات I/O.
- CancellationToken في Application وInfrastructure حيث يناسب.
- لا تستخدم Generic Repository لمجرد وجوده؛ EF Core نفسه Unit of Work/Repository عمليًا.
- Use Cases صغيرة وواضحة.
- DTOs منفصلة عن Entities.
- Enums لها قيم صريحة عند تخزينها.
- أسماء Domain بالإنجليزية، وواجهات المستخدم تدعم العربية.
- لا تضع Provider-specific code داخل Domain.

## Angular

- Standalone Components.
- Feature-based structure.
- Typed models.
- Interceptor للمصادقة والأخطاء.
- Guards للصفحات.
- Signals للحالة المحلية البسيطة وRxJS للتدفقات/HTTP حسب الحاجة.
- Components لا تحتوي على API orchestration ضخم؛ تستخدم Services/Facades.

## Git

Branches مقترحة:

```text
main
 develop
 feature/shift-management
 feature/order-checkout
 fix/swagger-packages
```

Commits صغيرة وواضحة، مثال:

```text
feat(shifts): add open shift endpoint
fix(api): align OpenAPI package versions
```

---

# 18. تعليمات إلزامية لأي AI يكمل المشروع

1. لا يغير C#, Angular, SQL Server.
2. لا يحول المشروع إلى Microservices في البداية.
3. يراجع الملفات الفعلية قبل كتابة Migration أو Package commands.
4. يفرق بين Package Manager Console وTerminal.
5. يعطي مسار الملف قبل الكود.
6. عند تعديل ملف، يفضل إرسال محتواه الكامل إذا كان صغيرًا.
7. لا يقفز لموديول جديد قبل نجاح Build وتشغيل الحالي.
8. ينفذ خطوة واحدة قابلة للتحقق، ثم ينتظر نتيجة المستخدم.
9. لا يفترض نجاح أمر لم يرسل المستخدم نتيجته.
10. يحافظ على Multi-Tenancy من البداية.
11. لا يثق في الحسابات أو السعر القادم من Angular.
12. لا يخزن بيانات بطاقات حساسة.
13. لا ينشئ عشرات Abstractions بلا حاجة.
14. يكتب Tests للقواعد المالية وعزل الـTenant.
15. بعد تغيير Entities/Mapping، يوضح هل نحتاج Migration.
16. عند خطأ Package، يطلب `.csproj` و`Get-Package` بدل التخمين.
17. لا يخلط بين Swashbuckle وOpenAPI المدمج بدون خطة واضحة.
18. يشرح السبب باختصار ثم يعطي الأمر العملي.

---

# 19. المهمة التالية المقترحة بدقة

## الهدف الفوري

الوصول إلى:

```text
Build Succeeded
→ Swagger opens
→ Initial Migration created
→ Database updated
```

## خطوات التنفيذ

1. قراءة `PosFlow.Api.csproj` و`TargetFramework`.
2. قراءة Packages المتعلقة بـ:
   - `Microsoft.AspNetCore.OpenApi`
   - `Microsoft.OpenApi`
   - `Swashbuckle.AspNetCore`
   - `Microsoft.EntityFrameworkCore.*`
3. اختيار Swagger implementation واحد متوافق.
4. Clean/Rebuild.
5. فحص `appsettings` وConnection String.
6. تنفيذ من Package Manager Console:

```powershell
Add-Migration InitialCreate `
  -Project PosFlow.Infrastructure `
  -StartupProject PosFlow.Api `
  -Context PosFlowDbContext `
  -OutputDir Persistence\Migrations
```

ثم:

```powershell
Update-Database `
  -Project PosFlow.Infrastructure `
  -StartupProject PosFlow.Api `
  -Context PosFlowDbContext
```

7. تشغيل API وفتح `/swagger`.
8. بعد نجاح ذلك، تنفيذ أول Feature: Products أو Shifts، ويفضل Products للاختبار السريع ثم Shifts.

## ما لا يجب فعله الآن

- لا تبدأ Angular قبل تثبيت Node.js والتأكد من الـAPI، إلا إذا قرر المالك العمل بالتوازي.
- لا تضف Redis/RabbitMQ/Kafka.
- لا تضف Geidea أو ETA الآن.
- لا تنشئ Accounting module.

---

# 20. أسئلة مفتوحة يجب حسمها تدريجيًا

هذه الأسئلة لا تمنع بدء الـMVP، لكن يجب تسجيل القرارات عند حسمها:

- إصدار .NET وAngular النهائي المثبت في المشروع.
- استخدام ASP.NET Core Identity أو Identity provider خارجي.
- قاعدة فتح الوردية: لكل User، أم User+Device، أم Terminal.
- طريقة Tax configuration في أول نسخة.
- هل الخصم قبل الضريبة أم بعدها حسب الإعداد.
- سياسة التقريب.
- صيغة أرقام الطلبات والفواتير.
- هل Product price عام أم لكل Branch من أول MVP.
- هل المستخدم يمكنه العمل على أكثر من Branch.
- طريقة الطباعة الأولى: Browser print أم Device Agent مبكرًا.
- اختيار Cloud/Hosting والإصدارات الإنتاجية.
- نموذج التسعير التجاري للـSaaS.

عند اتخاذ أي قرار، يضاف إلى قسم Decision Log.

---

# 21. Decision Log

| التاريخ | القرار | السبب |
|---|---|---|
| 2026-07-11 | Backend باستخدام C# ASP.NET Core Web API | اختيار صاحب المشروع |
| 2026-07-11 | Frontend باستخدام Angular | اختيار صاحب المشروع |
| 2026-07-11 | SQL Server + EF Core | اختيار صاحب المشروع |
| 2026-07-11 | Modular Monolith + Clean Architecture | أسرع وأبسط للـMVP مع قابلية التوسع |
| 2026-07-11 | Multi-Tenant من البداية | المنتج SaaS متعدد الشركات والفروع |
| 2026-07-11 | تأجيل Microservices | تجنب التعقيد المبكر |
| 2026-07-11 | تأجيل Offline/Payments/Tax لما بعد البيع الأساسي | تثبيت الرحلة الأساسية أولًا |
| 2026-07-11 | Package Manager Console لـNuGet/EF وTerminal لـAngular | متوافق مع طريقة عمل المستخدم |

---

# 22. ملخص سريع يمكن وضعه في بداية أي محادثة

```text
أنا أبني مشروع PosFlow، وهو SaaS POS للمطاعم والريتيل في مصر، مستوحى وظيفيًا من Odoo وFoodics وGeidea.
الـStack ثابت: C# ASP.NET Core Web API + Angular + SQL Server + EF Core.
المعمارية: Modular Monolith + Clean Architecture، مع Projects: Api, Application, Domain, Infrastructure.
تم إنشاء الـSolution والكيانات الأولية وDbContext وProgram.cs، لكن آخر حالة فيها مشكلة توافق Swagger/OpenAPI، ولم يتم تأكيد Migration أو قاعدة البيانات. Node.js غير مثبت/غير معروف، لذلك Angular لم يتم تأكيد إنشائه.
الهدف الحالي: Build ناجح، Swagger يعمل، Initial Migration وUpdate-Database، ثم Products وShifts وOrders للوصول إلى رحلة Login → Open Shift → Sale → Cash Payment → Complete → Close Shift.
لا تغيّر الـStack، لا تبدأ Microservices، لا تثق في أسعار Angular، حافظ على Tenant isolation، واعطني خطوة واحدة قابلة للتنفيذ في كل مرة.
```

---

# 23. Checklist قبل تسليم كل Sprint

- [ ] الكود يبني محليًا.
- [ ] Database migration مضافة ومختبرة عند الحاجة.
- [ ] Swagger/API contract محدث.
- [ ] Validation ورسائل الخطأ موجودة.
- [ ] Authorization وTenant isolation مراجعان.
- [ ] لا توجد Secrets داخل Git.
- [ ] لا توجد بيانات بطاقة أو Token حساسة في Logs.
- [ ] Tests للقواعد الجديدة.
- [ ] README وملف الـHandoff محدثان.
- [ ] تم تسجيل أي قرار معماري جديد في Decision Log.

---

# 24. تعريف النجاح طويل المدى

يعتبر PosFlow وصل للرؤية النهائية عندما يستطيع عميل متعدد الفروع تشغيل نشاطه من منصة واحدة تشمل:

- POS سريع Online وOffline.
- مطاعم وطاولات ومطبخ ودليفري.
- منتجات وأسعار وعروض.
- مخزون ومشتريات ووصفات وتكلفة.
- مدفوعات وتحصيل وتسويات آمنة.
- التزام ضريبي وإيصال إلكتروني.
- عملاء وولاء وقنوات طلب متعددة.
- حسابات وتقارير وربحية.
- APIs وMarketplace للتكاملات.

يتم الوصول لهذه الرؤية تدريجيًا. الأولوية دائمًا للاستقرار، صحة الأموال والبيانات، سهولة الاستخدام، والأمان قبل كثرة المميزات.
