# PosFlow — وثيقة الحالة الحالية وتسليم المشروع لأي AI أو مطور

**نوع الوثيقة:** Project Status + AI Handoff + Roadmap  
**آخر تحديث:** 11 يوليو 2026  
**اسم المشروع:** PosFlow  
**السوق الأولي:** مصر  
**المنتج المستهدف:** منصة POS وإدارة مطاعم/كافيهات/ريتيل متعددة العملاء والفروع

> الهدف من هذه الوثيقة هو أن يستطيع أي مساعد ذكاء اصطناعي أو مطور جديد معرفة ما تم تنفيذه بالفعل، ما هو غير مؤكد، آخر مشكلة وصل إليها المشروع، والخطوات التالية بالترتيب؛ بدون إعادة تصميم المشروع أو تغيير الـStack.

---

## 1. رسالة جاهزة لاستخدامها مع أي AI جديد

انسخ الرسالة التالية وأرفق معها هذا الملف:

```text
أنا أعمل على مشروع POS اسمه PosFlow.
اقرأ ملف PosFlow_Current_Status_AI_Handoff_AR.md بالكامل قبل اقتراح أي كود.
التزم بالـStack والقرارات المعمارية المكتوبة داخله.
ابدأ من قسم "آخر نقطة وصل إليها المشروع".
لا تفترض تنفيذ أي خطوة مكتوب أمامها "غير مؤكد" أو "قيد الاختبار".
أعطني خطوة واحدة قابلة للتنفيذ في كل مرة، مع مسار كل ملف والكود كاملًا عند الحاجة.
أنا أستخدم Visual Studio وPackage Manager Console لأوامر NuGet وEF Core، وTerminal لأوامر Node وAngular.
```

---

## 2. فكرة المنتج والرؤية

**PosFlow** مشروع لبناء منصة نقاط بيع متكاملة تستفيد من أهم القدرات الموجودة في:

| مصدر الإلهام | القدرات المستهدفة |
|---|---|
| Odoo | المنتجات، المخزون، المشتريات، الحسابات، العملاء، الصلاحيات، التقارير والتكاملات |
| Foodics | تشغيل المطاعم، الطاولات، المطبخ KDS، الإضافات والوجبات، الفروع، الولاء والدليفري |
| Geidea | تكامل أجهزة الدفع، الدفع الإلكتروني، الاسترداد، الإلغاء، التسويات وروابط الدفع |

المشروع لا يهدف إلى نسخ أي منتج حرفيًا. الهدف هو بناء منتج مستقل قابل للبيع، يبدأ بمطاعم وكافيهات مصر، ثم يتوسع إلى الريتيل والفروع والسلاسل الأكبر.

### القيمة الأساسية المستهدفة

- شاشة بيع سريعة وبسيطة.
- إدارة ورديات الكاشير وحركة النقدية.
- تشغيل الطلب من البيع إلى المطبخ ثم الدفع.
- إدارة المنتجات والفروع والمستخدمين مركزيًا.
- مخزون وتكلفة وربحية لاحقًا.
- Offline-first في مرحلة لاحقة.
- تكامل آمن مع المدفوعات والإيصال الإلكتروني المصري.

---

## 3. الـStack الثابت

| الجزء | التقنية |
|---|---|
| Backend | C# + ASP.NET Core Web API |
| Frontend | Angular + TypeScript + SCSS |
| Database | SQL Server |
| ORM | Entity Framework Core |
| Authentication | JWT Bearer Authentication |
| API Documentation | Swagger / OpenAPI |
| IDE | Visual Studio |
| NuGet وEF Commands | Package Manager Console |
| Angular Commands | Terminal / PowerShell باستخدام `npm`, `ng` أو `ng.cmd` |
| Real-time مستقبلًا | SignalR |
| Background Jobs مستقبلًا | Hangfire |
| Offline Storage مستقبلًا | IndexedDB |
| Hardware Integration مستقبلًا | C# POS Device Agent |

### قرار معماري ثابت

النسخة الحالية تستخدم:

```text
Modular Monolith + Clean Architecture
```

ولا يتم البدء بـMicroservices في الـMVP.

---

## 4. هيكل المشروع الحالي

```text
PosFlow/
├── PosFlow.sln
├── src/
│   ├── PosFlow.Api/
│   ├── PosFlow.Application/
│   ├── PosFlow.Domain/
│   └── PosFlow.Infrastructure/
└── posflow-web/
```

### مسؤولية كل Project

