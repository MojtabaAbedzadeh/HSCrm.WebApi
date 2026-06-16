using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using HSCrm.Entities.Entities;
using HSCrm.Services.Tools.Interface;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace HSCrm.WebApi.Tools
{
    public class JwtTokenGenerator : IJwtTokenGenerator
    {
        private readonly IConfiguration _config;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SymmetricSecurityKey _signingKey;
        string _securityKey;

        public JwtTokenGenerator(IConfiguration config, UserManager<ApplicationUser> userManager)
        {
            _config = config;
            _userManager = userManager;
            var key = _config["Jwt:Key"] ?? _config["TokenKey"];
            if (string.IsNullOrWhiteSpace(key))
                throw new InvalidOperationException("JWT Key is missing. Set 'Jwt:Key' or 'TokenKey' in appsettings.json");

            _signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        }

        public async Task<string> GenerateTokenAsync(ApplicationUser user, CancellationToken ct = default)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim(ClaimTypes.Name, user.UserName ?? "")
            };

            var roles = await _userManager.GetRolesAsync(user);
            foreach (var role in roles)
                claims.Add(new Claim(ClaimTypes.Role, role));

            var issuer = _config["Jwt:Issuer"] ?? "Automation";
            var audience = _config["Jwt:Audience"] ?? "Automation";

            var creds = new SigningCredentials(_signingKey, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddDays(7),
                signingCredentials: creds
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
