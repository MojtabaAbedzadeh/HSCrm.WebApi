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
            var connectionString = config.GetConnectionString("HSCrmConnectionString");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("Database connection is not configured. Set ConnectionStrings__HSCrmConnectionString in the deployment environment.");

            services.AddDbContext<ApplicationDbContext>(option =>
            {
                option.UseSqlServer(connectionString);
            });

            return services;
        }
    }
}
