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
        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException("JWT Key is missing. Set 'Jwt:Key' in appsettings.json");

        _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public async Task<string> GenerateTokenAsync(ApplicationUser user, long? fiscalYearId = null, CancellationToken ct = default)
    {
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id),
            new Claim(ClaimTypes.Name, user.UserName ?? ""),
            new Claim("UserId", user.Id)
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
        var expiryDays = double.Parse(_config["Jwt:ExpiryInDays"] ?? "7");

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
