# migrationهای دیتابیس

با `git pull` فقط کد و این فایل‌ها می‌آیند، نه دیتابیس. هر کس دیتابیس خودش را دارد و باید migrationهای تازه را **خودش و به ترتیب** روی آن اجرا کند. برنامه migration را خودکار اجرا نمی‌کند. کد تازه روی دیتابیس عقب‌مانده خطا می‌دهد؛ مثلاً بدون 008 هیچ‌کس وارد پنل مدیریت نمی‌شود.

## چطور کار می‌کنند

- هر migration سه فایل دارد:
  - `up`: تغییر را اعمال می‌کند.
  - `verify`: بررسی می‌کند که تغییر کامل نشسته باشد.
  - `down`: تغییر را برمی‌گرداند.

  001 و 002 فایل verify ندارند.
- اجرای هر `up` در جدول `dbo.SchemaMigration` ثبت می‌شود. هر `up` اول چک می‌کند migration قبلی ثبت شده باشد و اگر نشده باشد بدون تغییر متوقف می‌شود.
- اجرای دوباره‌ی یک `up` که قبلاً اجرا شده، کاری نمی‌کند.
- همه‌ی اسکریپت‌ها در یک تراکنش اجرا می‌شوند. اگر جایی خطا بدهد، هیچ تغییری نمی‌ماند.
- داده‌ی واقعی را حذف نمی‌کنند. داده‌ی آزمایشی هم در هیچ migrationی نیست.

## فهرست (به همین ترتیب)

| شناسه | فایل‌ها | چه می‌کند |
|---|---|---|
| `001_DatabaseConsistency` | همین پوشه | یکدست‌کردن ساختار قدیمی (کلید، Index، قید)، بدون تغییر داده |
| `002_AuthenticationSchema` | همین پوشه | جدول‌های ورود: تنظیمات روش ورود، کد پیامکی، ورود دومرحله‌ای |
| `003_ApplicationCatalog` | همین پوشه | کاتالوگ سامانه‌ها: پرچم‌های نیاز به ورود، دسترسی اینترنتی و عمومی‌بودن سامانه، دسترسی مستقیم کاربر/نقش، سرویس‌های هر سامانه |
| `004_AccessWorkflow` | همین پوشه | درخواست دسترسی، گروه نظارت، صف پیامک |
| `005_PublicPublications` | همین پوشه | پرچم «عمومی» برای مطالب (پرتال بدون لاگین) |
| `006_NationalIdLogin` | همین پوشه | آماده‌سازی ورود با کد ملی و پیامک. این روش خاموش می‌ماند تا مدیر روشنش کند |
| `007_PublicationImages` | همین پوشه | جدول تصویر شاخص مطالب (اسلایدر اخبار) |
| `008_AdminPermissions` | همین پوشه | ثبت ۱۲ مجوز پنل مدیریت و دادن همه به نقش ADMIN ([docs/admin-permissions.md](../../docs/admin-permissions.md)) |
| `009_SsoTickets` | همین پوشه | جدول بلیت‌های یک‌بارمصرف ورود یکپارچه (SSO)؛ کلید و آدرس SSO هر سامانه در تنظیمات امن API است ([docs/sso.md](../../docs/sso.md)) |
| `010_PublicationArchive` | همین پوشه | بایگانی (حذف نرم) مطالب: ستون‌های `IsArchived` و `ArchivedAt`؛ مطلب بایگانی‌شده از پرتال و فهرست پیش‌فرض مدیر پنهان است و قابل بازگردانی می‌ماند |
| `011_UserApplicationAccounts` | همین پوشه | نگاشت کاربر اینترانت به نام کاربری او در هر سامانه برای ورود یکپارچه، وقتی دو نام فرق دارند؛ سامانه‌ها تغییری نمی‌کنند ([docs/sso.md](../../docs/sso.md)) |
| `012_OrganizationChart` | همین پوشه | چارت سازمانی: جدول درختی `Org.Unit`، ستون اختیاری `Sec.User.UnitId` و مجوزهای `ORG_VIEW` و `ORG_EDIT` برای ADMIN؛ داده با `tools/Import-OrganizationChartFromIts.ps1` وارد می‌شود |
| `013_ApplicationSsoSettings` | همین پوشه | مسیر و هش کلید SSO هر سامانه در `App.Application` (`SsoPath`، `SsoKeyHash`، `SsoKeyChangedAt`)؛ از پنل «سامانه‌ها ← ورود یکپارچه» تنظیم می‌شود و دیگر در appsettings نیست ([docs/sso.md](../../docs/sso.md)) |
| `014_ApplicationUnitAccess` | همین پوشه | حالت سوم دسترسی سامانه بر پایه‌ی چارت سازمانی: ستون `App.Application.IsUnitRestricted` و جدول `App.ApplicationUnitAccess`؛ سامانه خودکار برای کاربران واحدهای انتخاب‌شده و زیرواحدهایشان باز می‌شود ([docs/organization-chart.md](../../docs/organization-chart.md)) |
| `015_AdminNotifications` | همین پوشه | اعلان سازمانی ارسالی مدیر از پنل: ستون‌های `IsActive`، `AudienceType`، `AudienceUserId`، `AudienceUnitId`، `RecipientCount`، `ChangedAt` روی `Notify.Notification` و مجوز `NOTIFICATION_SEND` برای ADMIN ([docs/organization-chart.md](../../docs/organization-chart.md)) |
| `016_PublicationSlider` | همین پوشه | کنترل اسلایدر اخبار صفحه‌ی اصلی: ستون‌های `ShowInSlider` و `SlideOrder` روی `Notify.Publication`؛ مطالب موجود مثل قبل همه در اسلایدرند |
| `017_LibraryDocuments` | همین پوشه | کتابخانه‌ی اسناد و فرم‌ها: Schema تازه‌ی `Lib` با `Category` و `Document` (فایل روی دیسک سرور API، مشخصات در بانک)، دسته‌های کاتالوگ قدیمی و مجوز `LIBRARY_EDIT` برای ADMIN ([docs/library.md](../../docs/library.md)) |
| `018_PublicApplications` | همین پوشه | ده سامانه‌ی صفحه‌ی اصلی اینترانت قدیمی (فیش حقوق، اشتراک فایل، درگاه واحد، مبین، فرزین، ورود فایل، جیکامز، اطلاعات مشتریان، کنترل بیجک، دیماند) را با نشانی و توضیح همان صفحه **عمومی** می‌کند؛ سامانه‌ی موجود (با کد) به‌روز و ناموجود (GCOMS و BIJAKCONTROL) ساخته می‌شود |
| `019_HideHomeApplication` | همین پوشه | سامانه‌ی «اینترانت سازمانی» (`INTERANET`) را غیرفعال می‌کند تا کارتش در هیچ فهرستی نیاید؛ مجوزهای پنل و اعلان‌های سازمانی که نام آن را فرستنده دارند دست‌نخورده کار می‌کنند |
| `020_ApplicationImages` | همین پوشه | تصویر کارت سامانه برای پورتال: ستون `ImageUpdatedAt` روی `App.Application` و جدول `App.ApplicationImage` (JPEG/PNG/WebP تا ۲ مگابایت، بایت‌ها جدا از فهرست سامانه‌ها)؛ سامانه‌ی بدون تصویر مثل قبل آیکن را نشان می‌دهد |
| `021_AttendanceApplication` | همین پوشه | «حضور و غیاب» (`ATTENDANCE`، نشانی از صفحه‌ی اینترانت قدیمی، توضیح «سامانه امور کارکنان») را **عمومی** اضافه می‌کند و توضیح کارت دیماند (`DEMAND`) و جیکامز (`GCOMS`) را به متن انتخاب‌شده تغییر می‌دهد؛ تصویر کارت در migration نیست و از پنل بارگذاری می‌شود |

