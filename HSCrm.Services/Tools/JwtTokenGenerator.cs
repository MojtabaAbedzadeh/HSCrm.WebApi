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
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SymmetricSecurityKey _key;
        string _securityKey;

        public JwtTokenGenerator(IConfiguration config, UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
            _key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["TokenKey"]));
            _securityKey = config["SecurityKey"];
        }

        public async Task<string> CreateTokenAsync(ApplicationUser user)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.NameId, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.UniqueName, user.UserName)
            };

            var roles = await _userManager.GetRolesAsync(user);
            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            //Hash Token SecurityKey Oriented
            var encryptorKey = Encoding.UTF8.GetBytes(_securityKey);
            var encryptorCredential = new EncryptingCredentials(new SymmetricSecurityKey(encryptorKey),
                SecurityAlgorithms.Aes128KW, SecurityAlgorithms.Aes128CbcHmacSha256);
            //

            var credential = new SigningCredentials(_key, SecurityAlgorithms.HmacSha512Signature);
            var tokenDescriber = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(claims),
                Expires = DateTime.Now.AddHours(6),
                SigningCredentials = credential,
                Issuer = "hscrm-api",
                Audience = "hscrm-dashboard",
                EncryptingCredentials = encryptorCredential
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriber);
            return tokenHandler.WriteToken(token);
        }
    }
}
