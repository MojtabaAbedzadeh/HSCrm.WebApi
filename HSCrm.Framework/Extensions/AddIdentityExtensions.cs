using HSCrm.DataModel.Context;
using HSCrm.Entities.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace HSCrm.Framework.Extensions
{
    public static class AddIdentityExtention
    {
        public static IServiceCollection AddIdentityService(
            this IServiceCollection service,
            IConfiguration config)
        {
            service.AddIdentity<ApplicationUser, ApplicationRole>(opt =>
            {
                // Password policy
                opt.Password.RequireDigit = true;
                opt.Password.RequireUppercase = true;
                opt.Password.RequireLowercase = true;
                opt.Password.RequireNonAlphanumeric = true;
                opt.Password.RequiredLength = 8;
                opt.Password.RequiredUniqueChars = 1;

                // User policy
                opt.User.RequireUniqueEmail = true;

                // Lockout policy
                opt.Lockout.AllowedForNewUsers = true;
                opt.Lockout.MaxFailedAccessAttempts = 5;
                opt.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            var jwtSettings = config.GetSection("Jwt");
            var key = jwtSettings["Key"];
            if (string.IsNullOrWhiteSpace(key) || Encoding.UTF8.GetByteCount(key) < 32)
                throw new InvalidOperationException("JWT signing key must be configured through Jwt__Key and contain at least 32 bytes.");
            if (string.IsNullOrWhiteSpace(jwtSettings["Issuer"]) || string.IsNullOrWhiteSpace(jwtSettings["Audience"]))
                throw new InvalidOperationException("JWT issuer and audience must be configured.");

            service.AddAuthentication(options =>
            {
                options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            })
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, // فعال شد
                    ValidateAudience = true, // فعال شد
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwtSettings["Issuer"],
                    ValidAudience = jwtSettings["Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
                    ClockSkew = TimeSpan.Zero // حذف تاخیر زمانی پیش‌فرض برای دقت بیشتر در Expiry
                };
            });

            return service;
        }
    }
}