using Framework.Authorization;
using HSCrm.DataModel.Services;
using HSCrm.DataModel.Services.Interface;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Framework.Extensions;
using HSCrm.Services.Tools.Interface;
using HSCrm.WebApi.Tools;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();

// Config
var configuration = builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(FileNamesExtensions.AppSettingName)
    .Build();

// Services
builder.Services.AddControllers();

// Database & Identity
builder.Services.AddDbContextService(configuration);
builder.Services.AddIdentityService(configuration);

//DI
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();

builder.Services.AddAutoMapper(typeof(Program));

builder.Services.AddCors(options =>
{
    options.AddPolicy("_myAllowSpecificationOrigins", policy =>
    {
        policy.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed(host => true);
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// seed فقط یکبار
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await DbInitializer.InitializeAsync(services);
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("_myAllowSpecificationOrigins");

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