| المشروع | المسؤولية |
|---|---|
| `PosFlow.Domain` | Entities, Enums وقواعد الدومين الأساسية |
| `PosFlow.Application` | DTOs, Interfaces وUse Cases contracts |
| `PosFlow.Infrastructure` | EF Core, SQL Server, Authentication services, Seeders وخدمات التنفيذ |
| `PosFlow.Api` | Controllers, Dependency Injection, Middleware, JWT وSwagger |
| `posflow-web` | واجهة Angular للكاشير والصفحات الحالية |

### قواعد الاعتماد

```text
Domain          -> لا يعتمد على أي مشروع آخر
Application     -> يعتمد على Domain
Infrastructure  -> يعتمد على Application وDomain
Api             -> يعتمد على Application وInfrastructure
Angular         -> يتعامل مع API فقط
```

---

## 5. ما تم تنفيذه بالفعل

### 5.1 إنشاء الـSolution والمشروعات

- [x] إنشاء Solution باسم `PosFlow`.
- [x] إنشاء المشاريع:
  - `PosFlow.Api`
  - `PosFlow.Application`
  - `PosFlow.Domain`
  - `PosFlow.Infrastructure`
- [x] ربط Project References بصورة تسمح بتطبيق Clean Architecture.

### 5.2 قاعدة البيانات وEntity Framework Core

- [x] ربط SQL Server.
- [x] إنشاء `PosFlowDbContext` داخل:

```text
src/PosFlow.Infrastructure/Persistence/PosFlowDbContext.cs
```

- [x] إنشاء Migrations وتطبيقها.
- [x] قاعدة البيانات تعمل واتصال الـAPI بها ناجح.
- [x] ظهر في الـlogs:

```text
No migrations were applied. The database is already up to date.
```

- [x] استخدام Schemas حاليًا:

```text
pos
auth
```

### 5.3 الكيانات الأساسية

تم إنشاء أو تجهيز الكيانات التالية:

```text
BaseEntity
Tenant
Branch
Product
AppUser
Shift
Order
OrderLine
Payment
```

خصائص مشتركة مهمة:

- `Guid` كمفتاح أساسي.
- `CreatedAtUtc` و`UpdatedAtUtc`.
- `RowVersion` للتزامن.
- `decimal` للقيم المالية.
- `TenantId` و`BranchId` حسب طبيعة السجل.

### 5.4 المصادقة وتسجيل الدخول

- [x] تنفيذ JWT Authentication.
- [x] استخدام `PasswordHasher<AppUser>` لتخزين Password Hash.
- [x] إنشاء endpoints:

```http
POST /api/auth/login
GET  /api/auth/me
```

- [x] إنشاء مستخدم Development تجريبي:

```text
Username: admin
Password: Admin@123
```

> هذه البيانات للتطوير فقط، ولا تستخدم في Production.

### 5.5 Database Seeder

تم إنشاء `DatabaseSeeder` لإضافة بيانات Development تلقائيًا:

- Tenant باسم تقريبي `PosFlow Demo`.
- فرع رئيسي بالكود `MAIN`.
- مستخدم Admin.
- منتجات تجريبية مثل القهوة والشاي والعصير والساندوتش والمياه.

### 5.6 Swagger وتشغيل الـAPI

الـAPI يعمل حاليًا على Profile باسم `https`:

```text
https://localhost:7178
http://localhost:5033
```

Swagger:

```text
https://localhost:7178/swagger
```

البورت:

```text
https://localhost:44324
```

خاص بـIIS Express فقط، وليس Profile `https` الحالي.

### 5.7 Angular

- [x] تثبيت Node.js وnpm.
- [x] تثبيت Angular CLI.
- [x] حل مشكلة PowerShell Execution Policy باستخدام `npm.cmd`/`ng.cmd` أو سياسة `RemoteSigned`.
- [x] إنشاء مشروع Angular باسم:

```text
posflow-web
```

- [x] المشروع يستخدم:
  - Routing
  - SCSS
  - Standalone Components
  - بدون SSR/SSG
- [x] Angular يعمل على:

```text
http://localhost:4200
```

### 5.8 صفحات Angular الحالية

تم إنشاء routes/components مبدئية:

```text
/login
/open-shift
/products
/pos
```

والـComponents:

```text
LoginComponent
OpenShiftComponent
ProductListComponent
CheckoutComponent
```

### 5.9 ربط Angular بالـAPI

ملف Proxy المستهدف:

```text
posflow-web/src/proxy.conf.json
```

ومحتواه الحالي:

