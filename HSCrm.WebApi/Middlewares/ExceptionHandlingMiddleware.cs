using FluentValidation;
using HSCrm.DataModel.Exceptions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Text.Json;
using ValidationException = FluentValidation.ValidationException;

namespace HSCrm.WebApi.Middlewares;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Unhandled exception occurred. TraceId: {TraceId}, Path: {Path}",
                context.TraceIdentifier,
                context.Request.Path);

            if (context.Response.HasStarted)
            {
                _logger.LogWarning(
                    "Response has already started. Standard error response cannot be written. TraceId: {TraceId}",
                    context.TraceIdentifier);

                throw;
            }

            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, message, additionalData) = MapException(exception);

        var response = new ApiResponse
        {
            Status = false,
            StatusCode = statusCode,
            Message = message,
            AdditionalData = additionalData
        };

        context.Response.Clear();
        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = null,
            WriteIndented = _environment.IsDevelopment()
        };

        await context.Response.WriteAsync(
            JsonSerializer.Serialize(response, jsonOptions),
            context.RequestAborted);
    }

    private static (ApiResultStatusCode StatusCode, string Message, object? AdditionalData) MapException(Exception exception)
    {
        return exception switch
        {
            // خطای اختصاصی کمبود موجودی
            NotEnoughInventoryException inventoryException =>
                (
                    ApiResultStatusCode.NotEnoughInventory,
                    inventoryException.Message,
                    null
                ),

            // FluentValidation
            ValidationException validationException => (
                    ApiResultStatusCode.BadRequest,
                    "اطلاعات ارسالی معتبر نیست.",
                    validationException.Errors
                        .GroupBy(x => x.PropertyName)
                        .ToDictionary(
                            group => group.Key,
                            group => group
                                .Select(x => x.ErrorMessage)
                                .Distinct()
                                .ToList()
                        )
                ),

            // عدم دسترسی
            UnauthorizedAccessException =>
                (
                    ApiResultStatusCode.Unauthorized,
                    "شما مجوز دسترسی به این بخش را ندارید.",
                    null
                ),

            // داده یا رکورد یافت نشد
            KeyNotFoundException keyNotFoundException =>
                (
                    ApiResultStatusCode.NotFound,
                    keyNotFoundException.Message,
                    null
                ),

            // تداخل هم‌زمانی در Entity Framework
            DbUpdateConcurrencyException =>
                (
                    ApiResultStatusCode.Conflict,
                    "اطلاعات توسط کاربر دیگری تغییر کرده است. لطفاً صفحه را به‌روزرسانی کرده و دوباره تلاش کنید.",
                    null
                ),

            // خطاهای پارامتری یا ورودی نامعتبر
            ArgumentException argumentException =>
                (
                    ApiResultStatusCode.BadRequest,
                    argumentException.Message,
                    null
                ),

            // خطای پیش‌بینی‌نشده
            _ =>
                (
                    ApiResultStatusCode.ServerError,
                    "خطای غیرمنتظره‌ای در سرور رخ داد. لطفاً مجدداً تلاش کنید.",
                    null
                )
        };
    }
}
