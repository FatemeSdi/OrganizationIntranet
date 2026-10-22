# OrganizationIntranet.Tests

تنها پروژه‌ی تست سامانه. قاعده‌ی کار: **هر تغییر (قابلیت، رفع باگ، migration، اتصال بیرونی) همراه با تست در همین پروژه ارائه می‌شود.** تغییر بدون تست فقط با دلیل صریح در توضیح همان تغییر پذیرفته است.

## ساختار پوشه‌ها

| پوشه | برچسب `Category` | چه چیزی را آزمون می‌کند | نیازها |
|---|---|---|---|
| `Unit/` | `Unit` | منطق خالص یک کلاس؛ وابستگی‌ها جعلی‌اند | هیچ |
| `Integration/` | `Integration` | رفتار سامانه از ابتدا تا انتها: Portal/Admin ← API ← SQLite درون‌حافظه، و کلاینت HTTP واقعی روی loopback | هیچ (شبکه‌ی داخلی لازم نیست) |
| `Database/` | `Database` | قیدها و یکپارچگی بانک روی SQLite، و بررسی اختیاری و فقط‌خواندنیِ **SQL Server واقعی** | بانک واقعی فقط با `INTRANET_SCHEMA_TEST_CONNECTION` |
| `Support/` | — | ابزار مشترک آزمون: درگاه پیامک شبیه‌سازی‌شده (`FakeSmsProvider`) و غیره | — |

نام فضای نام همه‌ی فایل‌ها `OrganizationIntranet.Tests` است؛ پوشه‌ها فقط برای سازمان‌دهی‌اند.

## اجرا

```powershell
dotnet test tests/OrganizationIntranet.Tests                                   # همه (بانک واقعی رد می‌شود)
dotnet test tests/OrganizationIntranet.Tests --filter "Category=Unit"
dotnet test tests/OrganizationIntranet.Tests --filter "Category=Integration"
dotnet test tests/OrganizationIntranet.Tests --filter "Category=Database"
```

آزمون‌های بانک واقعی اختیاری‌اند، فقط می‌خوانند و بدون متغیر محیطی رد می‌شوند:

```powershell
$env:INTRANET_SCHEMA_TEST_CONNECTION = "<connection string>"
dotnet test tests/OrganizationIntranet.Tests --filter "Category=Database"
Remove-Item Env:INTRANET_SCHEMA_TEST_CONNECTION
```

آزمون نصب تازه (`Database/FreshInstallTests.cs`) هم اختیاری است. یک دیتابیس موقت با نام تصادفی می‌سازد، خط پایه و همه‌ی migrationها را روی آن اجرا می‌کند و در پایان آن را حذف می‌کند. سرور را فقط وقتی بدهید که ساخت و حذف دیتابیس موقت روی آن مجاز است، ترجیحاً LocalDB، نه سرور اصلی:

```powershell
$env:INTRANET_FRESH_INSTALL_SERVER = "Server=(localdb)\MSSQLLocalDB;Integrated Security=True;TrustServerCertificate=True"
dotnet test tests/OrganizationIntranet.Tests --filter "FullyQualifiedName~FreshInstallTests"
Remove-Item Env:INTRANET_FRESH_INSTALL_SERVER
```

اگر `INTRANET_SCHEMA_TEST_CONNECTION` هم تنظیم باشد، ساختار نصب تازه با دیتابیس مرجع هم مقایسه می‌شود. این مقایسه فقط از دیتابیس مرجع می‌خواند.

## قواعد نوشتن تست

1. **پوشه و برچسب:** تست را در پوشه‌ی مناسب بگذارید و روی کلاس `[Trait("Category", "Unit|Integration|Database")]` بگذارید.
2. **هرگز پیامک، ایمیل یا درخواست واقعی به بیرون نفرستید.** برای اتصال بیرونی از handler جعلی یا `FakeSmsProvider` روی loopback استفاده کنید. `ApiFactory` عمداً `Sms:BaseUrl` و `Sms:AppName` را خالی می‌کند و یک آزمون (`Test_host_never_reaches_the_real_gateway`) آن را نگه می‌دارد؛ این را دور نزنید.
3. **امنیت را آزمون کنید، نه فقط مسیر موفق:** دسترسی غیرمجاز، جداسازی داده‌ی کاربران، ورودی بدخواه (مثل `&`، `<script>`)، درخواست تکراری و همزمانی.
4. **بانک واقعی را تغییر ندهید.** آزمون روی بانک واقعی فقط‌خواندنی و اختیاری است. تغییر ساختار بانک باید migration نسخه‌دار با آزمون SQLite و در صورت نیاز یک بررسی اختیاری در `Database/` داشته باشد؛ هنگام افزودن migration، شناسه‌ی آن را به `ExpectedMigrations` در `Database/LiveMigrationStateTests.cs` اضافه کنید.
5. **داده‌ی ساختگی فقط داخل آزمون** است و وارد بانک یا مخزن نمی‌شود.
6. **ظاهر (UI):** آزمون HTTP جایگزین بررسی بصری نیست؛ تغییر UI را در مرورگر هم ببینید و نتیجه را در توضیح تغییر بنویسید.
7. بعد از هر تغییر، کل مجموعه را اجرا کنید و خطاهای قبلی را از خطاهای ناشی از تغییر جدا گزارش دهید.