```json
{
  "/api/**": {
    "target": "https://localhost:7178",
    "secure": false,
    "changeOrigin": true,
    "logLevel": "debug"
  }
}
```

تشغيل Angular:

```powershell
ng.cmd serve --proxy-config src/proxy.conf.json
```

### 5.10 Authentication داخل Angular

تم إنشاء أو تجهيز:

```text
AuthService
Login Models
JWT Interceptor
Auth Guard
Login Form
localStorage token storage
```

التدفق الحالي:

```text
Angular Login
→ POST /api/auth/login
→ JWT Access Token
→ حفظ التوكن
→ الانتقال إلى /open-shift
```

### 5.11 إدارة الوردية

تم إنشاء أو تجهيز Backend للوردية:

```text
ICurrentUser
CurrentUserService
OpenShiftRequest
CloseShiftRequest
ShiftResponse
IShiftService
ShiftService
ShiftsController
```

Endpoints:

```http
GET  /api/shifts/current
POST /api/shifts/open
POST /api/shifts/{shiftId}/close
```

تم تجهيز واجهة Angular لفتح وإغلاق الوردية، مع:

- نموذج النقدية الافتتاحية.
- عرض الوردية الحالية.
- نموذج النقدية الفعلية عند الإغلاق.
- حساب مبدئي للنقدية المتوقعة والفرق.
- `shiftGuard` لمنع دخول `/pos` بدون وردية مفتوحة.

---

## 6. آخر نقطة وصل إليها المشروع

### المشكلة التي ظهرت

عند فتح:

```text
http://localhost:4200/open-shift
```

ظلت الصفحة على رسالة:

```text
جاري تحميل بيانات الوردية...
```

وطلب:

```http
GET /api/shifts/current
```

كان يرجع:

```text
204 No Content
```

### تفسير الحالة

`204 No Content` لا يعني فشل الطلب. معناه أن المستخدم لا يملك وردية مفتوحة، والـAPI كان يعيد `null`، فتحول إلى استجابة بدون Body.

### التعديل الأخير المقترح

تم اقتراح تغيير endpoint لكي يرجع دائمًا `200 OK` بالشكل التالي:

```json
{
  "hasOpenShift": false,
  "shift": null
}
```

وإنشاء model في Angular:

```typescript
export interface CurrentShiftResponse {
  hasOpenShift: boolean;
  shift: ShiftResponse | null;
}
```

ثم تعديل:

- `ShiftService.getCurrent()`.
- `OpenShiftComponent.loadCurrentShift()`.
- `shiftGuard`.

### حالة هذه الخطوة

```text
قيد الاختبار - لم يتم تأكيد نجاح التدفق بالكامل بعد التعديل.
```

هذه هي **أول نقطة يجب البدء منها في الجلسة القادمة**.

---

## 7. أول Checklist للجلسة القادمة

نفّذ بالترتيب ولا تنتقل للخطوة التالية قبل نجاح السابقة:

1. شغّل الـAPI وتأكد من:

```text
Now listening on: https://localhost:7178
```

2. افتح Swagger واختبر Login.
3. اختبر `GET /api/shifts/current` بتوكن صالح.
4. تأكد أن النتيجة `200 OK` وليست `204`، وأن الـBody:

```json
{
  "hasOpenShift": false,
  "shift": null
}
```

5. شغّل Angular بالـProxy.
6. امسح أي Token قديم وسجل دخول مرة أخرى.
7. افتح `/open-shift` وتأكد من اختفاء Loading وظهور حقل النقدية الافتتاحية.
8. أدخل مثلًا `500` واضغط **فتح الوردية**.
9. تأكد من الانتقال إلى `/pos`.
10. ارجع إلى `/open-shift` وتأكد من عرض بيانات الوردية المفتوحة.
11. أغلق الوردية وتأكد أن البيانات اتخزنت في SQL Server.

### التدفق المطلوب إثبات نجاحه

```text
Login
→ Get Current Shift
→ Open Shift
→ Enter POS
→ Close Shift
```

---

## 8. الملفات المهمة عند استكمال العمل

### Backend

