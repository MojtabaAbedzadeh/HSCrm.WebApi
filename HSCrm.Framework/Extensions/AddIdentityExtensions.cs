using HSCrm.DataModel.Context;
using HSCrm.Entities.Entities;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
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
                    ClockSkew = TimeSpan.Zero
                };

                // Re-check account, tenant and fiscal-year state on every authenticated request.
                // This makes password/security-stamp changes and account deactivation revoke JWTs immediately.
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var principal = context.Principal;
                        var userId = principal?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                        var tokenStamp = principal?.FindFirst("SecurityStamp")?.Value;
                        var tenantClaim = principal?.FindFirst("TenantId")?.Value;

                        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(tokenStamp)
                            || !int.TryParse(tenantClaim, out var tokenTenantId) || tokenTenantId <= 0)
                        {
                            context.Fail("Invalid account context.");
                            return;
                        }

                        var services = context.HttpContext.RequestServices;
                        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
                        var db = services.GetRequiredService<ApplicationDbContext>();
                        var user = await userManager.FindByIdAsync(userId);

                        if (user == null || !user.UserStatus || user.TenantId != tokenTenantId
                            || !string.Equals(user.SecurityStamp, tokenStamp, StringComparison.Ordinal))
                        {
                            context.Fail("Account credentials have changed or the account is inactive.");
                            return;
                        }

                        var tenantIsActive = await db.Tenants.IgnoreQueryFilters()
                            .AnyAsync(t => t.Id == tokenTenantId && t.IsActive && !t.IsDeleted, context.HttpContext.RequestAborted);
                        if (!tenantIsActive)
                        {
                            context.Fail("Tenant is inactive.");
                            return;
                        }

                        var fiscalYearClaim = principal?.FindFirst("FiscalYearId")?.Value;
                        if (!string.IsNullOrWhiteSpace(fiscalYearClaim))
                        {
                            if (!int.TryParse(fiscalYearClaim, out var fiscalYearId) || fiscalYearId <= 0)
                            {
                                context.Fail("Invalid fiscal-year context.");
                                return;
                            }

                            var fiscalYearIsValid = await db.FiscalYears.IgnoreQueryFilters()
                                .AnyAsync(f => f.Id == fiscalYearId && f.TenantId == tokenTenantId && !f.IsClosed,
                                    context.HttpContext.RequestAborted);
                            if (!fiscalYearIsValid)
                                context.Fail("Fiscal year is invalid or closed.");
                        }
                    }
                };
            });

            return service;
        }
    }
}