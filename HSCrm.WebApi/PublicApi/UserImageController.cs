using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace HSCrm.WebApi.PublicApi
{
    // User avatars are intentionally publicly retrievable by unguessable generated IDs,
    // matching the existing static-avatar URL behavior. New uploads are stored outside wwwroot.
    [ApiController]
    [AllowAnonymous]
    [Route("upload/userimage")]
    public sealed class UserImageController : ControllerBase
    {
        private static readonly Regex SafeFileName = new(
            @"\A[a-f0-9]{32}\.(?:png|jpe?g)\z",
            RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private readonly IWebHostEnvironment _hosting;

        public UserImageController(IWebHostEnvironment hosting)
        {
            _hosting = hosting;
        }

        [HttpGet("{fileName}")]
        public IActionResult Get(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
                || !SafeFileName.IsMatch(fileName))
                return NotFound();

            var extension = Path.GetExtension(fileName).ToLowerInvariant();
            var contentType = extension == ".png" ? "image/png" : "image/jpeg";
            var root = Path.Combine(_hosting.ContentRootPath, "App_Data", "UserImages");
            var path = Path.Combine(root, fileName);

            if (!System.IO.File.Exists(path))
                return NotFound();

            var fileInfo = new FileInfo(path);
            if (fileInfo.Length <= 0 || fileInfo.Length > 5 * 1024 * 1024)
                return NotFound();

            Response.Headers["X-Content-Type-Options"] = "nosniff";
            Response.Headers["Content-Security-Policy"] = "default-src 'none'; sandbox";
            Response.Headers["Cross-Origin-Resource-Policy"] = "same-site";
            Response.Headers["Cache-Control"] = "public, max-age=3600";

            return PhysicalFile(path, contentType, enableRangeProcessing: false);
        }
    }
}
