using Common.Permissions;
using Framework.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace HSCrm.WebApi.PublicApi
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileUploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _hosting;

        public FileUploadController(IWebHostEnvironment hosting)
        {
            _hosting = hosting;
        }

        [HttpPost("UserImageUpload")]
        [Permission(Permissions.Users.Edit)]
        public async Task<IActionResult> UserImageUpload([FromForm] IEnumerable<IFormFile> imagearray, CancellationToken ct)
        {
            const long maxFileBytes = 5 * 1024 * 1024;
            const int maxFileCount = 5;
            var filesToUpload = imagearray?.ToList() ?? new List<IFormFile>();

            if (filesToUpload.Count == 0)
                return BadRequest(new { statusCode = 400, message = "هیچ فایلی ارسال نشده است." });
            if (filesToUpload.Count > maxFileCount)
                return BadRequest(new { statusCode = 400, message = "حداکثر تعداد فایل مجاز ۵ عدد است." });

            foreach (var file in filesToUpload)
            {
                if (file.Length <= 0 || file.Length > maxFileBytes)
                    return BadRequest(new { statusCode = 400, message = "حجم هر تصویر باید بین ۱ بایت تا ۵ مگابایت باشد." });

                var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
                if (extension is not (".png" or ".jpg" or ".jpeg"))
                    return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { statusCode = 415, message = "فقط تصویر PNG یا JPEG مجاز است." });

                await using var stream = file.OpenReadStream();
                var header = new byte[8];
                var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length), ct);
                var isPng = bytesRead == 8 && header.SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });
                var isJpeg = bytesRead >= 3 && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF;
                if (!((extension == ".png" && isPng) || ((extension == ".jpg" || extension == ".jpeg") && isJpeg)))
                    return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { statusCode = 415, message = "محتوای فایل با پسوند آن مطابقت ندارد." });
            }

            var webRoot = _hosting.WebRootPath ?? Path.Combine(_hosting.ContentRootPath, "wwwroot");
            var uploadsRoot = Path.Combine(webRoot, "upload", "userimage");
            Directory.CreateDirectory(uploadsRoot);

            var savedUrls = new List<string>();
            foreach (var file in filesToUpload)
            {
                var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
                var filename = $"{Guid.NewGuid():N}{extension}";
                var physicalPath = Path.Combine(uploadsRoot, filename);
                await using (var fs = new FileStream(physicalPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                    await file.CopyToAsync(fs, ct);
                savedUrls.Add($"{Request.Scheme}://{Request.Host}/upload/userimage/{filename}");
            }

            if (savedUrls.Count == 1)
                return Ok(new { statusCode = 200, message = savedUrls[0] });
            return Ok(new { statusCode = 200, message = "آپلود موفق", urls = savedUrls });
        }
    }
}