```text
src/PosFlow.Api/Program.cs
src/PosFlow.Api/appsettings.json
src/PosFlow.Api/Properties/launchSettings.json
src/PosFlow.Api/Controllers/AuthController.cs
src/PosFlow.Api/Controllers/ShiftsController.cs

src/PosFlow.Application/Auth/*
src/PosFlow.Application/Common/ICurrentUser.cs
src/PosFlow.Application/Shifts/*

src/PosFlow.Domain/Entities/AppUser.cs
src/PosFlow.Domain/Entities/Shift.cs
src/PosFlow.Domain/Entities/Product.cs
src/PosFlow.Domain/Entities/Order.cs
src/PosFlow.Domain/Entities/OrderLine.cs
src/PosFlow.Domain/Entities/Payment.cs

src/PosFlow.Infrastructure/Authentication/AuthService.cs
src/PosFlow.Infrastructure/Authentication/CurrentUserService.cs
src/PosFlow.Infrastructure/Persistence/PosFlowDbContext.cs
src/PosFlow.Infrastructure/Persistence/DatabaseSeeder.cs
src/PosFlow.Infrastructure/Shifts/ShiftService.cs
```

### Angular

```text
posflow-web/src/app/app.routes.ts
posflow-web/src/app/app.config.ts
posflow-web/src/app/core/auth/auth.service.ts
posflow-web/src/app/core/auth/auth.interceptor.ts
posflow-web/src/app/core/auth/auth.guard.ts
posflow-web/src/app/core/auth/shift.guard.ts
posflow-web/src/app/features/login/*
posflow-web/src/app/features/shifts/shift.models.ts
posflow-web/src/app/features/shifts/shift.service.ts
posflow-web/src/app/features/shifts/open-shift/*
posflow-web/src/proxy.conf.json
```

---

## 9. الخطوة التالية بعد إنهاء الوردية

بعد التأكد أن إدارة الوردية تعمل end-to-end، نبدأ **Products + POS Cart**.

### الترتيب القريب للـMVP

1. Products API.
2. عرض المنتجات داخل شاشة POS.
3. البحث والفلترة والباركود لاحقًا.
4. إضافة المنتجات إلى Cart.
5. زيادة وتقليل الكمية.
6. حذف صنف من Cart.
7. حساب Subtotal وTotal.
8. إنشاء Order داخل الـBackend.
9. الدفع النقدي.
10. إكمال الطلب.
11. ربط Cash Sales بالوردية.
12. طباعة إيصال مبدئي.

### الـMVP المستهدف

```text
Login
→ Open Shift
→ Select Products
→ Create Order
→ Cash Payment
→ Complete Order
→ Close Shift
```

---

## 10. الخطة المستقبلية

### 10.1 Restaurant Features

```text
Dining Areas
Tables
Open/Transfer Table
Split Bill
Merge Orders
Kitchen Display System
Kitchen Printers
Modifiers
Combos
Dine-in
Takeaway
Delivery
```

### 10.2 Inventory and Purchasing

```text
Warehouses
Stock Movements
Stock Counts
Transfers
Waste
Suppliers
Purchase Orders
Goods Receipts
Recipes
Automatic Ingredient Deduction
Theoretical vs Actual Consumption
```

### 10.3 Payments

```text
Card Payments
Geidea Integration
Refunds
Voids
Payment Links
QR Payments
Settlement Reconciliation
Provider Abstraction Layer
```

### 10.4 Customers and Loyalty

```text
Customers
Loyalty Points
Customer Levels
Coupons
Gift Cards
Campaigns
```

### 10.5 Egypt Tax Integration

```text
Electronic Receipt
UUID
QR Code
Submission Queue
Accepted / Rejected / Pending
Offline Submission
Returns
Device Registration
Audit Trail
```

### 10.6 Offline-first

```text
IndexedDB
Local Products
Local Prices and Taxes
Local Orders
Pending Operations Queue
Sync Engine
Idempotency Keys
Conflict Handling
Retry Strategy
```

### 10.7 Hardware Integration

تطبيق محلي صغير باستخدام C# يعمل كـDevice Agent للتعامل مع:

```text
Thermal Printer
Cash Drawer
Barcode Scanner
Weight Scale
Customer Display
Payment Terminal
```

### 10.8 Advanced Platform

```text
Multi-branch Management
Advanced Roles and Permissions
Accounting
Expenses
Profit and Loss
Public API
Webhooks
App Marketplace
Manager Mobile App
Advanced Reports
```

---

## 11. قواعد تقنية يجب ألا تتغير بدون قرار صريح

