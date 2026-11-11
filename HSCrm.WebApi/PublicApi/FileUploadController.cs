using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCrm.WebApi.PublicApi
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class FileUploadController : ControllerBase
    {
        private const long MaxFileSizeInBytes = 2 * 1024 * 1024; // 2 MB
        private static readonly HashSet<string> AllowedMimeTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            "image/jpeg",
            "image/png"
        };
        private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".png",
            ".jpg",
            ".jpeg"
        };

        private readonly IWebHostEnvironment _hosting;

        public FileUploadController(IWebHostEnvironment hosting)
        {
            _hosting = hosting;
        }

        [HttpPost("UserImageUpload")]
        public async Task<IActionResult> UserImageUpload([FromForm] IEnumerable<IFormFile> imagearray)
        {
            if (imagearray == null || !imagearray.Any())
                return BadRequest(new { statusCode = 400, message = "هیچ فایلی ارسال نشده است." });

            var webRoot = _hosting.WebRootPath ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot");
            var uploadsRoot = Path.Combine(webRoot, "upload", "userimage");
            Directory.CreateDirectory(uploadsRoot);

            var savedUrls = new List<string>();

            foreach (var file in imagearray)
            {
                if (file.Length <= 0)
                    return BadRequest(new { statusCode = 400, message = "فایل خالی است." });

                if (file.Length > MaxFileSizeInBytes)
                    return BadRequest(new { statusCode = 400, message = "حجم فایل نباید بیشتر از ۲ مگابایت باشد." });

                var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
                    return BadRequest(new { statusCode = 415, message = "فرمت مجاز نیست. فقط png/jpg/jpeg" });

                if (!string.IsNullOrEmpty(file.ContentType) && !AllowedMimeTypes.Contains(file.ContentType))
                    return BadRequest(new { statusCode = 415, message = "نوع رسانه‌ای فایل مجاز نیست." });

                var filename = $"{Guid.NewGuid():N}{ext}";
                var physicalPath = Path.Combine(uploadsRoot, filename);

                await using (var fs = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(fs);
                }

                var url = $"{Request.Scheme}://{Request.Host}/upload/userimage/{filename}";
                savedUrls.Add(url);
            }

            if (savedUrls.Count == 1)
                return Ok(new { statusCode = 200, message = savedUrls[0] });

            return Ok(new { statusCode = 200, message = "آپلود موفق", urls = savedUrls });
        }
    }
}
