# پرتال عمومی (بدون ورود)

بازدیدکننده‌ی ناشناس صفحه‌ی اصلی پرتال را می‌بیند و فقط داده‌ی **عمومی** دریافت می‌کند؛ بعد از ورود، همان صفحه با سامانه‌های اختصاصی، اعلان‌ها و درخواست دسترسی کامل می‌شود. این سند رفتار مصوب و کد موجود را توضیح می‌دهد.

## چه چیزی «عمومی» است

| چیز | شرط نمایش به ناشناس |
|---|---|
| سامانه (`App.Application`) | `IsActive = 1` و `IsPublic = 1`؛ آدرس فقط HTTP(S) بدون اطلاعات ورود نمایش داده می‌شود، وگرنه کارت بدون لینک است |
| مطلب (`Notify.Publication`) | `IsPublished = 1` و `IsPublic = 1`. ستون `IsPublic` پیش‌فرض ۰ است و قید `CK_Publication_PublicRequiresPublished` پیش‌نویس را عمومی نمی‌گذارد |

هیچ‌چیز خودکار عمومی نمی‌شود: مدیر در Admin، گزینه‌ی «نمایش عمومی» را برای هر مطلب (و `IsPublic` را برای هر سامانه) جداگانه می‌زند.

## مسیرها

- **API** (`PublicController`، بدون Bearer ولی با `X-Intranet-Client` که فقط سرور Portal دارد، با Rate Limit سیاست `public`):
  `GET api/public/applications`، `GET api/public/publications` (فیلتر `kind`، `search`، `page`)، `GET api/public/publications/{id}`. فقط خواندنی‌اند.
- **Portal**: `/` به `/Portal` می‌رود (پنل مدیریت همچنان با `/Account/Login` شروع می‌شود). `/Portal`، `/Publications` و `/Publications/Details/{id}` برای ناشناس باز است. کاربر واردشده همان صفحه‌ها را با داده‌ی کامل می‌بیند.
- مطلب پیش‌نویس یا غیرعمومی برای ناشناس «۴۰۴» است تا وجودش لو نرود.
- همه‌ی عملیات POST در صفحه‌ی اصلی (اعلان، درخواست دسترسی و غیره) برای ناشناس رد می‌شود و API هم جداگانه Bearer را می‌خواهد.

## مرز امنیتی

- کنترل دسترسی در **API** است، نه در UI: مسیرهای `api/publications`، `api/portal/*`، `api/account/*` و `api/admin/*` همچنان بدون Bearer «۴۰۱» می‌دهند. این در `PublicPortalTests` آزموده می‌شود.
- ناشناس شمارنده‌ی اعلان، فهرست سرویس‌ها، کد سامانه، تنظیمات دسترسی، نام کاربر یا نقش نمی‌بیند (DTO جدا: `PublicApplicationDto`).
- متن مطالب Razor-encode می‌شود؛ آدرس‌های غیر HTTP(S) هرگز در HTML نمی‌آیند.

## بانک

migration `005_PublicPublications` (`database/migrations`) ستون `IsPublic`، قید و ایندکس را اضافه می‌کند و به `004_AccessWorkflow` وابسته است (تاریخچه‌ی خطی). down در حضور مطلب عمومی اجرا نمی‌شود. **این migration در ۱۴۰۵/۰۷/۲۸ روی دیتابیس توسعه اعمال و verify شد** (دیتابیس از پشتیبان کامل `masoud` برگردانده شده بود و 001 تا 004 را داشت). قبل از اجرا یک backup از نوع COPY_ONLY گرفته شد و اجرای دوباره‌ی up بی‌اثر است. درخواست ناشناس به API واقعی روی SQL Server واقعی هم آزموده شد. برای نصب‌های دیگر دستورها این‌هاست:

```powershell
pwsh -File tools/Invoke-DatabaseScript.ps1 -ScriptPath database/migrations/005_PublicPublications.up.sql
pwsh -File tools/Invoke-DatabaseScript.ps1 -ScriptPath database/migrations/005_PublicPublications.verify.sql
```

قبل از اجرا backup بگیرید (`database/backup.sql`). فایل `.gitignore` اصلاح شد: خط `/database/migrations` کل پوشه را نادیده می‌گرفت و فایل‌های 003 هیچ‌وقت commit نشدند.

## آزمون‌ها

`tests/OrganizationIntranet.Tests`: `Integration/PublicPortalTests.cs` (API، صفحه‌ها، امنیت، POST ناشناس) و `Database/PublicationPublicConstraintTests.cs` (قید بانک و دامنه‌ی Repository).
