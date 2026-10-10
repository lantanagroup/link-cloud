using System.Security.Claims;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Audit;
using Microsoft.AspNetCore.Http;

namespace UnitTests.Admin.BFF
{
    [Trait("Category", "UnitTests")]
    public class InitiatedByAuditTests
    {
        private static DefaultHttpContext Context(string? id, string? name, ClaimsPrincipal? user = null)
        {
            var context = new DefaultHttpContext();
            if (id != null) context.Request.Headers[InitiatedByAudit.InitiatedByHeader] = id;
            if (name != null) context.Request.Headers[InitiatedByAudit.InitiatedByNameHeader] = name;
            if (user != null) context.User = user;
            return context;
        }

        [Fact]
        public void Read_ReturnsNull_WhenNoHeader()
        {
            Assert.Null(InitiatedByAudit.Read(Context(null, "ignored")));
        }

        [Fact]
        public void Read_DecodesValues_AndNamesTheAuthenticatedPrincipal()
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim("sub", "system-account") }, "link_bearer"));

            var entry = InitiatedByAudit.Read(Context(Uri.EscapeDataString("nick@example.org"), Uri.EscapeDataString("Nick Montalto"), user));

            Assert.NotNull(entry);
            Assert.Equal("nick@example.org", entry!.Value.InitiatedById);
            Assert.Equal("Nick Montalto", entry.Value.InitiatedByName);
            Assert.Equal("system-account", entry.Value.Principal);
        }

        [Fact]
        public void Read_StripsControlCharacters_AndCapsLength()
        {
            var entry = InitiatedByAudit.Read(Context(Uri.EscapeDataString("a\r\nb") + new string('x', 400), null));

            Assert.NotNull(entry);
            Assert.DoesNotContain('\n', entry!.Value.InitiatedById);
            Assert.StartsWith("ab", entry.Value.InitiatedById);
            Assert.Equal(InitiatedByAudit.MaxValueLength, entry.Value.InitiatedById.Length);
            Assert.Equal("anonymous", entry.Value.Principal);
        }
    }
}
