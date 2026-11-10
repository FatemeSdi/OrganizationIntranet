using DNTCaptcha.Core;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Account;
/// <summary>صفحه‌ی ورود پورتال (نام کاربری، رمز و CAPTCHA)؛ منطق در LoginPageModel مشترک است. پس از ورود کاربر (حتی مدیر) به پورتال برمی‌گردد.</summary>
public class LoginModel(ApiClient api, IDNTCaptchaValidatorService captcha, IConfiguration configuration) : LoginPageModel(api, captcha, configuration) { }
