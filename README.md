# OrganizationIntranet

پرتال مرکزی اینترانت سازمان (اداره کل بنادر و دریانوردی استان هرمزگان): ورود و دسترسی، سامانه‌ها، اعلان، اطلاع‌رسانی، چارت سازمانی، کتابخانه‌ی اسناد و ورود یکپارچه (SSO) به سامانه‌های دیگر. منطق کسب‌وکار هر سامانه داخل خود آن می‌ماند، نه اینجا.

پنج پروژه: `Domain`، `Application`، `Api` (EF Core و زیرساخت)، `Portal` و `Admin` (Razor Pages که فقط با HTTP به API وصل‌اند). ساختار و اجرا: [معماری پنج‌پروژه‌ای](docs/five-project-architecture.md). قواعد دائمی معماری: [CLAUDE.md](CLAUDE.md).

## شروع سریع

```powershell
dotnet test tests/OrganizationIntranet.Tests                 # آزمون‌ها (بانک واقعی رد می‌شود)
./tools/Start-Dev.ps1                                        # Api (7232)، Portal (7230) و Admin (7231)
./tools/New-FirstAdmin.ps1 -Username <نام> -Name <نام> -LastName <نام خانوادگی>   # فقط روی نصب بدون مدیر
```

نصب دیتابیس و migrationها: [database/migrations/README.md](database/migrations/README.md). آدرس سرور بانک در `appsettings.json` پروژه‌ی Api است؛ آدرس درگاه پیامک و ... را در `appsettings.Development.json` (در git نیست؛ نمونه: `appsettings.Development.example.json`) بگذارید.

## مستندات

| موضوع | سند |
|---|---|
| وضعیت امنیتی، ریسک‌های پذیرفته‌شده و چک‌لیست استقرار | [security-review.md](docs/security-review.md) |
| مجوزهای پنل مدیریت | [admin-permissions.md](docs/admin-permissions.md) |
| چارت سازمانی، دسترسی بر اساس چارت و اعلان سازمانی | [organization-chart.md](docs/organization-chart.md) |
| ورود یکپارچه (SSO) و نگاشت نام کاربری | [sso.md](docs/sso.md) |
| کتابخانه‌ی اسناد و فرم‌ها (آپلود، پیش‌نمایش، import) | [library.md](docs/library.md) |
| درخواست دسترسی، نظارت، اعلان و پیامک | [access-workflow-and-integrations.md](docs/access-workflow-and-integrations.md) |
| اطلاع‌رسانی سازمانی (مطالب، اسلایدر، بایگانی) | [organizational-publications.md](docs/organizational-publications.md) |
| صفحه‌های پورتال و پرتال عمومی | [portal-pages.md](docs/portal-pages.md)، [public-portal.md](docs/public-portal.md) |
| ورود با کد ملی | [national-id-login.md](docs/national-id-login.md) |
| پنل مدیریت (UI) | [admin-ui.md](docs/admin-ui.md) |
| پایگاه داده: طراحی و فهرست جدول‌ها | [database-design.md](docs/database-design.md)، [database-catalog.md](docs/database-catalog.md) |
| آزمون‌ها | [tests/README.md](tests/README.md) |
| فهرست کارها، وضعیت و تصمیم‌های باز | [OrganizationIntranet-ToDo.xlsx](docs/OrganizationIntranet-ToDo.xlsx) |

## قراردادهای مهم

- **هر تغییر همراه با تست** و **هر نوع (class، record، interface، enum) با `<summary>` فارسی**؛ آزمون `PersianSummaryTests` کمبود را گزارش می‌دهد.
- Portal و Admin نشست جدا دارند (کوکی و کلید رمزگذاری جدا) و هرگز مستقیم به بانک وصل نمی‌شوند.
- پیش از هر تغییر روی دیتابیس backup بگیرید (`database/backup.sql`)؛ فایل‌های کتابخانه (`Library:StoragePath`) را هم جداگانه پشتیبان بگیرید.
- کلیدهای توسعه عمداً در `appsettings.json` هستند؛ **پیش از استقرار واقعی همه عوض شوند** ([security-review.md](docs/security-review.md)).
