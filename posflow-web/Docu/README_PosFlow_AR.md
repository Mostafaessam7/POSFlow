# PosFlow

منصة نقاط بيع وإدارة تشغيل للمطاعم والريتيل، مبنية باستخدام:

- C# وASP.NET Core Web API
- Angular
- SQL Server وEntity Framework Core
- Modular Monolith + Clean Architecture

## هدف الـMVP

```text
Login → Open Shift → View Products → Create Order → Cash Payment → Complete Order → Close Shift
```

## هيكل الحل

```text
PosFlow/
├── PosFlow.sln
├── src/
│   ├── PosFlow.Api/
│   ├── PosFlow.Application/
│   ├── PosFlow.Domain/
│   └── PosFlow.Infrastructure/
├── tests/
└── frontend/
    └── posflow-web/
```

## مسؤوليات المشاريع

| المشروع | المسؤولية |
|---|---|
| `PosFlow.Domain` | الكيانات وEnums وقواعد المجال |
| `PosFlow.Application` | Use Cases وDTOs وValidation وInterfaces |
| `PosFlow.Infrastructure` | EF Core وSQL Server والتكاملات |
| `PosFlow.Api` | Controllers وMiddleware وDependency Injection |
| `posflow-web` | واجهات Angular للكاشير والإدارة |

## الكيانات الحالية

- `Tenant`
- `Branch`
- `Product`
- `Shift`
- `Order`
- `OrderLine`
- `Payment`

## تشغيل الـBackend

1. اجعل `PosFlow.Api` هو Startup Project.
2. ضع Connection String في `appsettings.Development.json` أو User Secrets.
3. نفذ Build.
4. من Package Manager Console:

```powershell
Add-Migration InitialCreate `
  -Project PosFlow.Infrastructure `
  -StartupProject PosFlow.Api `
  -Context PosFlowDbContext `
  -OutputDir Persistence\Migrations

Update-Database `
  -Project PosFlow.Infrastructure `
  -StartupProject PosFlow.Api `
  -Context PosFlowDbContext
```

5. شغل API وافتح `/swagger`.

## تشغيل Angular

أوامر Angular تنفذ في Terminal/PowerShell وليس NuGet Package Manager Console.

```powershell
node -v
npm -v
npm install -g @angular/cli

mkdir frontend
cd frontend
ng new posflow-web --routing --style=scss --standalone --skip-git
cd posflow-web
ng serve --open
```

## قواعد أساسية

- لا تستخدم `float` أو `double` للمبالغ المالية؛ استخدم `decimal` و`DECIMAL(19,4)`.
- السيرفر هو المسؤول عن السعر والحساب النهائي.
- `TenantId` يأتي من المستخدم الموثق، وليس من قيمة موثوق بها في Request.
- لا ترجع EF Entities مباشرة من Controllers؛ استخدم DTOs.
- لا تضع منطق أعمال كبير داخل Controllers.
- لا تخزن كلمات المرور أو التوكنات أو بيانات البطاقات في Logs.

## Endpoints المستهدفة للـMVP

```text
POST /api/v1/auth/login
GET  /api/v1/products
POST /api/v1/products
GET  /api/v1/shifts/current
POST /api/v1/shifts/open
POST /api/v1/shifts/{id}/close
POST /api/v1/orders
GET  /api/v1/orders/{id}
POST /api/v1/orders/{id}/payments
POST /api/v1/orders/{id}/complete
```

## Definition of Done

- Build ناجح.
- الاختبارات ناجحة.
- Swagger محدث.
- Migration محدث عند تغيير قاعدة البيانات.
- Tenant isolation مختبر.
- لا توجد أسرار في المستودع.
- رحلة البيع الأساسية تم اختبارها.

راجع ملف `PosFlow_Project_Documentation_AR.docx` للتوثيق الكامل.
