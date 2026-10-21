# ورود با کد ملی و پیامک

روش سوم ورود، کنار «نام کاربری و رمز» و «LDAP»، در فهرست «روش ورود» صفحه‌ی ورود پورتال و پنل مدیریت. با انتخاب آن فقط فیلدهای داخل کادر ورود عوض می‌شود (`wwwroot/js/login-form.js`).

## جریان

1. کاربر کد ملی و کد امنیتی (کپچا) را وارد می‌کند ← `POST api/account/national-id/start`.
2. اگر کد ملی دقیقاً به یک کاربر فعالِ قفل‌نشده با شماره همراه معتبر تعلق داشته باشد، کد ۶ رقمی (اعتبار ۵ دقیقه) پیامک می‌شود.
3. کاربر کد را وارد می‌کند ← `POST api/account/national-id/verify` ← توکن‌ها صادر و کوکی UI ساخته می‌شود.
4. ارسال دوباره پس از ۶۰ ثانیه: `POST api/account/national-id/resend`.

الگوی کد یک‌بارمصرف (هش‌شده، محدودیت تلاش، شناسه‌ی چالش) از سامانه‌ی ServiceQualityManagementSystem گرفته شده و روی زیرساخت چالش موجود (`Sec.AuthenticationChallenge`) پیاده شده است.

## تصمیم‌های امنیتی

- **عدم افشا:** پاسخ start و resend برای کد ملی ناموجود، تکراری، غیرفعال، قفل، بدون موبایل یا در دوره‌ی انتظار یکسان است.
- **کد ملی تکراری** بین چند کاربر: ورود مسدود (fail closed). اسکریپت verify تعداد این موارد را گزارش می‌کند.
- اعتبارسنجی رقم کنترل کد ملی؛ ارقام فارسی/عربی پذیرفته می‌شوند.
- کپچا پیش از ارسال پیامک؛ rate limit حساب (`account`)؛ ۵ تلاش اشتباه ⇒ قفل ۱۵ دقیقه‌ای حساب.
- توکن چالش فقط در TempData سمت سرور UI نگه داشته می‌شود و به مرورگر نمی‌رسد.
- با تغییر تنظیمات احراز هویت (Revision) یا SecurityStamp کاربر، چالش‌های باز باطل می‌شوند.
- **یک‌عاملی است** (در اختیار داشتن تلفن) و از ورود دومرحله‌ای عبور نمی‌کند؛ این موضوع در صفحه‌ی «ورود و امنیت» به مدیر گفته می‌شود.

## فعال‌سازی

Admin ← ورود و امنیت ← «روش پیامک» و سپس «کد ملی و پیامک». برای کاربران باید کد ملی و شماره همراه ثبت شده باشد.

## بانک — migration 006

CHECK constraintهای `Sec.AuthenticationChallenge` مقدارهای `otp-login` و `national-id-sms` را می‌پذیرند و ایندکس غیریکتای `IX_User_NationalId` اضافه می‌شود. پیش از اجرای API جدید روی هر بانک:

```powershell
pwsh -File tools/Invoke-DatabaseScript.ps1 -ScriptPath database/migrations/006_NationalIdLogin.up.sql
pwsh -File tools/Invoke-DatabaseScript.ps1 -ScriptPath database/migrations/006_NationalIdLogin.verify.sql
```

بازگشت: ابتدا روش را در Admin خاموش و API جدید را متوقف کنید، سپس `006_NationalIdLogin.down.sql` (چالش‌های بازِ این روش حذف می‌شوند).

## آزمون‌ها

- `Unit/NationalIdTests.cs` — رقم کنترل و نرمال‌سازی ارقام.
- `Integration/AuthenticationTests.cs` — جریان API، عدم افشا، تکراری/غیرفعال/قفل، دوره‌ی انتظار، وابستگی به پیامک.
- `Integration/NationalIdLoginUiTests.cs` — جریان کامل از صفحه‌ی ورود Portal با درگاه پیامک جعلی.
- `Database/AuthenticationDatabaseTests.cs` — constraintهای جدید.
