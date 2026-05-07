using HSCrm.DataModel.Services;
using HSCrm.DataModel.Services.Interface;
using HSCrm.DataModel.Services.Repository;
using HSCrm.Framework.Extensions;
using HSCrm.Services.Tools.Interface;
using HSCrm.WebApi.Tools;



var builder = WebApplication.CreateBuilder(args);

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
builder.Services.AddAutoMapper(typeof(Program));

builder.Services.AddCors(options =>
{
    options.AddPolicy(name: "_myAllowSpecificationOrigins",
        builder =>
        {
            //builder.WithOrigins("https://localhost:7079");
            builder.AllowAnyHeader().AllowAnyMethod().AllowCredentials().SetIsOriginAllowed((host) => true);
        });
});

// Swagger
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseCors("_myAllowSpecificationOrigins");

//«Õ—«“ ÂÊ? 
app.UseAuthentication();
// „ÃÊ“Â«? œ” —”?
app.UseAuthorization();

app.MapControllers();

using (var scope = app.Services.CreateScope())
{
    await DbInitializer.InitializeAsync(scope.ServiceProvider);
}

app.Run();