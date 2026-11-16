namespace Sales.Api.Security;

public static class UploadLimits
{
    public const int MaxFileBytes = 5 * 1024 * 1024;
    public const int MaxRequestBytes = MaxFileBytes + 64 * 1024;
    public const string ConcurrencyPolicy = "sales-upload";
}
