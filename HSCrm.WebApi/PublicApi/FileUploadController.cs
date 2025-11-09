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
        public async Task<IActionResult> UserImageUpload([FromForm] IEnumerable<IFormFile> imagearray)
        {
            if (imagearray == null || !imagearray.Any())
                return BadRequest(new { statusCode = 400, message = "هیچ فایلی ارسال نشده است." });

            // مسیر فیزیکی: wwwroot/upload/userimage
            var uploadsRoot = Path.Combine(_hosting.WebRootPath, "upload", "userimage");
            Directory.CreateDirectory(uploadsRoot);

            var allowedExts = new[] { ".png", ".jpg", ".jpeg" };
            var savedUrls = new List<string>();

            foreach (var file in imagearray)
            {
                if (file.Length <= 0)
                    return BadRequest(new { statusCode = 400, message = "فایل خالی است." });

                var ext = Path.GetExtension(file.FileName)?.ToLowerInvariant();
                if (string.IsNullOrEmpty(ext) || !allowedExts.Contains(ext))
                    return BadRequest(new { statusCode = 415, message = "فرمت مجاز نیست. فقط png/jpg/jpeg" });

                // نام یکتا
                var filename = $"{Guid.NewGuid():N}{ext}";
                var physicalPath = Path.Combine(uploadsRoot, filename);

                await using (var fs = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await file.CopyToAsync(fs);
                }

                // URL عمومی (فرض بر این است که StaticFiles برای wwwroot فعال است)
                var url = $"{Request.Scheme}://{Request.Host}/upload/userimage/{filename}";
                savedUrls.Add(url);
            }

            // اگر فقط یک فایل داریم، همان تک لینک را بده
            if (savedUrls.Count == 1)
                return Ok(new { statusCode = 200, message = savedUrls[0] });

            // چندفایله
            return Ok(new { statusCode = 200, message = "آپلود موفق", urls = savedUrls });
        }
    }
}
