# مجوزهای پنل مدیریت (Permission-based)

دسترسی به پنل مدیریت و هر بخش آن با مجوز (Permission) کنترل می‌شود، نه با نام نقش. نقش `ADMIN` همه‌ی مجوزها را دارد؛ برای واگذاری بخشی از کار (مثلاً فقط بررسی درخواست‌های دسترسی) یک نقش تازه بسازید و فقط همان مجوزها را در «مجوزها ← اختصاص» به آن بدهید.

فهرست کدها: `shared/Contracts/AdminPermissions.cs`. همه زیر سامانه‌ی اینترانت (`ApplicationCode = "INTERANET"`، همان کدی که در بانک ثبت است) تعریف شده‌اند.

## ماتریس مجوز

| کد | نام | صفحه‌ها (Admin) | API |
|---|---|---|---|
| `INTRANET_ADMIN` | ورود به پنل مدیریت | ورود به Admin، داشبورد، پروفایل | شرط همه‌ی `api/admin/*` (در `AdminClientMiddleware` و Policy کلاس کنترلرها) |
| `USER_VIEW` | مشاهده‌ی کاربران | `Users/Index`، `Users/Edit` (GET)؛ همچنین `Applications/Edit` و `Applications/Supervision` چون فهرست کاربران را نشان می‌دهند | GET کاربران؛ «آخرین کاربران» داشبورد |
| `USER_EDIT` | ویرایش کاربران | POST در `Users/Edit` | POST کاربران |
| `ROLE_VIEW` | مشاهده‌ی نقش‌ها | `Roles/Index`، `Roles/Edit` (GET؛ فهرست اعضا فقط با `USER_VIEW`) | `GET roles/{id}` (فهرست نقش‌ها برای فرم‌ها فقط `INTRANET_ADMIN` می‌خواهد) |
| `ROLE_EDIT` | ویرایش نقش‌ها | POST در `Roles/Index` و `Roles/Edit` | ساخت، ویرایش نام/کد، toggle و حذف نقش |
| `PERMISSION_VIEW` | مشاهده‌ی مجوزها | `Permissions/Index`، `Permissions/Assign` | GET مجوزها |
| `PERMISSION_EDIT` | ویرایش و اختصاص مجوزها | POST در همان دو صفحه | ساخت، toggle و اختصاص مجوز به نقش |
| `APPLICATION_VIEW` | مشاهده‌ی سامانه‌ها | `Applications/Index`، `Edit`، `Import`، `Supervision` | GET یک سامانه، پیش‌نمایش import، گروه نظارت (GET) |
| `APPLICATION_EDIT` | ویرایش سامانه‌ها و دسترسی آن‌ها | POST در همان صفحه‌ها | ساخت/ویرایش/toggle سامانه، ثبت import، گروه نظارت (POST) |
| `ACCESS_REQUEST_REVIEW` | بررسی درخواست‌های دسترسی | `AccessRequests/Index`، `AccessRequests/Notifications`، زنگ اعلان هدر | `api/admin/access-requests/*`، `api/admin/supervision/sms` (وضعیت و ارسال دوباره‌ی پیامک) |
| `PUBLICATION_EDIT` | مدیریت اطلاع‌رسانی سازمانی | `Publications/*` | `api/admin/publications/*` |
| `AUTH_SETTINGS_EDIT` | تنظیمات ورود و امنیت | `Authentication/Index` | `api/admin/authentication` |

منو، کارت‌های داشبورد و «دسترسی سریع» فقط مواردی را نشان می‌دهند که کاربر مجوزش را دارد؛ بدون مجوز ویرایش، دکمه‌های ثبت پنهان و سوییچ‌ها غیرفعال‌اند. این فقط برای راحتی است؛ همه‌ی قواعد در سرور (صفحه و API) بررسی می‌شوند.

هشدار «درخواست دسترسی جدید» در کارتابل نظارت به کاربرانی می‌رسد که `ACCESS_REQUEST_REVIEW` را از راه یک نقش فعال دارند (`SupervisionQueries`)، نه لزوماً نقش ADMIN.

## سازوکار

