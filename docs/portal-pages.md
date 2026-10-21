# صفحه‌های پورتال و اخبار تصویری

## صفحه‌ی اصلی (`/Portal`)

ترتیب بخش‌ها: سربرگ ← **اخبار و اطلاعیه‌ها** ← سامانه‌های من ← کارتابل نظارت ← پیوندهای سریع. کاربر اول اخبار سازمان را می‌بیند.

- **اسلایدر اخبار** (`wwwroot/js/news-carousel.js`): کارت تصویری با نوع، عنوان، خلاصه‌ی دوخطی و تاریخ. هر ۶ ثانیه روی کارت بعدی متمرکز می‌شود. با hover، فوکوس صفحه‌کلید یا پنهان‌بودن تب مکث می‌کند و با `prefers-reduced-motion` خودکار حرکت نمی‌کند. فقط track افقی جابه‌جا می‌شود و صفحه پرش نمی‌کند.
- مطلب بدون تصویر با زمینه‌ی رنگیِ نوع مطلب (خبر، اطلاعیه، بخشنامه) نمایش داده می‌شود.
- مدال «درخواست دسترسی» (کارت سامانه‌های من) فقط فرم را دارد؛ پیگیری درخواست‌ها در `/AccessRequests` است.

## صفحه‌های داخلی (پوسته‌ی `_LayoutPortal`)

| مسیر | محتوا |
|---|---|
| `/Account/Profile` | مشخصات و اطلاعات حساب |
| `/Account/Settings` | تغییر رمز (پس از موفقیت خروج و ورود دوباره)، وضعیت امنیت ورود، خروج |
| `/Account/Security` | راه‌اندازی Google Authenticator |
| `/Notifications` | همه‌ی اعلان‌ها با تب همه/خوانده‌نشده/خوانده‌شده و فیلتر منبع؛ «خواندن همه» |
| `/AccessRequests` | ثبت درخواست دسترسی و پیگیری/لغو درخواست‌های من با تب وضعیت |

هدر مشترک (`ViewComponents/PortalHeaderViewComponent`) شمار اعلان‌های خوانده‌نشده و منوی حساب را دارد؛ موارد منو در `_PortalUserMenuItems` با صفحه‌ی اصلی مشترک‌اند. سبک‌ها: `wwwroot/css/portal-shell.css`.

## تصویر شاخص مطالب

- مدیر در «اطلاع‌رسانی سازمانی ← ویرایش» تصویر JPEG، PNG یا WebP تا ۲ مگابایت بارگذاری یا حذف می‌کند و پیش‌نمایش کارت را می‌بیند.
- نوع فایل در API از روی امضای فایل تشخیص داده می‌شود (SVG و سایر نوع‌ها رد می‌شوند). پاسخ تصویر `nosniff` دارد.
- دید تصویر همان دید مطلب است: ناشناس فقط مطلب عمومی، کاربر واردشده مطلب منتشرشده، مدیر همه (`api/public|api|api/admin/publications/{id}/image`). Portal و Admin تصویر را از API می‌گیرند و به مرورگر می‌دهند (`/Publications/Image/{id}` و `/Admin/Publications/Image?id=`).
- نشانی تصویر نسخه‌دار است (`?v=`)؛ کش خصوصی مرورگر یک روز.

### بانک — migration 007

`Notify.PublicationImage` (کلید و FK به Publication، بدون حذف آبشاری) و ستون `Notify.Publication.ImageUpdatedAt`. پیش از اجرای API جدید روی هر بانک (بعد از 006):

```powershell
pwsh -File tools/Invoke-DatabaseScript.ps1 -ScriptPath database/migrations/007_PublicationImages.up.sql
pwsh -File tools/Invoke-DatabaseScript.ps1 -ScriptPath database/migrations/007_PublicationImages.verify.sql
```

بازگشت (`007_PublicationImages.down.sql`) تا وقتی تصویری ثبت شده باشد اجرا نمی‌شود.

## آزمون‌ها

`Integration/PortalPagesTests.cs` — تشخیص نوع تصویر، دید تصویر، بارگذاری از پنل مدیریت و نمایش در اسلایدر، پوسته‌ی صفحه‌های داخلی، تب‌های اعلان، هدر و منوی پنل مدیریت.
