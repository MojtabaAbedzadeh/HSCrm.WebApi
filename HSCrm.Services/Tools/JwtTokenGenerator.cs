// HSCrm.Services/Tools/JwtTokenGenerator.cs

using HSCrm.Entities.Entities;
using HSCrm.Services.Tools.Interface;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

public class JwtTokenGenerator : IJwtTokenGenerator
{
    private readonly IConfiguration _config;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SymmetricSecurityKey _signingKey;

    public JwtTokenGenerator(IConfiguration config, UserManager<ApplicationUser> userManager)
    {
        _config = config;
        _userManager = userManager;
        var key = _config["Jwt:Key"];
        if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
            throw new InvalidOperationException("JWT signing key must be configured through Jwt__Key and contain at least 32 bytes.");

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public async Task<string> GenerateTokenAsync(ApplicationUser user, long? fiscalYearId = null, CancellationToken ct = default)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? ""),
            new Claim("UserId", user.Id),
            new Claim("SecurityStamp", user.SecurityStamp ?? string.Empty)
        };

        if (user.TenantId > 0)
            claims.Add(new Claim("TenantId", user.TenantId.ToString()));

        if (fiscalYearId.HasValue)
            claims.Add(new Claim("FiscalYearId", fiscalYearId.Value.ToString()));

        var roles = await _userManager.GetRolesAsync(user);
        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var issuer = _config["Jwt:Issuer"];
        var audience = _config["Jwt:Audience"];
        var expiryDays = double.TryParse(_config["Jwt:ExpiryInDays"], System.Globalization.NumberStyles.Number, System.Globalization.CultureInfo.InvariantCulture, out var configuredExpiryDays) ? Math.Clamp(configuredExpiryDays, 0.01, 1) : 1;

        var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddDays(expiryDays),
            signingCredentials: creds
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
