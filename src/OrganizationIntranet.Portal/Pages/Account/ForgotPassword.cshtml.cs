using DNTCaptcha.Core;
using OrganizationIntranet.UI;
namespace OrganizationIntranet.Pages.Account;
public class ForgotPasswordModel(ApiClient api, IDNTCaptchaValidatorService captcha) : ForgotPasswordPageModel(api, captcha) { }
