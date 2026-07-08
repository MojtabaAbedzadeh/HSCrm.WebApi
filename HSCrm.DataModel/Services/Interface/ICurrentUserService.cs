public interface ICurrentUserService
{
    string? UserId { get; }
    int? TenantId { get; }
    int? FiscalYearId { get; }
    bool IsAuthenticated { get; }
}