- **Policy:** `[Authorize(Policy = AdminPermissions.UserViewPolicy)]` (یعنی `Permission:USER_VIEW`). `PermissionPolicyProvider` برای هر نام با پیشوند `Permission:` یک `PermissionRequirement` می‌سازد. کدها در `shared/Authorization` و مشترک میان API، Admin و Portal‌اند.
- **API — بررسی زنده:** `LivePermissionAuthorizationHandler` و `LivePermissions` (`src/OrganizationIntranet.Api/Security`) مجوز را در هر درخواست از بانک می‌خوانند (فقط نقش فعال و مجوز فعال) و در همان درخواست cache می‌کنند. پس گرفتن مجوز یا غیرفعال‌شدن کاربر فوراً اثر دارد، حتی با توکن قدیمی.
- **UI — Claimها:** Admin و Portal مجوزها را در کوکی به‌صورت Claim نگه می‌دارند. `UiHost` در `OnValidatePrincipal` هر ۵ دقیقه (`claims_synced`) آن‌ها را از `api/account/me` تازه می‌کند؛ اگر کاربر دیگر معتبر نباشد (۴۰۱/۴۰۴) از حساب خارج می‌شود. بعد از تغییر مجوز Logout/Login لازم نیست.
- **POST:** `Admin/Authorization/PostRequiresPermissionAttribute` روی PageModelها برای هر درخواست غیر GET مجوز ویرایش را می‌خواهد و در غیر این صورت ۴۰۳ می‌دهد.
- **ورود:** ورود به Admin فقط با `INTRANET_ADMIN` ممکن است. کلید داخلی Admin (`AdminClientKey`) فقط برای کاربری که این مجوز را دارد فرستاده می‌شود.

## جلوگیری از قفل‌شدن پنل

`IntranetService` این کارها را رد می‌کند:

- غیرفعال‌کردن نقش `ADMIN`؛
- تغییر کد یا حذف نقش‌های سیستمی `ADMIN` و `EXPERT` (برنامه آن‌ها را با کد پیدا می‌کند؛ `AdminPermissions.IsSystemRole`)؛
- غیرفعال‌کردن یکی از مجوزهای سیستمی بالا؛
- گرفتن یک مجوز سیستمی از نقش `ADMIN` (در صفحه‌ی اختصاص، این گزینه قفل است)؛
- ذخیره‌ی کاربری که آخرین مدیر فعال است، اگر نقش ADMIN را از دست بدهد یا غیرفعال شود.

## migration 008

`database/migrations/008_AdminPermissions.*.sql` مجوزهای بالا را می‌سازد (یا فعال می‌کند) و به نقش ADMIN می‌دهد. پیش‌نیاز: 007 و نقش فعال ADMIN. اگر کدی زیر سامانه‌ی دیگری ثبت شده باشد، اجرا متوقف می‌شود.

```bash
pwsh -File tools/Invoke-DatabaseScript.ps1 -ScriptPath database/migrations/008_AdminPermissions.up.sql
pwsh -File tools/Invoke-DatabaseScript.ps1 -ScriptPath database/migrations/008_AdminPermissions.verify.sql
```

verify بررسی می‌کند که همه‌ی کدها فعال‌اند، ADMIN همه را دارد و دست‌کم یک کاربر فعال به پنل دسترسی دارد. بازگشت (`008_AdminPermissions.down.sql`) فقط همراه با برگشت به نسخه‌ی قبلی کد معنا دارد؛ با کد فعلی پنل را برای همه می‌بندد.

روی بانک توسعه در ۲۰۲۶-۱۰-۲۱ اعمال شد (backup: `OrganizationIntranet_before_008_20261021_1628.bak`).

**افزودن مجوز تازه:** کد را به `AdminPermissions` (و فهرست `All`) اضافه کنید، یک migration تازه برای ثبت آن و اختصاصش به ADMIN بنویسید و شناسه‌اش را در `LiveMigrationStateTests` بیاورید.

## آزمون‌ها

- `Integration/AdminPermissionTests.cs`: محدوده‌ی هر مجوز در API، اثر فوری پس گرفتن مجوز، قواعد جلوگیری از قفل‌شدن، صفحه‌ها و منو، رد POST بدون مجوز ویرایش، تازه‌شدن Claimها.
- `Database/LiveMigrationStateTests.cs` (اختیاری، فقط‌خواندنی): 008 اعمال شده و ADMIN همه‌ی کدهای `AdminPermissions` را دارد.

## مدیریت نقش‌ها

- فهرست نقش‌ها شمار کاربران و مجوزهای هر نقش را نشان می‌دهد. صفحه‌ی `Roles/Edit` جزئیات نقش را دارد: نام و کد، اعضا با پیوند به کاربر، و مجوزها با پیوند به اختصاص.
- کد نقش یکتا است و فقط حروف بزرگ انگلیسی، رقم و `_` دارد. ورودی کوچک به بزرگ تبدیل می‌شود. کد تکراری با پیام روشن رد می‌شود (۴۰۰).
- حذف فقط برای نقشی ممکن است که سیستمی نباشد و نه کاربر داشته باشد، نه مجوز، نه دسترسی مستقیم سامانه یا عضویت در گروه نظارت. قید FK بانک (NO ACTION) هم در همزمانی جلوی حذف را می‌گیرد. برای قطع دسترسی نقشی که در حال استفاده است، آن را غیرفعال کنید.
- آزمون‌ها: `Integration/RoleManagementTests.cs`.
