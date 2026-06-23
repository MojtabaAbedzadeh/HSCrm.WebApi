public interface ICurrentUserService
{
    string? UserId { get; }
    int? TenantId { get; }
    bool IsAuthenticated { get; }
}