# OrganizationIntranet — راهنمای معماری پروژه

## تصمیم جاری — تفکیک پنج پروژه

طبق درخواست صریح کاربر، معماری فعال به پنج پروژه `OrganizationIntranet.Domain`، `OrganizationIntranet.Application`، `OrganizationIntranet.Api`، `OrganizationIntranet.Portal` و `OrganizationIntranet.Admin` تغییر کرده است. مرجع ساختار، اجرا و قراردادهای جدید: [معماری پنج‌پروژه‌ای](docs/five-project-architecture.md).

Portal و Admin فقط از طریق HTTP API به منطق و داده دسترسی دارند و هیچ ProjectReference به Domain/Application/API یا اتصال دیتابیس ندارند. UIها Razor Pages هستند؛ Controllerهای کاربردی فقط در API قرار دارند. Domain مستقل، Application محل منطق و Interfaceها، و API محل EF Core و پیاده‌سازی زیرساخت است. این معماری لایه‌ای با API مرکزی است و Microservice نیست.

مطالب بعدی این سند، مبنای پیشین و قراردادهای رفتاری/امنیتی را حفظ می‌کنند؛ توصیف ساختار تک‌پروژه‌ای و محل قدیمی فایل‌ها تاریخی است. قواعد امنیت، نقش/مجوز، استقلال سامانه‌های بیرونی و ترتیب امن تغییر Policy همچنان معتبرند. توسعه جدید در `src` انجام شود؛ نسخه ریشه فقط برای مقایسه و بازگشت نگه داشته شده است.

این فایل مرجع دائمی معماری پروژه است. هر تغییری در کد، دیتابیس یا ساختار پوشه‌ها باید با این سند هم‌خوان باشد؛ بدون نیاز واقعی، ساختار زیر را تغییر نده.

## هدف پروژه

`OrganizationIntranet` نقش **Central Portal** سازمان را دارد، نه اینکه منطق Business سامانه‌های دیگر داخل آن نوشته شود.

```
                         Organization Intranet
                                  │
                 ┌────────────────┼────────────────┐
                 │                │                │
          Authentication       Dashboard      Notifications
                 │                │                │
                 └────────────────┼────────────────┘
                                  │
                         Applications
                                  │
              ┌───────────────────┼───────────────────┐
              │                   │                   │
         AccessReport             HR             Other Systems
              │                   │                   │
             API                 API                 API
```

Intranet مسئول: Authentication، Authorization، Users، Roles، Permissions، Applications، Dashboard، Notifications، Navigation، SSO (در آینده)، مدیریت مرکزی دسترسی.
Business Logic هر سامانه (مثل AccessReport یا HR) باید داخل خودش بماند، نه داخل Intranet.

## تکنولوژی

ASP.NET Core 8 (Razor Pages خالص، بدون MVC Controller — به‌جز کنترلر داخلی DNTCaptcha)، Entity Framework Core + SQL Server، Bootstrap RTL (تمپلیت yzen)، JavaScript ساده. ارتباط با سرویس‌های خارجی فقط از طریق API.

## ساختار پوشه‌ها — وضعیت هدف

```
OrganizationIntranet
│
├── Data
│   ├── AppDbContext.cs
│   └── Configurations          ← هنوز ایجاد نشده
│
├── Models
│   ├── Entities                ← هنوز ایجاد نشده (فعلاً همه فلت داخل Models هستند)
│   └── DTOs                    ← هنوز ایجاد نشده
│
├── Services                    ← هنوز ایجاد نشده؛ منطق فعلاً داخل PageModel است
│   ├── Authentication
│   ├── User
│   ├── Role
│   ├── Permission
│   ├── Application
│   └── Notification
│
├── Helper
│
├── Pages
│   ├── Account      (Login, ForgotPassword, Logout, Profile)   ✅ موجود
│   ├── Portal        → معادل «Dashboard» در سند اصلی؛ فعلاً همین نام نگه داشته می‌شود مگر رنیم درخواست شود
│   ├── Applications                                             ← هنوز ایجاد نشده
│   ├── Notifications                                            ← هنوز ایجاد نشده
│   ├── Admin
│   │   └── Index, Users, Roles, Applications, Permissions        ✅ موجود
│   └── Shared
│
└── wwwroot
```

