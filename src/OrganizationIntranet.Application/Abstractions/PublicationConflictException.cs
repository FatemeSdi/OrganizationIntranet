namespace OrganizationIntranet.Application.Abstractions;

/// <summary>تعارض ویرایش هم‌زمان مطلب (Revision قدیمی)؛ در API پاسخ ۴۰۹ می‌شود.</summary>
public sealed class PublicationConflictException : Exception
{
    public PublicationConflictException() : base("این مطلب تغییر کرده است. متن خود را نگه دارید و آخرین نسخه را دوباره باز کنید.") { }
}
