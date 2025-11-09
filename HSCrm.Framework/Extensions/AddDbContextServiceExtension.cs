using HSCrm.DataModel.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace HSCrm.Framework.Extensions
{
    public static class AddDbContextServiceExtension
    {
        public static IServiceCollection AddDbContextService(this IServiceCollection services, IConfiguration config)
        {
            services.AddDbContext<ApplicationDbContext>(option =>
            {
                option.UseSqlServer(config.GetConnectionString("HSCrmConnectionString"));
            });

            return services;
        }
    }
}