## معماری دیتابیس — وضعیت هدف در برابر وضعیت فعلی

دیتابیس: `OrganizationIntranet`، سه Schema: `Sec`, `App`, `Notify`.

```
Sec.User ──< Sec.UserRole >── Sec.Role ──< Sec.RolePermission >── Sec.Permission >── App.Application
Sec.User ──< Sec.UserPermission >── Sec.Permission        (مسیر فرعی/مستقیم — نگاه کن به یادداشت زیر)
App.Application ──< Notify.Notification ──< Notify.UserNotification >── Sec.User
```

| جدول | وضعیت | یادداشت |
|---|---|---|
| `Sec.User` | ✅ پیاده‌سازی شده | دقیقاً منطبق با سند |
| `Sec.Role` | ✅ پیاده‌سازی شده | |
| `Sec.UserRole` | ✅ پیاده‌سازی شده | |
| `Sec.Permission` | ✅ پیاده‌سازی شده | وابسته به `Application` |
| `Sec.RolePermission` | ✅ جدول + Entity + EF Config + صفحه‌ی مدیریت (`Admin/Permissions`) + زیرساخت Authorization (`Authorization/*`) + Permission Claims در Login | همه‌ی صفحه‌ها و endpointهای پنل مدیریت با `[Authorize(Policy="Permission:...")]` کنترل می‌شوند؛ مجوزهای پایه با migration 008 ثبت شده‌اند (نگاه کن به بخش Authorization) |
| `Sec.UserPermission` | ✅ در دیتابیس هست، ولی **در هیچ‌جای کد استفاده نمی‌شود** (فقط در Models/AppDbContext تعریف شده) | طبق سند: قبل از حذف باید وابستگی‌ها بررسی و مزایا/معایب نگه‌داشتن توضیح داده شود — هنوز این بررسی/تصمیم نهایی انجام نشده |
| `App.Application` | ✅ پیاده‌سازی شده | |
| `Notify.Notification` | ✅ پیاده‌سازی شده | ستون `TargetUrl` (nvarchar(500), nullable) اضافه شد؛ هنوز هیچ Notification ای واقعی آن را پر نمی‌کند چون صفحه/سرویس Notification هنوز ساخته نشده |
| `Notify.UserNotification` | ✅ پیاده‌سازی شده | |

## Authorization — وضعیت فعلی

مجوزدهی پنل مدیریت **Permission-based است** (P4-09، ۲۰۲۶-۱۰-۲۱). مرجع کامل (ماتریس مجوز/صفحه/endpoint، سازوکار و migration): [docs/admin-permissions.md](docs/admin-permissions.md).

- کدها در `shared/Contracts/AdminPermissions.cs` (`INTRANET_ADMIN`, `USER_VIEW/EDIT`, `ROLE_VIEW/EDIT`, `PERMISSION_VIEW/EDIT`, `APPLICATION_VIEW/EDIT`, `ACCESS_REQUEST_REVIEW`, `PUBLICATION_EDIT`, `AUTH_SETTINGS_EDIT`). صفحه‌ها و کنترلرها `[Authorize(Policy = AdminPermissions.XPolicy)]` دارند؛ برای مجوزدهی پنل از `Roles = "ADMIN"` استفاده نمی‌شود. POST صفحه‌ها با `PostRequiresPermission` مجوز ویرایش می‌خواهد.
- زیرساخت Policy (`PermissionPolicyProvider`, `PermissionRequirement`, Claims handler) در `shared/Authorization` و مشترک میان API، Admin و Portal است.
- **API مجوز را زنده از بانک می‌خواند** (`Api/Security/LivePermissions`) و `AdminClientMiddleware` شرط `INTRANET_ADMIN` زنده را دارد؛ پس گرفتن مجوز فوراً اثر دارد.
- **UI مجوزها را در Claim کوکی نگه می‌دارد و هر ۵ دقیقه از `api/account/me` تازه می‌کند** (`UiHost`/`AccountSignIn`، Claim `claims_synced`). تصمیم قبلی «بارگذاری فقط هنگام Login» کنار گذاشته شد؛ بعد از تغییر مجوز یا نقش، Logout/Login لازم نیست. نمایش منو/دکمه‌ها با `User.HasPermission(...)` فقط برای راحتی است.
- **جلوگیری از قفل‌شدن (در `IntranetService`):** نقش ADMIN و مجوزهای سیستمی غیرفعال نمی‌شوند، مجوز سیستمی از ADMIN گرفته نمی‌شود و آخرین مدیر فعال نقش ADMIN را از دست نمی‌دهد.
- migration `008_AdminPermissions` مجوزها را ثبت و به ADMIN می‌دهد؛ روی بانک توسعه اعمال و verify شد (backup `OrganizationIntranet_before_008_20261021_1628.bak`). هر مجوز سیستمی تازه باید هم به `AdminPermissions.All` و هم با migration به بانک اضافه شود.
- هشدار درخواست دسترسی به دارندگان `ACCESS_REQUEST_REVIEW` می‌رسد (`SupervisionQueries`)، نه لزوماً نقش ADMIN.

