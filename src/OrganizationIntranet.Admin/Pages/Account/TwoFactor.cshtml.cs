using OrganizationIntranet.UI;
namespace OrganizationIntranet.Pages.Account;
/// <summary>مرحله‌ی دوم ورود پنل (کد Authenticator یا پیامک)؛ منطق در TwoFactorPageModel مشترک است.</summary>
public class TwoFactorModel(ApiClient api, IConfiguration configuration) : TwoFactorPageModel(api, configuration) { }
