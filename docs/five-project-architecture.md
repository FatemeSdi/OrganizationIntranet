# معماری پنج‌پروژه‌ای

این سند تصمیم جاری کاربر برای تفکیک پروژه را ثبت می‌کند و بر بخش ساختار تک‌پروژه‌ای سند قدیمی مقدم است. هدف حفظ عملکرد فعلی Portal و Admin با دسترسی UI صرفاً از طریق HTTP API است. این معماری لایه‌ای با API مرکزی است، نه Microservice.

| پروژه | نوع | مسئولیت و وابستگی |
|---|---|---|
| OrganizationIntranet.Domain | Class Library، net8.0 | Entityها؛ بدون وابستگی پروژه‌ای |
| OrganizationIntranet.Application | Class Library، net8.0 | منطق کاربردی، قراردادها و Interfaceهای Repository/Password؛ وابسته به Domain |
| OrganizationIntranet.Api | ASP.NET Core Web API، net8.0 | Controller، احراز هویت و مجوز، EF Core/SQL Server و پیاده‌سازی Repository؛ وابسته به Application |
| OrganizationIntranet.Portal | ASP.NET Core Razor Pages، net8.0 | Portal، Account، قالب و ViewModel؛ بدون ProjectReference و بدون دیتابیس |
| OrganizationIntranet.Admin | ASP.NET Core Razor Pages، net8.0 | Admin، Account، قالب و ViewModel؛ بدون ProjectReference و بدون دیتابیس |

## مرزها و قراردادهای حفظ‌شده

- UI فقط با `ApiClient` سمت سرور به API درخواست می‌فرستد. مرورگر به SQL Server یا سرویس‌های Application دسترسی مستقیم ندارد.
- `shared/Contracts` قراردادهای انتقال داده مستقل از Domain است که با Compile Link در Application و UI کامپایل می‌شود؛ پروژه ششم محصول ایجاد نشده است. تغییر قرارداد باید با هر سه مصرف‌کننده بررسی شود.
- `shared/UI` کد کلاینت HTTP، راه‌اندازی مشترک و مدیریت خطای UI را نگه می‌دارد. فایل‌های صفحات هر UI در پروژه همان UI هستند.
- `wwwroot` ریشه، منبع مشترک assetهای فعلی است؛ MSBuild آن‌ها را به خروجی build و publish هر UI کپی می‌کند. نسخه منتشرشده وابسته به پوشه سورس نیست.
- Entityها در `Domain/Entities`، منطق در `Application/Services` و EF در `Api/Data` هستند. DTOهای API شامل PasswordHash یا navigation graph نیستند.
- مسیرهای `/Portal`، `/Admin`، `/Admin/Users`، `/Admin/Roles`، `/Admin/Applications`، `/Admin/Permissions` و `/Account/*` حفظ شده‌اند. لینک‌های بین دو میزبان از `Sites:Portal` و `Sites:Admin` خوانده می‌شوند.
- ورود، خروج، RememberMe، پروفایل، تغییر رمز، بازیابی رمز، اطلاعات کاربر پورتال، آمار مدیریت، ایجاد/ویرایش کاربر و نقش‌ها، ایجاد/تغییر وضعیت سامانه و مجوز و انتساب نقش به مجوز منتقل شده‌اند.
- احراز هویت API از Bearer Token محافظت‌شده ASP.NET Core استفاده می‌کند؛ JWT عمومی نیست. کلید محافظت API باید در استقرار پایدار بماند.
- API مستقلاً `[Authorize(Roles = "ADMIN")]` دارد؛ پنهان‌سازی UI جایگزین این کنترل نیست. زیرساخت Permission Policy حفظ شده؛ تغییر Admin به Permission Policy یا Seed انجام نشده است.
- نقش‌ها و مجوزها هنگام ورود ثبت می‌شوند. مانند رفتار قبلی، تغییر آن‌ها نیازمند ورود مجدد است. سیاست نقش غیرفعال، فعال‌سازی اولیه حساب بدون رمز و بازیابی رمز با اطلاعات هویتی، موضوعات امنیتی قبلی‌اند و این مهاجرت ادعای رفع آن‌ها ندارد.
- کارت‌ها و اعلان‌های نمونه پورتال همان رفتار قبلی را دارند؛ این تغییر، اتصال واقعی سامانه‌های بیرونی یا SSO آن‌ها را پیاده نمی‌کند.
- ساختار جدول‌ها، Schemaها، FKها و نگاشت زمانی EF حفظ شده‌اند. هیچ Migration، Seed یا تغییر داده در دیتابیس فعلی اجرا نمی‌شود.
- فایل‌های نسخه قبلی در ریشه برای مقایسه و بازگشت نگه داشته شده‌اند؛ پروژه قدیمی از Solution خارج شده و `src/shared/tests` را کامپایل نمی‌کند. توسعه جاری فقط در پنج پروژه جدید انجام شود.

