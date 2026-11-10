using DNTCaptcha.Core;
using OrganizationIntranet.UI;
namespace OrganizationIntranet.Pages.Account;
/// <summary>بازیابی رمز با پیامک در پورتال؛ منطق در ForgotPasswordPageModel مشترک است.</summary>
public class ForgotPasswordModel(ApiClient api, IDNTCaptchaValidatorService captcha) : ForgotPasswordPageModel(api, captcha) { }
