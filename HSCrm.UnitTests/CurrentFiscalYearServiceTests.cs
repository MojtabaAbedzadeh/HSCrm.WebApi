using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace HSCrm.UnitTests
{
    public class CurrentFiscalYearServiceTests
    {
        [Fact]
        public void FiscalYearId_UsesValidatedJwtClaim_AndIgnoresClientHeader()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["X-FiscalYear-Id"] = "999";
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("FiscalYearId", "12") },
                authenticationType: "Test"));

            var service = new CurrentFiscalYearService(new HttpContextAccessor
            {
                HttpContext = context
            });

            Assert.Equal(12, service.FiscalYearId);
        }

        [Fact]
        public void FiscalYearId_ReturnsNull_WhenOnlyClientHeaderIsPresent()
        {
            var context = new DefaultHttpContext();
            context.Request.Headers["X-FiscalYear-Id"] = "12";
            context.User = new ClaimsPrincipal(new ClaimsIdentity());

            var service = new CurrentFiscalYearService(new HttpContextAccessor
            {
                HttpContext = context
            });

            Assert.Null(service.FiscalYearId);
        }

        [Theory]
        [InlineData("0")]
        [InlineData("-1")]
        [InlineData("not-a-number")]
        public void FiscalYearId_ReturnsNull_ForInvalidClaimValues(string claimValue)
        {
            var context = new DefaultHttpContext();
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
                new[] { new Claim("FiscalYearId", claimValue) },
                authenticationType: "Test"));

            var service = new CurrentFiscalYearService(new HttpContextAccessor
            {
                HttpContext = context
            });

            Assert.Null(service.FiscalYearId);
        }
    }
}