## اجرای محلی

پیش‌نیاز: .NET SDK سازگار با net8.0، دسترسی API به SQL Server فعلی و گواهی توسعه HTTPS معتبر و مورد اعتماد.

```powershell
pwsh -File tools/Initialize-Development.ps1 -UseExistingConnection
dotnet build OrganizationIntranet.sln
dotnet run --project src/OrganizationIntranet.Api --launch-profile https
dotnet run --project src/OrganizationIntranet.Portal --launch-profile https
dotnet run --project src/OrganizationIntranet.Admin --launch-profile https
```

سه دستور run در ترمینال‌های جدا اجرا شوند. در Visual Studio نیز Api، Portal و Admin را به‌عنوان Multiple Startup Projects با Action=Start انتخاب کنید. Domain و Application کتابخانه‌اند و اجرا نمی‌شوند.

| برنامه | آدرس توسعه |
|---|---|
| Portal | https://localhost:7230 |
| Admin | https://localhost:7231 |
| API | https://localhost:7232/api |

اسکریپت راه‌اندازی PowerShell 7، یک کلید تصادفی مشترک تولید می‌کند و در User Secrets سه برنامه ذخیره می‌کند. اگر کلید API از قبل باشد آن را حفظ می‌کند. سوییچ `UseExistingConnection`، ConnectionString فعلی را بدون نمایش در خروجی به User Secrets API منتقل می‌کند؛ تنظیم موجود ریشه حذف یا اصلاح نمی‌شود. اجرای مجدد بدون این سوییچ ConnectionString ذخیره‌شده را حفظ می‌کند.

API و UI بدون `Api:ClientKey` معتبر شروع نمی‌شوند. این کلید فقط برای اعتماد بین سرورهای UI و API است و جایگزین Bearer Token و نقش کاربر نیست. کلید به JavaScript/HTML ارسال نمی‌شود؛ همه درخواست‌های API از جمله ورود باید آن را داشته باشند تا مسیر ورود مستقیم نتواند مرز CAPTCHA رابط کاربری را دور بزند. CAPTCHA ورود و Antiforgery فرم‌های Razor حفظ شده‌اند. روی عملیات حساب API محدودیت نرخ هم وجود دارد.

## نشست و استقرار

### Swagger در محیط توسعه

