using HSCrm.Entities.Entities;

namespace HSCrm.Services.Tools.Interface
{
    public interface IJwtTokenGenerator
    {
        Task<string> CreateTokenAsync(ApplicationUser user);
    }
}
