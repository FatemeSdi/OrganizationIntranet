using DNTCaptcha.Core;
using OrganizationIntranet.UI;

namespace OrganizationIntranet.Pages.Account;
public class LoginModel(ApiClient api, IDNTCaptchaValidatorService captcha, IConfiguration configuration) : LoginPageModel(api, captcha, configuration) { }
