using Microsoft.AspNetCore.Authorization;
using Common.Permissions;
using Framework.Authorization;
using HSCrm.Common.PublicTools;
using HSCrm.DataModel.Services.Interface;
using HSCrm.Entities;
using HSCrm.Entities.Entities;
using HSCrm.Entities.Models;
using HSCrm.Framework.Extensions;
using HSCrm.Framework.StandardApiResult;
using Microsoft.AspNetCore.Mvc;
using SinaPharm.Common;
using System.Security.Claims;
using static HSCrm.Common.PublicTools.Enums;
using static HSCrm.Common.PublicTools.StringCleaner;

namespace HSCrm.WebApi.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class TenantController : ControllerBase
    {
        private const long MaxLogoSizeInBytes = 2 * 1024 * 1024;
        private static readonly HashSet<string> AllowedLogoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png", ".jpg", ".jpeg"
        };
        private static readonly HashSet<string> AllowedLogoMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/png", "image/jpeg"
        };
        private static readonly HashSet<string> AllowedNumberingModes = new(StringComparer.OrdinalIgnoreCase)
        {
            "Auto", "Manual", "Hybrid"
        };

        private readonly IUnitOfWork _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IWebHostEnvironment? _hosting;
        private readonly IInvoiceNumberGenerator? _invoiceNumberGenerator;

        public TenantController(
            IUnitOfWork context,
            IHttpContextAccessor httpContextAccessor,
            IWebHostEnvironment? hosting = null,
            IInvoiceNumberGenerator? invoiceNumberGenerator = null)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _hosting = hosting;
            _invoiceNumberGenerator = invoiceNumberGenerator;
        }

        [HttpGet("GetTenant")]
        [Permission(Permissions.Tenants.View)]
        public ApiResponse<IEnumerable<Tenant>> Get()
        {
            var tenants = _context.tenantUW.Get();

            return new ApiResponse<IEnumerable<Tenant>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = tenants
            };
        }

        [HttpGet("GetById")]
        [Produces("application/json")]
        [ProducesResponseType(typeof(Tenant), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Tenants.View)]
        public ApiResponse GetById(int tenantId)
        {
            var tenants = _context.tenantUW.GetById(tenantId);

            if (tenants == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name),
            };

            return new ApiResponse<Tenant>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = tenants
            };
        }

        [HttpPost("CreateTenant")]
        [Permission(Permissions.Tenants.Create)]
        public ApiResponse Create([FromForm] TenantCreateModel model)
        {
            if (!ModelState.IsValid) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getBusiness = _context.tenantUW.Get(b => b.TenantTitle == model.TenantTitle);

            if (getBusiness.Any()) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Conflict,
                Message = ApiResultStatusCode.Conflict.DisplayNameAttribute(DisplayProperty.Name)
            };

            try
            {
                Tenant tenant = new Tenant()
                {
                    TenantTitle = model.TenantTitle,
                    CompanyPhone = model.CompanyPhone,
                    Address = model.Address,
                    CompanyEmail = model.CompanyEmail,
                    IsActive = model.IsActive,
                    IsDeleted = false,
                    LogoUrl = model.LogoUrl,
                    TenantCode = TenantCodeGenerator.Generate(model.TenantTitle),
                    CreatedAt = DateTime.UtcNow,
                    CreatedById = User.GetUserId(),
                    Currency = model.Currency
                };

                _context.tenantUW.Create(tenant);
                _context.Save();

                return new ApiResponse<Tenant>
                {
                    Status = true,
                    StatusCode = ApiResultStatusCode.Success,
                    Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                    Data = tenant
                };
            }
            catch (Exception)
            {
                return new ApiResponse
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.ServerError,
                    Message = ApiResultStatusCode.ServerError.DisplayNameAttribute(DisplayProperty.Name)
                };
            }
        }

        [HttpPut("UpdateTenant")]
        [ProducesResponseType(typeof(Tenant), StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [Permission(Permissions.Tenants.Edit)]
        public ApiResponse Update([FromForm] TenantEditModel model)
        {
            if (model.TenantId == 0) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.BadRequest,
                Message = ApiResultStatusCode.BadRequest.DisplayNameAttribute(DisplayProperty.Name)
            };

            var getTenant = _context.tenantUW.GetById(model.TenantId);
            if (getTenant == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = "شرکت مورد نظر یافت نشد."
            };

            getTenant.TenantTitle = model.TenantTitle;
            getTenant.CompanyPhone = model.CompanyPhone;
            getTenant.CompanyEmail = model.CompanyEmail;
            getTenant.Address = model.Address;
            if (!string.IsNullOrWhiteSpace(model.LogoUrl))
                getTenant.LogoUrl = model.LogoUrl;
            getTenant.IsActive = model.IsActive;
            getTenant.ModifiedAt = DateTime.UtcNow;
            getTenant.ModifiedById = User.GetUserId();

            _context.tenantUW.Update(getTenant);
            _context.Save();

            return new ApiResponse<Tenant>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = getTenant
            };
        }

        [HttpGet("TenantDropdownList")]
        public ApiResponse TenantDropdownList()
        {
            IEnumerable<DropdownDto> tenantList = _context.tenantUW.Get().Select(w => new DropdownDto
            {
                DrId = w.Id,
                DrName = w.TenantTitle,
            });

            return new ApiResponse<IEnumerable<DropdownDto>>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = tenantList
            };
        }

        [HttpGet("GetCompanySettings")]
        public ApiResponse<CompanySettingsDto> GetCompanySettings()
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue) return new ApiResponse<CompanySettingsDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
            };

            var tenant = _context.tenantUW.GetById(tenantId.Value);
            if (tenant == null) return new ApiResponse<CompanySettingsDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = "شرکت مورد نظر یافت نشد."
            };

            var salesPattern = string.IsNullOrWhiteSpace(tenant.SalesInvoicePattern) ? "INV-{YEAR}-{SEQ:4}" : tenant.SalesInvoicePattern;
            var purchasePattern = string.IsNullOrWhiteSpace(tenant.PurchaseInvoicePattern) ? "PINV-{YEAR}-{SEQ:4}" : tenant.PurchaseInvoicePattern;
            var salesReturnPattern = string.IsNullOrWhiteSpace(tenant.SalesReturnPattern) ? "SRI-{YEAR}-{SEQ:4}" : tenant.SalesReturnPattern;
            var purchaseReturnPattern = string.IsNullOrWhiteSpace(tenant.PurchaseReturnPattern) ? "PRI-{YEAR}-{SEQ:4}" : tenant.PurchaseReturnPattern;
            var now = DateTime.UtcNow;

            var dto = new CompanySettingsDto
            {
                TenantId = tenant.Id,
                TenantTitle = tenant.TenantTitle,
                TenantCode = tenant.TenantCode ?? string.Empty,
                Currency = tenant.Currency ?? "IRR",
                TaxId = tenant.TaxId,
                CompanyPhone = tenant.CompanyPhone,
                CompanyEmail = tenant.CompanyEmail,
                Address = tenant.Address,
                LogoUrl = tenant.LogoUrl,
                InvoiceNumberingMode = string.IsNullOrWhiteSpace(tenant.InvoiceNumberingMode) ? "Hybrid" : tenant.InvoiceNumberingMode,
                SalesInvoicePattern = salesPattern,
                PurchaseInvoicePattern = purchasePattern,
                SalesReturnPattern = salesReturnPattern,
                PurchaseReturnPattern = purchaseReturnPattern,
                SalesInvoicePreview = _invoiceNumberGenerator?.FormatNumber(salesPattern, "INV", 1, now, tenant.TenantCode),
                PurchaseInvoicePreview = _invoiceNumberGenerator?.FormatNumber(purchasePattern, "PINV", 1, now, tenant.TenantCode),
                SalesReturnPreview = _invoiceNumberGenerator?.FormatNumber(salesReturnPattern, "SRI", 1, now, tenant.TenantCode),
                PurchaseReturnPreview = _invoiceNumberGenerator?.FormatNumber(purchaseReturnPattern, "PRI", 1, now, tenant.TenantCode),
                DefaultVatPercent = tenant.DefaultVatPercent,
                InvoiceFooterText = tenant.InvoiceFooterText,
                DefaultCostingMethod = Enum.IsDefined(typeof(InventoryCostingMethod), tenant.DefaultCostingMethod)
                    ? tenant.DefaultCostingMethod
                    : InventoryCostingMethod.WeightedAverage,
                DocumentLockDate = tenant.DocumentLockDate,
                DocumentLockDateShamsi = tenant.DocumentLockDate.HasValue
                    ? ConvertDate.ConvertMiladiToShamsiShort(tenant.DocumentLockDate.Value)
                    : null
            };

            return new ApiResponse<CompanySettingsDto>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = dto
            };
        }

        [HttpPut("UpdateCompanySettings")]
        [Permission(Permissions.Tenants.Edit)]
        public ApiResponse<CompanySettingsDto> UpdateCompanySettings([FromBody] CompanySettingsUpdateModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.TenantTitle))
                return new ApiResponse<CompanySettingsDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "عنوان شرکت الزامی است."
                };

            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue) return new ApiResponse<CompanySettingsDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
            };

            var tenant = _context.tenantUW.GetById(tenantId.Value);
            if (tenant == null) return new ApiResponse<CompanySettingsDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = "شرکت مورد نظر یافت نشد."
            };

            var mode = string.IsNullOrWhiteSpace(model.InvoiceNumberingMode)
                ? "Hybrid"
                : model.InvoiceNumberingMode.Trim();
            if (!AllowedNumberingModes.Contains(mode))
                return new ApiResponse<CompanySettingsDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "حالت شماره‌گذاری نامعتبر است. مقادیر مجاز: Auto، Manual، Hybrid."
                };

            if (model.DefaultVatPercent < 0 || model.DefaultVatPercent > 100)
                return new ApiResponse<CompanySettingsDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "درصد ارزش افزوده پیش‌فرض باید بین ۰ تا ۱۰۰ باشد."
                };

            if (!Enum.IsDefined(typeof(InventoryCostingMethod), model.DefaultCostingMethod))
                return new ApiResponse<CompanySettingsDto>
                {
                    Status = false,
                    StatusCode = ApiResultStatusCode.BadRequest,
                    Message = "روش قیمت‌گذاری انبار نامعتبر است."
                };

            DateTime? parsedLockDate = null;
            if (!string.IsNullOrWhiteSpace(model.DocumentLockDate))
            {
                var rawDate = model.DocumentLockDate.Trim().Fa2En();
                var isLikelyShamsi = System.Text.RegularExpressions.Regex.IsMatch(rawDate, @"^1[34]\d{2}[/-]\d{1,2}[/-]\d{1,2}$");
                if (isLikelyShamsi)
                {
                    try
                    {
                        parsedLockDate = ConvertDate.ConvertShamsiToMiladi(rawDate.Replace('-', '/')).Date;
                    }
                    catch
                    {
                        return new ApiResponse<CompanySettingsDto>
                        {
                            Status = false,
                            StatusCode = ApiResultStatusCode.BadRequest,
                            Message = "فرمت تاریخ قفل اسناد نامعتبر است (مثال: 1404/01/01)."
                        };
                    }
                }
                else if (DateTime.TryParse(rawDate, out var gregDate))
                {
                    parsedLockDate = gregDate.Date;
                }
                else
                {
                    try
                    {
                        parsedLockDate = ConvertDate.ConvertShamsiToMiladi(rawDate).Date;
                    }
                    catch
                    {
                        return new ApiResponse<CompanySettingsDto>
                        {
                            Status = false,
                            StatusCode = ApiResultStatusCode.BadRequest,
                            Message = "فرمت تاریخ قفل اسناد نامعتبر است (مثال: 1404/01/01)."
                        };
                    }
                }
            }

            tenant.TenantTitle = model.TenantTitle.Trim();
            if (string.IsNullOrWhiteSpace(tenant.TenantCode))
                tenant.TenantCode = TenantCodeGenerator.Generate(tenant.TenantTitle);

            tenant.Currency = string.IsNullOrWhiteSpace(model.Currency) ? "IRR" : model.Currency.Trim();
            tenant.TaxId = model.TaxId?.Trim();
            tenant.CompanyPhone = model.CompanyPhone?.Trim();
            tenant.CompanyEmail = model.CompanyEmail?.Trim();
            tenant.Address = model.Address?.Trim();
            if (model.LogoUrl != null)
                tenant.LogoUrl = string.IsNullOrWhiteSpace(model.LogoUrl) ? null : model.LogoUrl.Trim();

            tenant.InvoiceNumberingMode = AllowedNumberingModes.First(m => m.Equals(mode, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(model.SalesInvoicePattern))
                tenant.SalesInvoicePattern = model.SalesInvoicePattern.Trim();
            if (!string.IsNullOrWhiteSpace(model.PurchaseInvoicePattern))
                tenant.PurchaseInvoicePattern = model.PurchaseInvoicePattern.Trim();
            if (!string.IsNullOrWhiteSpace(model.SalesReturnPattern))
                tenant.SalesReturnPattern = model.SalesReturnPattern.Trim();
            if (!string.IsNullOrWhiteSpace(model.PurchaseReturnPattern))
                tenant.PurchaseReturnPattern = model.PurchaseReturnPattern.Trim();

            tenant.DefaultVatPercent = model.DefaultVatPercent;
            tenant.InvoiceFooterText = model.InvoiceFooterText?.Trim();
            tenant.DefaultCostingMethod = model.DefaultCostingMethod;
            tenant.DocumentLockDate = parsedLockDate;
            tenant.ModifiedAt = DateTime.UtcNow;
            tenant.ModifiedById = User.GetUserId();

            _context.tenantUW.Update(tenant);
            _context.Save();

            return GetCompanySettings();
        }

        [HttpPost("UploadCompanyLogo")]
        [Permission(Permissions.Tenants.Edit)]
        public async Task<IActionResult> UploadCompanyLogo([FromForm] IFormFile? file)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue)
                return Unauthorized(new { status = false, statusCode = 401, message = "عدم احراز هویت." });

            var tenant = _context.tenantUW.GetById(tenantId.Value);
            if (tenant == null)
                return NotFound(new { status = false, statusCode = 404, message = "شرکت مورد نظر یافت نشد." });

            if (file == null || file.Length <= 0)
                return BadRequest(new { status = false, statusCode = 400, message = "لطفاً فایل لوگوی شرکت را انتخاب کنید." });

            if (file.Length > MaxLogoSizeInBytes)
                return BadRequest(new { status = false, statusCode = 400, message = "حجم لوگو نباید بیشتر از ۲ مگابایت باشد." });

            var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
            if (string.IsNullOrEmpty(ext) || !AllowedLogoExtensions.Contains(ext))
                return BadRequest(new { status = false, statusCode = 415, message = "فرمت تصویر مجاز نیست. فقط png/jpg/jpeg." });

            if (!string.IsNullOrWhiteSpace(file.ContentType) && !AllowedLogoMimeTypes.Contains(file.ContentType))
                return BadRequest(new { status = false, statusCode = 415, message = "نوع رسانه‌ای فایل مجاز نیست." });

            var webRoot = _hosting?.WebRootPath
                ?? (_hosting != null ? Path.Combine(_hosting.ContentRootPath, "wwwroot") : Path.Combine(Directory.GetCurrentDirectory(), "wwwroot"));
            var uploadsRoot = Path.Combine(webRoot, "upload", "companylogo");
            Directory.CreateDirectory(uploadsRoot);

            var fileName = $"tenant_{tenant.Id}_{Guid.NewGuid():N}{ext}";
            var physicalPath = Path.Combine(uploadsRoot, fileName);

            await using (var fs = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(fs);
            }

            var relativeUrl = $"/upload/companylogo/{fileName}";
            tenant.LogoUrl = relativeUrl;
            tenant.ModifiedAt = DateTime.UtcNow;
            tenant.ModifiedById = User.GetUserId();
            _context.tenantUW.Update(tenant);
            _context.Save();

            var fullUrl = Request != null && Request.Host.HasValue
                ? $"{Request.Scheme}://{Request.Host}{relativeUrl}"
                : relativeUrl;

            return Ok(new
            {
                status = true,
                statusCode = 200,
                message = "لوگوی شرکت با موفقیت بارگذاری شد.",
                data = new { logoUrl = relativeUrl, fullUrl }
            });
        }

        [HttpGet("GetOnboardingStatus")]
        public ApiResponse<OnboardingStatusDto> GetOnboardingStatus()
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue) return new ApiResponse<OnboardingStatusDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
            };

            var tId = tenantId.Value;
            var tenant = _context.tenantUW.GetById(tId);
            if (tenant == null) return new ApiResponse<OnboardingStatusDto>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = "شرکت مورد نظر یافت نشد."
            };

            var profileCompleted = !string.IsNullOrWhiteSpace(tenant.TenantTitle)
                && (!string.IsNullOrWhiteSpace(tenant.CompanyPhone) || !string.IsNullOrWhiteSpace(tenant.Address));

            var bankCount = _context.bankAccountUW.Get(x => x.TenantId == tId && !x.IsDeleted).Count();
            var warehouseCount = _context.warehouseUW.Get(x => x.TenantId == tId && x.IsActive && !x.IsDeleted).Count();
            var productCount = _context.productUW.Get(x => x.TenantId == tId && x.IsActive && !x.IsDeleted).Count();
            var customerCount = _context.customerUW.Get(x => x.TenantId == tId && !x.IsDeleted).Count();
            var supplierCount = _context.supplierUW.Get(x => x.TenantId == tId && !x.IsDeleted).Count();

            var steps = new List<OnboardingStepDto>
            {
                new()
                {
                    Key = "company_profile",
                    Title = "تکمیل پروفایل شرکت",
                    Description = "نام شرکت و شماره تماس یا نشانی را در تنظیمات شرکت ثبت کنید.",
                    IsCompleted = profileCompleted,
                    CurrentCount = profileCompleted ? 1 : 0,
                    ActionUrl = "/AdminArea/Settings/Index"
                },
                new()
                {
                    Key = "bank_account",
                    Title = "تعریف حساب بانکی / صندوق",
                    Description = "حداقل یک حساب بانکی یا صندوق برای دریافت‌ها و پرداخت‌ها ثبت کنید.",
                    IsCompleted = bankCount > 0,
                    CurrentCount = bankCount,
                    ActionUrl = "/AdminArea/BankAccount/Index"
                },
                new()
                {
                    Key = "warehouse",
                    Title = "تعریف انبار فعال",
                    Description = "حداقل یک انبار فعال برای نگهداری کالا و گردش موجودی ایجاد کنید.",
                    IsCompleted = warehouseCount > 0,
                    CurrentCount = warehouseCount,
                    ActionUrl = "/AdminArea/Warehouse/Index"
                },
                new()
                {
                    Key = "product",
                    Title = "تعریف کالا و خدمات",
                    Description = "حداقل یک کالا یا خدمت فعال جهت استفاده در فاکتورها ثبت کنید.",
                    IsCompleted = productCount > 0,
                    CurrentCount = productCount,
                    ActionUrl = "/AdminArea/Product/Index"
                },
                new()
                {
                    Key = "customer",
                    Title = "تعریف مشتری",
                    Description = "حداقل یک مشتری برای ثبت پروژه و صدور فاکتور فروش تعریف کنید.",
                    IsCompleted = customerCount > 0,
                    CurrentCount = customerCount,
                    ActionUrl = "/AdminArea/Customer/Index"
                },
                new()
                {
                    Key = "supplier",
                    Title = "تعریف تأمین‌کننده",
                    Description = "حداقل یک تأمین‌کننده جهت ثبت فاکتورهای خرید کالا تعریف کنید.",
                    IsCompleted = supplierCount > 0,
                    CurrentCount = supplierCount,
                    ActionUrl = "/AdminArea/Supplier/Index"
                }
            };

            var completedSteps = steps.Count(s => s.IsCompleted);
            var totalSteps = steps.Count;
            var completionPercent = totalSteps > 0 ? (int)Math.Round(completedSteps * 100.0 / totalSteps) : 0;

            return new ApiResponse<OnboardingStatusDto>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = new OnboardingStatusDto
                {
                    TotalSteps = totalSteps,
                    CompletedSteps = completedSteps,
                    CompletionPercent = completionPercent,
                    IsCompleted = completedSteps == totalSteps,
                    Steps = steps
                }
            };
        }

        [HttpGet("GetInvoicePatterns")]
        public ApiResponse<object> GetInvoicePatterns()
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue) return new ApiResponse<object>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
            };

            var tenant = _context.tenantUW.GetById(tenantId.Value);
            if (tenant == null) return new ApiResponse<object>
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name)
            };

            return new ApiResponse<object>
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = ApiResultStatusCode.Success.DisplayNameAttribute(DisplayProperty.Name),
                Data = new
                {
                    salesInvoicePattern = tenant.SalesInvoicePattern ?? "INV-{YEAR}-{SEQ:4}",
                    purchaseInvoicePattern = tenant.PurchaseInvoicePattern ?? "PINV-{YEAR}-{SEQ:4}",
                    salesReturnPattern = tenant.SalesReturnPattern ?? "SRI-{YEAR}-{SEQ:4}",
                    purchaseReturnPattern = tenant.PurchaseReturnPattern ?? "PRI-{YEAR}-{SEQ:4}",
                    tokens = new[]
                    {
                        new { token = "{PREFIX}", description = "پیشوند نوع سند (مانند INV یا PINV)" },
                        new { token = "{YEAR}", description = "سال ۴ رقمی شمسی (مانند ۱۴۰۴)" },
                        new { token = "{YY}", description = "سال ۲ رقمی شمسی (مانند ۰۴)" },
                        new { token = "{MONTH}", description = "ماه ۲ رقمی شمسی (مانند ۰۱)" },
                        new { token = "{DAY}", description = "روز ۲ رقمی شمسی (مانند ۱۵)" },
                        new { token = "{SEQ:4}", description = "شماره مسلسل با صفر پر شده (مانند ۰۰۰۱)" },
                        new { token = "{TENANT}", description = "کد شناسه شرکت / مستأجر" }
                    }
                }
            };
        }

        [HttpPut("UpdateInvoicePatterns")]
        public ApiResponse UpdateInvoicePatterns([FromBody] InvoicePatternUpdateModel model)
        {
            var tenantId = User.GetTenantId();
            if (!tenantId.HasValue) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.Unauthorized,
                Message = ApiResultStatusCode.Unauthorized.DisplayNameAttribute(DisplayProperty.Name)
            };

            var tenant = _context.tenantUW.GetById(tenantId.Value);
            if (tenant == null) return new ApiResponse
            {
                Status = false,
                StatusCode = ApiResultStatusCode.NotFound,
                Message = ApiResultStatusCode.NotFound.DisplayNameAttribute(DisplayProperty.Name)
            };

            if (!string.IsNullOrWhiteSpace(model.SalesInvoicePattern))
                tenant.SalesInvoicePattern = model.SalesInvoicePattern.Trim();

            if (!string.IsNullOrWhiteSpace(model.PurchaseInvoicePattern))
                tenant.PurchaseInvoicePattern = model.PurchaseInvoicePattern.Trim();

            if (!string.IsNullOrWhiteSpace(model.SalesReturnPattern))
                tenant.SalesReturnPattern = model.SalesReturnPattern.Trim();

            if (!string.IsNullOrWhiteSpace(model.PurchaseReturnPattern))
                tenant.PurchaseReturnPattern = model.PurchaseReturnPattern.Trim();

            tenant.ModifiedAt = DateTime.UtcNow;
            tenant.ModifiedById = User.GetUserId();

            _context.tenantUW.Update(tenant);
            _context.Save();

            return new ApiResponse
            {
                Status = true,
                StatusCode = ApiResultStatusCode.Success,
                Message = "الگوهای شماره‌گذاری فاکتورها با موفقیت به‌روزرسانی شد."
            };
        }
    }
}
