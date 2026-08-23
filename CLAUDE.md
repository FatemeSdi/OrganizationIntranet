# OrganizationIntranet — راهنمای معماری پروژه

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
│   │   ├── Index, Users, Roles, Applications                    ✅ موجود
│   │   └── Permissions                                          ← هنوز ایجاد نشده
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
| `Sec.RolePermission` | ❌ **هنوز وجود ندارد** | مسیر اصلی Authorization طبق سند («User → Role → Permission») هنوز در دیتابیس ساخته نشده |
| `Sec.UserPermission` | ✅ در دیتابیس هست، ولی **در هیچ‌جای کد استفاده نمی‌شود** (فقط در Models/AppDbContext تعریف شده) | طبق سند: قبل از حذف باید وابستگی‌ها بررسی و مزایا/معایب نگه‌داشتن توضیح داده شود — هنوز این بررسی/تصمیم نهایی انجام نشده |
| `App.Application` | ✅ پیاده‌سازی شده | |
| `Notify.Notification` | ✅ پیاده‌سازی شده | ستون پیشنهادی `TargetUrl` هنوز اضافه نشده |
| `Notify.UserNotification` | ✅ پیاده‌سازی شده | |

## Authorization — وضعیت فعلی

فعلاً همه‌جا از `[Authorize(Roles = "ADMIN")]` استفاده می‌شود (Role-based، نه Permission-based). این با قانون «Admin بودن را با IsAdmin flag پیاده نکن» در تضاد نیست (چون از Role/RoleCode استفاده شده، نه یک Flag خام)، ولی هنوز به مدل کامل هدف «User → Role → RolePermission → Permission → Application» نرسیده. رسیدن به این مدل نیازمند ساخت `Sec.RolePermission` و تغییر Authorization Handlerها به بررسی Permission Code است.

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

## روند کار پیشنهادی برای هر مرحله‌ی مهاجرت

هر مرحله باید شامل این ترتیب باشد: ۱) بررسی وضعیت فعلی، ۲) توضیح تغییرات لازم، ۳) کد کامل، ۴) مسیر دقیق فایل‌های جدید، ۵) اسکریپت SQL/Migration در صورت نیاز، ۶) بررسی وابستگی قبل از حذف چیزی، ۷) عدم ایجاد تغییر غیرضروری، ۸) اگر بین چند راهکار تردید هست، ابتدا مزایا/معایب و بعد پیشنهاد.