فایل‌های 003 در ۲۰۲۶-۱۰-۲۲ از روی دیتابیس مرجع بازسازی شدند (فایل‌های اصلی هیچ‌وقت commit نشده بودند). 004 قبلاً با نام `tools/sql/003-access-workflow-*` بود و به همین پوشه منتقل شد. `tools/sql/001-publications-*` و `002-publications-integrity-*` اسکریپت‌های قدیمی‌تر جدول مطالب‌اند، در `SchemaMigration` ثبت نمی‌شوند و در خط پایه‌ی نصب تازه آمده‌اند.

## نصب تازه (دیتابیس خالی)

1. یک دیتابیس خالی بسازید (collation پیش‌فرض سرور؛ مرجع `SQL_Latin1_General_CP1_CI_AS` است) و اتصال را مثل مرحله‌ی ۳ بخش «بعد از pull» تنظیم کنید.
2. `000_Baseline.install.sql` را اجرا کنید. ساختار را تا حالت بعد از 002 می‌سازد، 001 و 002 را ثبت می‌کند و فقط ردیف تنظیمات ورود و نقش ADMIN را می‌گذارد. روی دیتابیسی که جدول دارد اجرا نمی‌شود.
3. 003 تا آخرین migration را به ترتیب اجرا کنید (مثل حلقه‌ی مرحله‌ی ۴ بخش «بعد از pull»، با فهرستی که از 003 شروع می‌شود).
4. **اولین مدیر:** نصب تازه هیچ کاربری ندارد و verify 008 بدون مدیر فعال خطای «هیچ کاربر فعالی به پنل دسترسی ندارد» می‌دهد. بعد از اجرای 003 تا 007 و 008 (up) این ابزار را بزنید: `./tools/New-FirstAdmin.ps1 -Username <نام> -Name <نام> -LastName <نام خانوادگی>`؛ رمز را بدون نمایش می‌پرسد (۱۰ تا ۱۲۸ نویسه، فقط هش ذخیره می‌شود)، فقط وقتی هیچ مدیر فعالی نیست کار می‌کند و حساب موجود را ارتقا نمی‌دهد. سپس verify 008 را اجرا کنید. اتصال همان بانکی است که API به آن تنظیم شده است (`appsettings.json`، متغیر محیطی یا User Secrets).