آدرس `https://localhost:7232/swagger` رابط مستندات و آزمایش API است. در محیط Development، مسیر ریشه API نیز به Swagger هدایت می‌شود؛ در Production مستندات فعال نیستند. این تنظیم از الگوی [Swashbuckle برای ASP.NET Core](https://learn.microsoft.com/en-us/aspnet/core/tutorials/getting-started-with-swashbuckle?view=aspnetcore-8.0) پیروی می‌کند.

در دکمه Authorize، مقدار `Api:ClientKey` از User Secrets پروژه API را در ClientKey وارد کنید. سپس `POST /api/account/login` را با حساب آزمایشی اجرا کنید و مقدار `accessToken` پاسخ را در Bearer وارد کنید (بدون پیشوند Bearer). عملیات مدیریت همچنان نقش ADMIN می‌خواهد. هیچ کلیدی در مستندات یا HTML درج نمی‌شود؛ Swagger مقدار واردشده را برای درخواست‌های همان نشست توسعه استفاده می‌کند. عملیات Try it out واقعاً اجرا می‌شود و از دیتابیس تنظیم‌شده API استفاده می‌کند.

عنوان صفحه ورود Admin «پنل مدیریت اینترانت سازمان» و عنوان صفحه ورود Portal «پورتال داخلی سازمان» است.

- نشست UI یک Cookie امن و HttpOnly مشترک است. توکن دسترسی و تمدید درون Cookie محافظت‌شده قرار می‌گیرند و در LocalStorage نیستند. گزینه RememberMe ماندگاری Cookie پس از بستن مرورگر را تعیین می‌کند. عمر ۱۴روزه و تمدید لغزان پیش‌فرض Cookie قبلی حفظ شده‌اند؛ UI پس از عبور از نیمه عمر، توکن API را نیز با همان Claims زمان ورود تمدید می‌کند. شکست تمدید باعث خروج می‌شود و نشست UI فراتر از اعتبار API تمدید نمی‌شود.
- اشتراک نشست محلی با نام Cookie، Authentication Scheme و ApplicationName یکسان و مسیر کلید مشترک `%LOCALAPPDATA%/OrganizationIntranet/UIKeys` انجام می‌شود. ورود مدیر در Portal به Admin هدایت می‌شود و خروج، Cookie مشترک را حذف می‌کند.
- در استقرار، `Api:ClientKey` و `ConnectionStrings:DefaultConnection` را در Secret Store یا Environment Variables تنظیم کنید؛ User Secrets فقط برای توسعه است. فقط API به ConnectionString نیاز دارد.
- `Api:BaseUrl` باید HTTPS باشد. `Sites:Portal` و `Sites:Admin` باید به آدرس واقعی دو UI اشاره کنند. API را فقط برای سرورهای مجاز UI در شبکه قابل دسترسی کنید.
- برای UIهای روی زیردامنه‌های یک دامنه، `Ui:CookieDomain` را روی دامنه مشترک و `Ui:DataProtectionKeysPath` را روی مخزن کلید مشترک با دسترسی محدود حساب سرویس قرار دهید. کلیدهای Data Protection API نیز باید پایدار و حفاظت‌شده باشند. اشتراک Cookie روی دو دامنه نامرتبط کار نمی‌کند؛ چنین استقراری نیازمند تصمیم مستقل SSO است.
- با انتقال از نسخه قدیمی، کاربران یک بار دوباره وارد می‌شوند؛ Cookie نسخه قدیمی به نشست جدید تبدیل نمی‌شود.

## بررسی

```powershell
dotnet test tests/OrganizationIntranet.SmokeTests/OrganizationIntranet.SmokeTests.csproj
```

پروژه آزمون عمداً خارج از Solution محصول است تا Solution شامل همان پنج پروژه درخواستی بماند. آزمون‌ها API واقعی، Application و Repository را با SQLite موقت در حافظه و هر دو میزبان Razor با TestServer اجرا می‌کنند. این تست‌ها داده SQL Server فعلی را تغییر نمی‌دهند و جایگزین آزمون پذیرش با داده عملیاتی و بررسی بصری مرورگر نیستند.

### نتیجه بررسی این تغییر

- Build پنج پروژه: موفق، بدون خطا و هشدار.
- ۸ آزمون یکپارچه: موفق؛ شامل عملیات مدیریت، تغییر/بازیابی رمز، منع دسترسی API، بارگذاری صفحات هر دو UI، فرم دارای Antiforgery، نشست مشترک، ورود موفق، خروج و تمدید/رد نشست.
- در آزمون ورود موفق فقط نتیجه CAPTCHA جایگزین آزمایشی دارد؛ آزمون جداگانه با Validator واقعی، ورود بدون CAPTCHA را رد می‌کند. تشخیص تصویر CAPTCHA در مرورگر خودکار آزموده نشده است.
- Publish مستقل هر سه میزبان: موفق. اجرای فایل‌های منتشرشده، صفحه ورود Portal و Admin را با HTTP 200 و API فاقد کلید/توکن را با HTTP 401 پاسخ داد. assetهای CSS در خروجی موجودند و هیچ اسمبلی EF Core در خروجی UIها نیست.
- SQL Server فعلی، اعتبار حساب‌های عملیاتی و نمای بصری در مرورگر در این آزمون‌ها تأیید نشده‌اند. هیچ Seed یا Migration و هیچ تغییری در داده عملیاتی اجرا نشده است.
- درخواست بعدی تفکیک پورتال اینترنتی/اینترانتی توسط کاربر لغو شد؛ Solution همچنان یک Portal و همان پنج پروژه را دارد. ثبت سرویس‌ها و اعلان‌های تعاملی به این تغییر اضافه نشده‌اند.
