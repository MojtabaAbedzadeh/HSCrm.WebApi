using Common.Permissions;
using Framework.Authorization;
using Microsoft.AspNetCore.Mvc;
using SkiaSharp;

namespace HSCrm.WebApi.PublicApi
{
    [Route("api/[controller]")]
    [ApiController]
    public class FileUploadController : ControllerBase
    {
        private readonly IWebHostEnvironment _hosting;
        private readonly IConfiguration _configuration;

        public FileUploadController(IWebHostEnvironment hosting, IConfiguration configuration)
        {
            _hosting = hosting;
            _configuration = configuration;
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

                if (!TryDecodeAndReencodeImage(content, extension, width, height, out var normalizedContent)
                    || normalizedContent.Length == 0 || normalizedContent.Length > maxFileBytes)
                    return StatusCode(StatusCodes.Status415UnsupportedMediaType,
                        new { statusCode = 415, message = "تصویر قابل رمزگشایی یا بازرمزگذاری امن نیست." });

                validatedFiles.Add((normalizedContent, extension));
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
                var publicBaseUrl = GetPublicBaseUrl();
                var relativeUrl = $"/upload/userimage/{filename}";
                savedUrls.Add(publicBaseUrl == null ? relativeUrl : $"{publicBaseUrl}{relativeUrl}");
            }

            if (savedUrls.Count == 1)
                return Ok(new { statusCode = 200, message = savedUrls[0] });
            return Ok(new { statusCode = 200, message = "آپلود موفق", urls = savedUrls });
        }
        private static bool TryDecodeAndReencodeImage(
            byte[] data,
            string extension,
            int expectedWidth,
            int expectedHeight,
            out byte[] normalized)
        {
            normalized = Array.Empty<byte>();

            try
            {
                // The lightweight header parser runs first to cap dimensions before native decoding.
                using var bitmap = SKBitmap.Decode(data);
                if (bitmap == null
                    || bitmap.Width != expectedWidth
                    || bitmap.Height != expectedHeight
                    || bitmap.Width <= 0
                    || bitmap.Height <= 0
                    || bitmap.Width > 4096
                    || bitmap.Height > 4096
                    || (long)bitmap.Width * bitmap.Height > 16_000_000)
                    return false;

                var outputFormat = extension == ".png"
                    ? SKEncodedImageFormat.Png
                    : SKEncodedImageFormat.Jpeg;
                var quality = outputFormat == SKEncodedImageFormat.Jpeg ? 90 : 100;

                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(outputFormat, quality);
                if (encoded == null || encoded.Size <= 0)
                    return false;

                normalized = encoded.ToArray();
                return normalized.Length > 0;
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
                // Invalid or unsupported bitstreams are rejected; raw uploaded bytes are never persisted.
                normalized = Array.Empty<byte>();
                return false;
            }
        }

        private string? GetPublicBaseUrl()
        {
            var configured = _configuration["PublicBaseUrl"];
            if (string.IsNullOrWhiteSpace(configured))
                return null;

            if (!Uri.TryCreate(configured, UriKind.Absolute, out var uri)
                || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp)
                || !string.IsNullOrEmpty(uri.UserInfo)
                || uri.AbsolutePath != "/"
                || !string.IsNullOrEmpty(uri.Query)
                || !string.IsNullOrEmpty(uri.Fragment))
                throw new InvalidOperationException("PublicBaseUrl must be an absolute HTTP(S) origin without credentials, path, query, or fragment.");

            return uri.GetLeftPart(UriPartial.Authority).TrimEnd('/');
        }

        // Profile images are intentionally served by a constrained endpoint because
        // uploads are stored outside wwwroot. Filenames are random GUIDs generated here.
        [HttpGet("/upload/userimage/{fileName}")]
        [Microsoft.AspNetCore.Authorization.AllowAnonymous]
        [ResponseCache(Duration = 300, Location = ResponseCacheLocation.Client)]
        public IActionResult GetUserImage(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || !System.Text.RegularExpressions.Regex.IsMatch(
                    fileName,
                    @"^[0-9a-fA-F]{32}\.(png|jpg|jpeg)$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase))
                return NotFound();

            var uploadsRoot = Path.GetFullPath(
                Path.Combine(_hosting.ContentRootPath, "App_Data", "UserImages"));
            var physicalPath = Path.GetFullPath(Path.Combine(uploadsRoot, fileName));

            if (!physicalPath.StartsWith(uploadsRoot + Path.DirectorySeparatorChar,
                    StringComparison.OrdinalIgnoreCase)
                || !System.IO.File.Exists(physicalPath))
                return NotFound();

            var contentType = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" or ".jpeg" => "image/jpeg",
                _ => "application/octet-stream"
            };

            return PhysicalFile(physicalPath, contentType, enableRangeProcessing: false);
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
