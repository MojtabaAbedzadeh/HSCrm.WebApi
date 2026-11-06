using System.Buffers.Binary;
using HSCrm.WebApi.PublicApi;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Moq;
using SkiaSharp;
using Xunit;

namespace HSCrm.UnitTests
{
    public class FileUploadSecurityTests
    {
        [Fact]
        public async Task UserImageUpload_DecodesAndReencodesValidPngBeforeSaving()
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "HSCrmUploadTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRoot);

            try
            {
                using var bitmap = new SKBitmap(new SKImageInfo(8, 6, SKColorType.Rgba8888, SKAlphaType.Premul));
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.CornflowerBlue);
                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Png, 100);
                var inputBytes = encoded.ToArray();

                using var stream = new MemoryStream(inputBytes);
                var formFile = new FormFile(stream, 0, stream.Length, "imagearray", "avatar.png")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "image/png"
                };

                var environment = new Mock<IWebHostEnvironment>();
                environment.SetupGet(x => x.ContentRootPath).Returns(contentRoot);
                environment.SetupGet(x => x.EnvironmentName).Returns("Development");
                var controller = new FileUploadController(environment.Object, new ConfigurationBuilder().Build());

                var result = await controller.UserImageUpload(new[] { formFile }, CancellationToken.None);

                Assert.IsType<OkObjectResult>(result);
                var uploadsRoot = Path.Combine(contentRoot, "App_Data", "UserImages");
                var savedFiles = Directory.GetFiles(uploadsRoot);
                var savedFile = Assert.Single(savedFiles);
                Assert.EndsWith(".png", savedFile, StringComparison.OrdinalIgnoreCase);

                var storedBytes = await File.ReadAllBytesAsync(savedFile);
                using var decoded = SKBitmap.Decode(storedBytes);
                Assert.NotNull(decoded);
                Assert.Equal(8, decoded.Width);
                Assert.Equal(6, decoded.Height);
                Assert.True(storedBytes.Length > 0);
            }
            finally
            {
                if (Directory.Exists(contentRoot))
                    Directory.Delete(contentRoot, recursive: true);
            }
        }

        [Fact]
        public async Task UserImageUpload_DecodesAndReencodesValidJpegBeforeSaving()
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "HSCrmUploadTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRoot);

            try
            {
                using var bitmap = new SKBitmap(new SKImageInfo(8, 6, SKColorType.Rgba8888, SKAlphaType.Premul));
                using var canvas = new SKCanvas(bitmap);
                canvas.Clear(SKColors.DarkOrange);
                using var image = SKImage.FromBitmap(bitmap);
                using var encoded = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                var inputBytes = encoded.ToArray();

                using var stream = new MemoryStream(inputBytes);
                var formFile = new FormFile(stream, 0, stream.Length, "imagearray", "avatar.jpg")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "image/jpeg"
                };

                var environment = new Mock<IWebHostEnvironment>();
                environment.SetupGet(x => x.ContentRootPath).Returns(contentRoot);
                environment.SetupGet(x => x.EnvironmentName).Returns("Development");
                var controller = new FileUploadController(environment.Object, new ConfigurationBuilder().Build());

                var result = await controller.UserImageUpload(new[] { formFile }, CancellationToken.None);

                Assert.IsType<OkObjectResult>(result);
                var uploadsRoot = Path.Combine(contentRoot, "App_Data", "UserImages");
                var savedFile = Assert.Single(Directory.GetFiles(uploadsRoot));
                Assert.EndsWith(".jpg", savedFile, StringComparison.OrdinalIgnoreCase);

                var storedBytes = await File.ReadAllBytesAsync(savedFile);
                using var decoded = SKBitmap.Decode(storedBytes);
                Assert.NotNull(decoded);
                Assert.Equal(8, decoded.Width);
                Assert.Equal(6, decoded.Height);
            }
            finally
            {
                if (Directory.Exists(contentRoot))
                    Directory.Delete(contentRoot, recursive: true);
            }
        }

        [Fact]
        public async Task UserImageUpload_RejectsPngHeaderWithoutDecodableImageBody()
        {
            var contentRoot = Path.Combine(Path.GetTempPath(), "HSCrmUploadTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(contentRoot);

            try
            {
                var invalidPng = new byte[24];
                new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(invalidPng, 0);
                BinaryPrimitives.WriteUInt32BigEndian(invalidPng.AsSpan(8, 4), 13);
                "IHDR"u8.CopyTo(invalidPng.AsSpan(12, 4));
                BinaryPrimitives.WriteUInt32BigEndian(invalidPng.AsSpan(16, 4), 2);
                BinaryPrimitives.WriteUInt32BigEndian(invalidPng.AsSpan(20, 4), 2);

                using var stream = new MemoryStream(invalidPng);
                var formFile = new FormFile(stream, 0, stream.Length, "imagearray", "malformed.png")
                {
                    Headers = new HeaderDictionary(),
                    ContentType = "image/png"
                };

                var environment = new Mock<IWebHostEnvironment>();
                environment.SetupGet(x => x.ContentRootPath).Returns(contentRoot);
                environment.SetupGet(x => x.EnvironmentName).Returns("Development");
                var controller = new FileUploadController(environment.Object, new ConfigurationBuilder().Build());

                var result = await controller.UserImageUpload(new[] { formFile }, CancellationToken.None);

                var objectResult = Assert.IsType<ObjectResult>(result);
                Assert.Equal(StatusCodes.Status415UnsupportedMediaType, objectResult.StatusCode);
                Assert.False(Directory.Exists(Path.Combine(contentRoot, "App_Data", "UserImages")));
            }
            finally
            {
                if (Directory.Exists(contentRoot))
                    Directory.Delete(contentRoot, recursive: true);
            }
        }
    }
}
