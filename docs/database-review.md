# بازبینی دیتابیس — ۲۰۲۶/۰۹/۱۶

این سند نتیجه درخواست جدید اصلاح دیتابیس است و محدودیت «عدم تغییر بانک» در گزارش تاریخی مهاجرت پنج‌پروژه‌ای را فقط برای تغییرات این سند به‌روز می‌کند. مرز Domain/Application/API/UI و سیاست‌های احراز هویت و مجوز حفظ شده‌اند.

## بررسی و تغییرات اعمال‌شده

بانک واقعی `OrganizationIntranet` روی SQL Server بررسی شد: ۹ جدول اولیه در Schemaهای `Sec`، `App` و `Notify`. نام مفرد و PascalCase جدول‌ها و ستون‌های شناسه (`UserId` و مانند آن) حفظ شد؛ این نام‌ها با Entityها و مسئولیت Schemaها سازگارند. جدول `Sec.UserPermission` با وجود نبود مصرف کاربردی فعلی حذف نشد. هیچ رکورد کاربردی تغییر یا حذف نشد.

استاندارد نام‌گذاری: `PK_<Table>`، `FK_<Table>_<ReferencedTable>` (در صورت ابهام همراه ستون رابطه)، `UQ_<Table>_<Columns>` برای قید یکتا، `UX_<Table>_<Columns>` برای ایندکس یکتای مستقل، `IX_<Table>_<Columns>`، `DF_<Table>_<Column>` و `CK_<Table>_<Rule>`.

- نام ۶ قید یکتا و یک ایندکس یکتا با نام کامل ستون‌ها هماهنگ شد؛ شناسه و وابستگی اشیا حفظ شد.
- ایندکس‌های `UX_Application_ApplicationCode` و `UX_Role_RoleCode` پس از بررسی تعریف و نبود FK وابسته حذف شدند؛ قیود یکتای معادل باقی مانده‌اند.
- ایندکس FKهای `UserPermission.PermissionId` و `Notification.ApplicationId/CreatedBy` اضافه شد.
- ایندکس `(UserId, IsRead, CreatedAt)` برای دریافت اعلان‌های هر کاربر اضافه شد.
- قید `CK_UserNotification_ReadState` سازگاری وضعیت خواندن با `ReadAt` را اعمال می‌کند: نخوانده بدون زمان، خوانده با زمان.
- پیش‌فرض `RolePermission.CreatedAt` و ثبت نقش/مجوز در Repository برای رکوردهای جدید UTC شد. زمان‌های تاریخی بدون حدس درباره منطقه زمانی یا منبع ثبت، تغییر نکردند؛ داده‌های قدیمی این ستون ممکن است زمان محلی باشند.
- نگاشت EF برای ستون‌های واقعی `varchar`، ستون `ReadAt` از نوع `datetime`، رفتار `NO ACTION` همه روابط اولیه، ایندکس ترتیب سامانه و یکتایی سراسری `PermissionCode` اصلاح شد.
- یکتایی سراسری کد مجوز از قبل در بانک وجود داشت و حفظ شد؛ Claimها فعلاً فقط کد را حمل می‌کنند، بنابراین حذف این یکتایی می‌تواند بین دو سامانه تداخل دسترسی ایجاد کند. پشتیبانی از کد یکسان در سامانه‌های مختلف نیازمند تغییر هماهنگ قرارداد مجوز است.

## اجرا و بازگشت

اسکریپت‌ها SQL نسخه‌دار هستند، نه EF Migration. جدول `dbo.SchemaMigration` اجرای نسخه را ثبت می‌کند. Up دارای تراکنش، قفل مهاجرت، کنترل انحراف تعریف ایندکس‌ها و قابلیت اجرای مجدد است. روی بانک تازه یا ساختار متفاوت ابتدا باید baseline جداگانه تهیه شود؛ اجرای خودکار `EnsureCreated`/`Migrate` در راه‌اندازی API اضافه نشده است.