خط پایه یک بار و از روی دیتابیس مرجعِ برگشت‌داده‌شده به 002 تولید شده و با برداشت `docs/database-catalog.md` (ساختار بعد از 002) یکی است. migrationهای بعدی آن را تغییر نمی‌دهند؛ هر تغییر تازه یک migration شماره‌دار است.

آزمون `Database/FreshInstallTests.cs` (اختیاری) همین مسیر را روی یک دیتابیس موقت اجرا می‌کند: خط پایه، همه‌ی migrationها با verify، اجرای دوباره، تطابق با مدل EF و (اگر `INTRANET_SCHEMA_TEST_CONNECTION` هم باشد) تطابق کامل با دیتابیس مرجع، سپس بازگشت تا 002 و نصب دوباره. دیتابیس موقت در پایان حذف می‌شود:

```powershell
$env:INTRANET_FRESH_INSTALL_SERVER = "Server=(localdb)MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True"
dotnet test tests/OrganizationIntranet.Tests --filter "FullyQualifiedName~FreshInstallTests"
```

## بعد از pull

**۱. ببینید دیتابیس شما کدام‌ها را دارد:**

```sql
SELECT MigrationId, AppliedAt FROM dbo.SchemaMigration ORDER BY MigrationId;
```

**۲. backup بگیرید.** `database/backup.sql` یک backup با `COPY_ONLY` و `CHECKSUM` در پوشه‌ی Backup خود SQL Server می‌سازد، آن را بررسی می‌کند و مسیرش را نشان می‌دهد.

**۳. اتصال را تنظیم کنید.** اسکریپت‌ها با `tools/Invoke-DatabaseScript.ps1` اجرا می‌شوند. این ابزار رشته‌ی اتصال را از متغیر محیطی `ConnectionStrings__DefaultConnection` یا از User Secrets برنامه‌ی API می‌خواند. در همان پنجره‌ی PowerShell:

```powershell
$env:ConnectionStrings__DefaultConnection = "Server=<سرور>;Database=OrganizationIntranet;Integrated Security=True;TrustServerCertificate=True"
```

**۴. backup و migrationهای جاافتاده را به ترتیب اجرا کنید.** بعد از هر `up`، `verify` همان را بزنید. مثال برای کسی که 004 را دارد:

```powershell
$run = { param($f) powershell -NoProfile -ExecutionPolicy Bypass -File tools/Invoke-DatabaseScript.ps1 -ScriptPath $f; if ($LASTEXITCODE) { throw "failed: $f" } }
& $run database/backup.sql
foreach ($m in '005_PublicPublications','006_NationalIdLogin','007_PublicationImages','008_AdminPermissions','009_SsoTickets','010_PublicationArchive','011_UserApplicationAccounts','012_OrganizationChart','013_ApplicationSsoSettings','014_ApplicationUnitAccess','015_AdminNotifications','016_PublicationSlider','017_LibraryDocuments','018_PublicApplications','019_HideHomeApplication','020_ApplicationImages','021_AttendanceApplication') {
    & $run "database/migrations/$m.up.sql"
    & $run "database/migrations/$m.verify.sql"
}
```

خروجی هر اجرای موفق «Database script completed.» است. اگر یکی خطا داد، ادامه ندهید: متن خطا می‌گوید کدام پیش‌نیاز یا قید برقرار نیست.

**۵. (اختیاری) آزمون فقط‌خواندنی:**

```powershell
$env:INTRANET_SCHEMA_TEST_CONNECTION = $env:ConnectionStrings__DefaultConnection
dotnet test tests/OrganizationIntranet.Tests --filter "Category=Database"
```

این آزمون‌ها بررسی می‌کنند که همه‌ی migrationها اعمال شده‌اند و ساختار دیتابیس با کد می‌خواند.

## بازگشت

`down` را فقط همراه با برگشت کد به نسخه‌ی قبل اجرا کنید و از آخر به اول: اول 008، بعد 007 و الی آخر. بعضی‌ها وقتی داده‌ی وابسته وجود داشته باشد اجرا نمی‌شوند؛ مثلاً 007 وقتی تصویری ثبت شده باشد. راه مطمئن‌تر، restore از backup مرحله‌ی ۲ است.

## نوشتن migration تازه

1. نام: `NNN_Name.up.sql`، `.down.sql` و `.verify.sql` با شماره‌ی بعدی. `up` باید شناسه‌ی قبلی را در `SchemaMigration` بخواهد و شناسه‌ی خودش را ثبت کند. برای الگو 008 را ببینید.
2. یک batch بدون `GO`، با `SET XACT_ABORT ON`، تراکنش و `sp_getapplock`. متن فارسی با `N'...'` و فایل با UTF-8.
3. شناسه را به جدول بالا و به `ExpectedMigrations` در `tests/OrganizationIntranet.Tests/Database/LiveMigrationStateTests.cs` اضافه کنید. آزمون `MigrationFilesTests` ناهماهنگی را می‌گیرد.
4. پیش از اجرا روی دیتابیس مشترک backup بگیرید.