## قوانین معماری — الزامی در همه تغییرات آینده

1. بدون نیاز واقعی، ساختار پروژه را تغییر نده.
2. Business Logic را داخل Razor PageModel ننویس؛ وقتی Service Layer ساخته شد، منطق باید از PageModel به Service منتقل شود.
3. Query‌های پیچیده داخل UI/PageModel نباشند.
4. Authentication/Authorization از UI جدا بماند.
5. Role و Permission را قاطی نکن — Role سطح کلی، Permission سطح دقیق دسترسی به یک Application خاص است.
6. Admin بودن را هرگز با فلگ `IsAdmin` پیاده‌سازی نکن؛ همیشه از Role/Permission.
7. Applicationها مستقل از Intranet بمانند — منطق AccessReport/HR و... داخل این پروژه نوشته نشود.
8. Notification باید بتواند از Applicationهای دیگر Event/Notification دریافت کند (معماری فعلی این را با `ApplicationId` روی `Notification` پشتیبانی می‌کند).
9. همه‌ی Foreign Key و Index‌های لازم مشخص و استاندارد باشند.
10. نام‌گذاری Entity/Service/DTO یکدست باشد.
11. قبل از حذف هر جدول/ستون/کلاس، همه‌ی وابستگی‌های آن در کد و دیتابیس بررسی شود.
12. تغییرات دیتابیس باید Migration-پذیر و Rollback-پذیر باشند.
13. امنیت هرگز فقط به نمایش/عدم‌نمایش چیزی در UI متکی نباشد؛ همیشه در سرور هم چک شود.
14. هر Application باید بتواند Permissionهای مخصوص خودش را داشته باشد (`Permission.ApplicationId`).
15. **هر تغییر همراه با تست ارائه شود** (تصمیم صریح کاربر، ۲۰۲۶-۱۰-۲۰): قابلیت، رفع باگ، migration یا اتصال بیرونی بدون تست در `tests/OrganizationIntranet.Tests` کامل شمرده نمی‌شود. پوشه‌بندی، برچسب‌ها و قواعد نوشتن تست در [tests/README.md](tests/README.md) است؛ بعد از هر تغییر `dotnet test tests/OrganizationIntranet.Tests` اجرا و نتیجه‌ی واقعی گزارش شود.
16. آزمون‌ها هرگز نباید پیامک یا درخواست واقعی به بیرون بفرستند یا بانک واقعی را تغییر دهند؛ بررسی بانک واقعی فقط‌خواندنی و اختیاری است.

## روند کار پیشنهادی برای هر مرحله‌ی مهاجرت

هر مرحله باید شامل این ترتیب باشد: ۱) بررسی وضعیت فعلی، ۲) توضیح تغییرات لازم، ۳) کد کامل، ۴) مسیر دقیق فایل‌های جدید، ۵) اسکریپت SQL/Migration در صورت نیاز، ۶) بررسی وابستگی قبل از حذف چیزی، ۷) عدم ایجاد تغییر غیرضروری، ۸) اگر بین چند راهکار تردید هست، ابتدا مزایا/معایب و بعد پیشنهاد.
