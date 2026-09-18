namespace OrganizationIntranet.Application.Abstractions;

public sealed class PublicationConflictException : Exception
{
    public PublicationConflictException() : base("این مطلب تغییر کرده است. متن خود را نگه دارید و آخرین نسخه را دوباره باز کنید.") { }
}