- لا يتم وضع Business Logic داخل Angular.
- لا يتم الثقة في `TenantId`, `BranchId`, `UserId` القادم من Request Body.
- هذه القيم تؤخذ من JWT/Current User Context.
- كل القيم المالية تستخدم `decimal` في C# و`DECIMAL(19,4)` في SQL Server.
- لا يتم استخدام `float` للأموال.
- Order Number يولده السيرفر.
- `OrderLine` يحتفظ Snapshot لاسم وسعر المنتج وقت البيع.
- لا يتم حذف الطلبات والمدفوعات المالية نهائيًا؛ يستخدم Cancel/Void/Refund.
- لا يتم تخزين بيانات البطاقة الخام أو CVV.
- لا تبدأ Microservices قبل وجود سبب تشغيلي واضح.
- لا تبدأ Accounting كامل أو Offline Sync قبل استقرار دورة البيع الأساسية.
- أي Migration تُنشأ بعد نجاح Build فقط.
- حزم EF Core يجب أن تكون متوافقة في جميع المشاريع.
- لا تخلط أكثر من تنفيذ Swagger/OpenAPI بدون حاجة.

---

## 12. أوامر التشغيل الأساسية

### تشغيل الـAPI

من Visual Studio: اختر Profile باسم `https` ثم Run.

أو من Terminal داخل جذر المشروع:

```powershell
dotnet run --project .\src\PosFlow.Api\PosFlow.Api.csproj
```

### Swagger

```text
https://localhost:7178/swagger
```

### تشغيل Angular

```powershell
cd posflow-web
ng.cmd serve --proxy-config src/proxy.conf.json
```

### إنشاء Migration

داخل Package Manager Console:

```powershell
Add-Migration MigrationName -Project PosFlow.Infrastructure -StartupProject PosFlow.Api -Context PosFlowDbContext -OutputDir Persistence\Migrations
```

### تطبيق Migration

```powershell
Update-Database -Project PosFlow.Infrastructure -StartupProject PosFlow.Api -Context PosFlowDbContext
```

### Clean وBuild

```powershell
dotnet clean
dotnet restore
dotnet build
```

---

## 13. مشاكل تم حلها سابقًا

### Node غير معروف

تم تثبيت Node.js LTS والتأكد من `node` و`npm`.

### PowerShell يمنع `npm.ps1`

تم استخدام:

```powershell
npm.cmd
ng.cmd
```

أو ضبط:

```powershell
Set-ExecutionPolicy -Scope CurrentUser -ExecutionPolicy RemoteSigned
```

### Swagger methods غير موجودة

تم تثبيت/توحيد حزم Swagger المناسبة.

### أخطاء `IPasswordHasher` داخل Infrastructure

تمت إضافة مرجع ASP.NET Core Framework إلى مشروع Infrastructure عند الحاجة:

```xml
<FrameworkReference Include="Microsoft.AspNetCore.App" />
```

### Connection Refused على 44324

السبب كان استخدام بورت IIS Express مع تشغيل Profile `https`. البورت الفعلي الحالي هو `7178`.

---

## 14. تعريف النجاح للمرحلة الحالية

تعتبر مرحلة Authentication + Shift Management مكتملة فقط عندما تنجح الحالات التالية:

- [ ] Login صحيح يرجع JWT.
- [ ] Login خاطئ يرجع Unauthorized برسالة واضحة.
- [ ] صفحة محمية لا تفتح بدون Token.
- [ ] `GET /api/shifts/current` يرجع response ثابتًا.
- [ ] المستخدم يستطيع فتح وردية واحدة فقط.
- [ ] محاولة فتح وردية ثانية يتم رفضها.
- [ ] `/pos` لا يفتح بدون وردية.
- [ ] الوردية المفتوحة تظهر بعد Refresh.
- [ ] المستخدم يستطيع إغلاق الوردية.
- [ ] بيانات الوردية محفوظة في SQL Server.
- [ ] Angular لا يظل في Loading بعد استجابة فارغة أو خطأ.

---

## 15. قالب تحديث الوثيقة بعد كل جلسة

أضف في نهاية كل جلسة:

```text
تاريخ الجلسة:

تم تنفيذ:
- 
- 

تم اختبار:
- 
- 

مشاكل حالية:
- 

آخر ملف تم تعديله:
- 

أول خطوة للجلسة القادمة:
- 
```

---

## 16. ملخص شديد الاختصار

```text
اسم المشروع: PosFlow
Backend: ASP.NET Core Web API
Frontend: Angular Standalone
Database: SQL Server + EF Core
Authentication: JWT
الحالة الحالية: Login يعمل، API وDB يعملان، Angular يعمل، وإدارة الوردية قيد الإكمال.
آخر نقطة: تغيير GET /api/shifts/current من 204 إلى 200 مع CurrentShiftResponse ثم اختبار Open/Close Shift.
بعدها مباشرة: Products API ثم POS Cart ثم Order ثم Cash Payment.
```
