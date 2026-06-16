using HSCrm.Entities.Entities;

namespace HSCrm.Services.Tools.Interface
{
    public interface IJwtTokenGenerator
    {
        Task<string> GenerateTokenAsync(ApplicationUser user, CancellationToken ct = default);
    }
}
