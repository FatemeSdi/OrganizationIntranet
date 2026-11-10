using DNTCaptcha.Core;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Account;
/// <summary>صفحه‌ی ورود پنل مدیریت (نام کاربری، رمز و CAPTCHA)؛ منطق در LoginPageModel مشترک است و فقط دارندگان مجوز INTRANET_ADMIN وارد می‌شوند.</summary>
public class LoginModel(ApiClient api, IDNTCaptchaValidatorService captcha, IConfiguration configuration) : LoginPageModel(api, captcha, configuration) { }
