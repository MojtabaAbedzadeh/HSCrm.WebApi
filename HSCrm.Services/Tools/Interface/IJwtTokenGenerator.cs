using HSCrm.Entities.Entities;

namespace HSCrm.Services.Tools.Interface
{
    public interface IJwtTokenGenerator
    {
        Task<string> GenerateTokenAsync(ApplicationUser user, long? fiscalYearId = null, CancellationToken ct = default);
    }
}
