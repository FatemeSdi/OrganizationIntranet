# قراردادهای UI پنل مدیریت

پنل مدیریت روی تمپلیت yzen می‌ماند؛ لایه‌ی `wwwroot/assets/css/admin-ui.css` فقط از متغیرهای خود yzen استفاده می‌کند تا حالت روشن/تیره کار کند و `wwwroot/assets/js/admin-ui.js` رفتارهای مشترک را با data-attribute فعال می‌کند (بدون JavaScript هم صفحه کار می‌کند).

## سربرگ صفحه

`_LayoutAdmin` برای هر صفحه سربرگ یکسان (آیکن، مسیر، عنوان، توضیح، دکمه‌ها) می‌سازد. صفحه فقط مقدار می‌دهد:

```cshtml
ViewData["Title"] = "کاربران";
ViewData["Icon"] = "ti ti-users";
ViewData["Subtitle"] = "...";            // اختیاری
ViewData["ParentTitle"] = "سامانه‌ها";     // اختیاری، برای صفحه‌های فرعی
ViewData["ParentUrl"] = "/Admin/Applications";
@section PageActions { <a class="btn btn-primary">...</a> }
```

صفحه‌ای که سربرگ اختصاصی دارد `ViewData["HidePageHead"] = true` می‌گذارد. breadcrumb یا `<h1>` جداگانه در صفحه ننویسید.

## اجزا

| جزء | کاربرد |
|---|---|
| `_ToastMessages` | پیام نتیجه‌ی عملیات (toast تمپلیت yzen) |
| `_EmptyState` (`EmptyState`) | حالت خالی فهرست با اقدام پیشنهادی |
| `_PickList` (`PickList`) | انتخاب چندگانه‌ی قابل جست‌وجو به‌جای `select multiple`؛ همان نام فیلد را ارسال می‌کند |
| `.admin-toolbar` + `data-filter-scope` | جست‌وجو/فیلتر سمت کاربر برای فهرست‌های کامل (`data-filter="search"` یا کلید دلخواه، ردیف‌ها `data-row`) |
| `.table-admin`, `.entity`, `.status-pill`, `.code-chip` | جدول‌ها |
| `.item-card` | فهرست محتوا و درخواست‌ها |
| `.form-section`, `.switch-row`, `.form-actions` | فرم‌های بخش‌بندی‌شده با نوار ذخیره‌ی چسبان |

## رفتارها

- `data-autosubmit` روی سوییچ وضعیت؛ `data-confirm-off` پیش از غیرفعال‌کردن تأیید می‌گیرد.
- `data-confirm` روی دکمه‌ی ارسال.
- `form[data-busy]` جلوی ارسال دوباره را می‌گیرد؛ `form[data-dirty-guard]` تغییرات ذخیره‌نشده را هشدار می‌دهد.
- `[data-counter][maxlength]` شمارنده‌ی کاراکتر.

تأیید سمت کاربر فقط برای راحتی است؛ همه‌ی قواعد در API بررسی می‌شوند. آزمون: `Integration/AdminRedesignTests.cs`.

## هدر و منو

- زنگ اعلان (`Components/AccessRequestBell`) مطابق `notifications-dropdown` تمپلیت yzen است، با آیکن SVG هم‌شکل آیکن تم و جداکننده‌ی `header-link`؛ در همه‌ی صفحه‌های پوسته (از جمله پروفایل) از `api/portal/supervision/alerts` خوانده می‌شود.
- دکمه‌ی جمع‌کردن منو کلاس `hor-toggle` تمپلیت را دارد تا در صفحه‌ی بزرگ زیر منوی کناری پنهان نشود.
- ترتیب گروه‌های منو: سامانه‌ها و محتوا، کاربران و دسترسی، تنظیمات.
- ستون عملیات جدول‌ها عنوان «عملیات» دارد.
- جستجوی پیشرفته‌ی کاربران: فیلدهای جدا با `data-filter-mode="contains"` و برچسب فیلترهای فعال (`data-filter-label`، `[data-filter-chips]`).
- نمای بندر داشبورد (`_PortIllustration`) درون‌خطی است؛ در تم تیره خورشید به ماه و ستاره تبدیل می‌شود.

## مجوزها

هر صفحه و endpoint پنل مجوز خودش را دارد و منو و دکمه‌ها بر اساس مجوز نمایش داده می‌شوند: [admin-permissions.md](admin-permissions.md).

## صفحه‌ی خطا

`Pages/Error.cshtml` برای ۴۰۳، ۴۰۴ و ۵۰۰ با کارت yzen روی پس‌زمینه‌ی صفحه‌ی ورود؛ بدون منو و زنگ اعلان، پس به API وابسته نیست. کاربر واردشده دکمه‌ی «بازگشت به داشبورد» و در ۴۰۳ «ورود با حساب دیگر» می‌بیند. جزئیات: [portal-pages.md](portal-pages.md#صفحههای-خطا).