```powershell
./tools/Invoke-DatabaseScript.ps1 -ScriptPath database/backup.sql
./tools/Invoke-DatabaseScript.ps1 -ScriptPath database/migrations/001_DatabaseConsistency.up.sql
./tools/Invoke-DatabaseScript.ps1 -ScriptPath database/verify.sql
# در صورت نیاز، همراه بازگشت نسخه کد:
./tools/Invoke-DatabaseScript.ps1 -ScriptPath database/migrations/001_DatabaseConsistency.down.sql
```

اتصال از `ConnectionStrings__DefaultConnection` یا User Secrets پروژه API خوانده می‌شود. برای اتصال قدیمی ریشه، سوییچ صریح `-UseExistingConnection` در دسترس است. اطلاعات اتصال چاپ نمی‌شود. Rollback رکوردهای کاربردی را پاک نمی‌کند؛ زمان‌های UTC ایجادشده پس از ارتقا نیز بازنویسی نمی‌شوند. جدول ثبت نسخه برای استفاده بعدی باقی می‌ماند.

پیش از اجرای واقعی، پشتیبان COPY_ONLY همراه CHECKSUM تهیه و با RESTORE VERIFYONLY بررسی شد. فایل در مسیر Backup سرور با نام `OrganizationIntranet_BeforeDatabaseReview_20260916_121446.bak` قرار دارد. VERIFYONLY جای آزمون بازیابی کامل را نمی‌گیرد.

## نتیجه آزمون

- اجرای Up، Down، Up و اجرای تکراری Up داخل تراکنش آزمایشی روی SQL Server موفق بود؛ کل آزمایش rollback شد. سپس Up واقعاً اعمال شد.
- `DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS` تخلفی گزارش نکرد؛ هیچ FK غیرفعال یا نامعتبر نبود.
- تعداد داده‌ها پیش/پس از مهاجرت ثابت بود: User=3، Role=2، UserRole=3، Application=1، Permission=6، RolePermission=6؛ سه جدول Notification/UserNotification/UserPermission خالی بودند.
- ساخت پروژه‌ها و ۱۸ آزمون یکپارچه موفق شد؛ آزمون‌های اضافه‌شده، منع تخصیص تکراری، تداخل کد مجوز بین سامانه‌ها، وضعیت نامعتبر خواندن اعلان و حذف کاربر وابسته را بررسی می‌کنند.
- خروجی معمول Debug توسط API/Portal/Admin و Visual Studio قفل بود. آزمون موفق در مسیر مجزای `artifacts/database-review/build` اجرا شد؛ پردازش‌های کاربر متوقف نشدند. برنامه در حال اجرای قبلی برای استفاده از کد جدید نیاز به بازسازی و راه‌اندازی مجدد دارد.
- اجرای اولیه هشدارهای NuGet درباره Azure.Identity، Microsoft.Identity.Client و System.Formats.Asn1 داشت. ارتقای وابستگی‌ها در این تغییر انجام نشد؛ اجرای مجزای نهایی با `NuGetAudit=false` فقط برای جلوگیری از تکرار ممیزی restore بود و به معنی رفع هشدارها نیست.
- آزمون‌های کاربردی از SQLite استفاده می‌کنند؛ اجرای واقعی SQL Server برای اسکریپت‌ها و قیود بررسی شد، اما آزمون کامل ورود HTTP با داده عملیاتی انجام نشد.
- تغییرات هم‌زمان Publication متعلق به کار اطلاع‌رسانی است و اسکریپت جداگانه `tools/sql/001-publications-up.sql` دارد؛ ایجاد یا مهاجرت این جدول در نسخه 001 این سند اجرا نشده است.

```powershell
dotnet test tests/OrganizationIntranet.SmokeTests/OrganizationIntranet.SmokeTests.csproj --artifacts-path artifacts/database-review/build --verbosity minimal -p:NuGetAudit=false
```
