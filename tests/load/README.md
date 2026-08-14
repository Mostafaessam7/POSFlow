# اختبار الحمل (Load Testing)

سكريبت [k6](https://k6.io) بسيط لاختبار أهم الـ endpoints تحت حمل - أشار
`ENTERPRISE-READINESS.md` صراحة لغياب أي load testing، وده أول خطوة عملية
لسد الفجوة دي.

## التشغيل

1. ثبّت k6 (لا يحتاج .NET/Node - أداة مستقلة):
   - Windows: `winget install k6 --source winget` أو حمّله من https://k6.io/docs/get-started/installation/
   - أو استخدم صورة Docker الجاهزة: `docker run --rm -i grafana/k6 run - <posflow-load-test.js`

2. شغّل الـ API محليًا (أو أشِّر على بيئة staging - **مش production أبدًا**، اختبار الحمل بيولّد بيانات فعلية وضغط حقيقي).

3. شغّل السكريبت:

```bash
k6 run tests/load/posflow-load-test.js
```

متغيرات بيئة اختيارية:

```bash
POSFLOW_BASE_URL=https://localhost:5443 POSFLOW_USERNAME=cashier POSFLOW_PASSWORD=Cashier@123 k6 run tests/load/posflow-load-test.js
```

## اللي بيغطّيه

- **`browse_catalog`**: يحاكي كاشير بيصفح قائمة المنتجات كتير أثناء الشيفت (حتى 20 مستخدم متزامن) - يغطي بالظبط النقطة اللي `ENTERPRISE-READINESS.md` ذكرها: قائمة المنتجات بتتقرا كتير ومفيهاش caching حقيقي (غير التصنيفات).
- **`checkout`**: سيناريو بيع كامل (فتح وردية لو مقفولة، تحديد منتج، إتمام الشراء) - **مقصود يكون بـ VU واحد فقط**، لأن بيانات الـ demo seed فيها كاشير واحد بس، والنظام بيسمح بوردية مفتوحة واحدة فقط لكل (tenant, branch, user) في نفس الوقت. عشان تختبر checkout بحمل أعلى، لازم تزوّد حسابات كاشير تانية في الـ seed أولاً.

## حدود السكريبت الحالي

- مش بديل عن اختبار حمل حقيقي على بيئة تشبه الإنتاج (سيرفر SQL Server حقيقي، مش LocalDB).
- الـ thresholds (`http_req_failed < 1%`, `p95 < 800ms` لقائمة المنتجات) قيم بداية معقولة - عدّلها حسب SLA فعلي لو اتحدد.
- لا يغطي senarios زي void order أو reports - أضف `exec` function جديدة بنفس النمط لو محتاجها.
