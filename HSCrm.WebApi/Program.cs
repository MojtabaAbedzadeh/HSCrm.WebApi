using Framework.Authorization;
using FluentValidation;
using HSCrm.DataModel.Services;
using HSCrm.DataModel.Services.Interface;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using HSCrm.Services.Tools.Interface;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// ۱. دسترسی به HttpContext
builder.Services.AddHttpContextAccessor();

// ۲. لود تنظیمات AppSettings
var configuration = builder.Configuration
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(FileNamesExtensions.AppSettingName)
    .Build();

// ۳. کنترلرها و ثبت FluentValidation
builder.Services.AddControllers();

// ثبت Validatorها به روش مدرن و استاندارد DI از اسمبلی جاری
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

// استانداردسازی خطاهای ModelState با ساختار ApiResponse پروژه
builder.Services.Configure<ApiBehaviorOptions>(options =>
{
    options.InvalidModelStateResponseFactory = context =>
    {
        var validationErrors = context.ModelState
            .Where(x => x.Value is not null && x.Value.Errors.Count > 0)
            .ToDictionary(
                x => x.Key,
                x => x.Value!.Errors
                    .Select(error => !string.IsNullOrWhiteSpace(error.ErrorMessage)
                        ? error.ErrorMessage
                        : "مقدار وارد شده معتبر نیست.")
                    .ToList()
            );

        var apiResponse = new ApiResponse
        {
            Status = false,
            StatusCode = ApiResultStatusCode.BadRequest,
            Message = "اعتبارسنجی مقادیر ورودی با خطا مواجه شد.",
            AdditionalData = validationErrors
        };

        return new BadRequestObjectResult(apiResponse);
    };
});

// ۴. سرویس‌های اصلی پروژه (DbContext و Identity)
builder.Services.AddDbContextService(configuration);
builder.Services.AddIdentityService(configuration);

// ۵. تزریق وابستگی‌ها (Dependency Injection)
builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
builder.Services.AddScoped<IJwtTokenGenerator, JwtTokenGenerator>();
builder.Services.AddScoped<IInventoryService, InventoryService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddScoped<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddScoped<IRoleService, RoleService>();
builder.Services.AddScoped<ICurrentUserService, CurrentUserService>();
builder.Services.AddScoped<ICurrentFiscalYearService, CurrentFiscalYearService>();
builder.Services.AddScoped<IPaymentSettlementService, PaymentSettlementService>();
builder.Services.AddScoped<IAccountingService, AccountingService>();


// ۶. مپ کردن پروفایل‌های AutoMapper
builder.Services.AddAutoMapper(typeof(Program));

// ۷. تنظیمات CORS بر اساس پیکربندی موجود در پروژه شما
builder.Services.AddCors(options =>
{
    options.AddPolicy("_myAllowSpecificationOrigins", policy =>
    {
        policy.AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()
              .SetIsOriginAllowed(host => true);
    });
});

// ۸. مستندسازی Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// ۹. مقداردهی اولیه و Seed کردن پایگاه داده
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    await DbInitializer.InitializeAsync(services);
}

// ۱۰. لوله‌های پردازش میان‌افزارها (Pipeline Middleware)
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