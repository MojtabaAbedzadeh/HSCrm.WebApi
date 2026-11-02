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

            var validatedFiles = new List<(byte[] Content, string Extension)>();
            foreach (var file in filesToUpload)
            {
                if (file.Length <= 0 || file.Length > maxFileBytes)
                    return BadRequest(new { statusCode = 400, message = "حجم هر تصویر باید بین ۱ بایت تا ۵ مگابایت باشد." });

                var extension = Path.GetExtension(file.FileName)?.ToLowerInvariant();
                if (extension is not (".png" or ".jpg" or ".jpeg"))
                    return StatusCode(StatusCodes.Status415UnsupportedMediaType, new { statusCode = 415, message = "فقط تصویر PNG یا JPEG مجاز است." });

                await using var input = file.OpenReadStream();
                using var buffer = new MemoryStream();
                await input.CopyToAsync(buffer, ct);
                var content = buffer.ToArray();

                if (!TryGetImageDimensions(content, extension, out var width, out var height)
                    || width <= 0 || height <= 0 || width > 4096 || height > 4096
                    || (long)width * height > 16_000_000)
                    return StatusCode(StatusCodes.Status415UnsupportedMediaType,
                        new { statusCode = 415, message = "ساختار یا ابعاد تصویر معتبر نیست." });

                validatedFiles.Add((content, extension));
            }

            // Store new uploads outside wwwroot. They are served only by the
            // constrained image endpoint below, never by the generic static-file handler.
            var uploadsRoot = Path.Combine(_hosting.ContentRootPath, "App_Data", "UserImages");
            Directory.CreateDirectory(uploadsRoot);

            var savedUrls = new List<string>();
            foreach (var validated in validatedFiles)
            {
                var filename = $"{Guid.NewGuid():N}{validated.Extension}";
                var physicalPath = Path.Combine(uploadsRoot, filename);
                await System.IO.File.WriteAllBytesAsync(physicalPath, validated.Content, ct);
                savedUrls.Add($"{Request.Scheme}://{Request.Host}/upload/userimage/{filename}");
            }

            if (savedUrls.Count == 1)
                return Ok(new { statusCode = 200, message = savedUrls[0] });
            return Ok(new { statusCode = 200, message = "آپلود موفق", urls = savedUrls });
        }
        private static bool TryGetImageDimensions(byte[] data, string extension, out int width, out int height)
        {
            width = 0;
            height = 0;

            if (extension == ".png")
            {
                if (data.Length < 24
                    || System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(8, 4)) != 13
                    || !data.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A })
                    || !data.AsSpan(12, 4).SequenceEqual("IHDR"u8))
                    return false;

                var pngWidth = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(16, 4));
                var pngHeight = System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(20, 4));
                if (pngWidth > int.MaxValue || pngHeight > int.MaxValue)
                    return false;

                width = (int)pngWidth;
                height = (int)pngHeight;
                return width > 0 && height > 0;
            }

            if (extension is not (".jpg" or ".jpeg")
                || data.Length < 4 || data[0] != 0xFF || data[1] != 0xD8)
                return false;

            var index = 2;
            while (index < data.Length)
            {
                while (index < data.Length && data[index] != 0xFF)
                    index++;
                while (index < data.Length && data[index] == 0xFF)
                    index++;

                if (index >= data.Length)
                    return false;

                var marker = data[index++];
                if (marker == 0xD9 || marker == 0xDA)
                    return false;
                if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD7))
                    continue;

                if (index + 2 > data.Length)
                    return false;

                var segmentLength = (data[index] << 8) | data[index + 1];
                if (segmentLength < 2 || index + segmentLength > data.Length)
                    return false;

                var isStartOfFrame = marker is 0xC0 or 0xC1 or 0xC2 or 0xC3
                    or 0xC5 or 0xC6 or 0xC7 or 0xC9 or 0xCA or 0xCB or 0xCD or 0xCE or 0xCF;
                if (isStartOfFrame)
                {
                    if (segmentLength < 7)
                        return false;

                    height = (data[index + 3] << 8) | data[index + 4];
                    width = (data[index + 5] << 8) | data[index + 6];
                    return width > 0 && height > 0;
                }

                index += segmentLength;
            }

            return false;
        }

    }
}
