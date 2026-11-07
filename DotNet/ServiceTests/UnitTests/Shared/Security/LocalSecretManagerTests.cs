using LantanaGroup.Link.Shared.Application.Services.SecretManager;
using Link.Authorization.Infrastructure;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging.Abstractions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.Shared.Security
{
    /// <summary>
    /// The local secrets file is shared by every service running as one user (the Aspire AppHost), and
    /// DataAcquisition passes facility credentials to its worker through it. These cover what sharing needs:
    /// one process sees another's writes, a change doesn't drop another's secrets, and a file written under
    /// another Data Protection key ring is never written over.
    /// </summary>
    [Trait("Category", "UnitTests")]
    public sealed class LocalSecretManagerTests : IDisposable
    {
        private const string BearerKey = LinkAuthorizationConstants.LinkBearerService.LinkBearerKeyName;

        private readonly string _directory = Path.Combine(Path.GetTempPath(), "link-secrets-tests", Guid.NewGuid().ToString("N"));
        private readonly IDataProtectionProvider _sharedKeyRing = new EphemeralDataProtectionProvider();

        private string SecretsFile => Path.Combine(_directory, "link-local-secrets.enc");

        private LocalSecretManager Create(IDataProtectionProvider? keyRing = null) =>
            new(NullLogger<LocalSecretManager>.Instance, keyRing ?? _sharedKeyRing, _directory);

        public void Dispose()
        {
            try
            {
                Directory.Delete(_directory, recursive: true);
            }
            catch (IOException)
            {
                // A temp folder left behind holds nothing of value.
            }
        }

        [Fact]
        public async Task A_new_file_gets_a_bearer_key_that_a_second_process_reads()
        {
            var first = Create();
            var second = Create();

            var key = await first.GetSecretAsync(BearerKey, CancellationToken.None);

            Assert.False(string.IsNullOrEmpty(key));
            Assert.Equal(key, await second.GetSecretAsync(BearerKey, CancellationToken.None));
        }

        [Fact]
        public async Task A_secret_set_after_another_process_started_is_seen_by_it()
        {
            var dataAcquisition = Create();
            var worker = Create();

            Assert.True(await dataAcquisition.SetSecretAsync("facility-client-secret", "s3cret", CancellationToken.None));

            Assert.Equal("s3cret", await worker.GetSecretAsync("facility-client-secret", CancellationToken.None));
        }

        [Fact]
        public async Task A_change_keeps_secrets_another_process_added_since_it_read_the_file()
        {
            var first = Create();
            var second = Create();

            await first.SetSecretAsync("from-first", "1", CancellationToken.None);
            await second.SetSecretAsync("from-second", "2", CancellationToken.None);

            var fresh = Create();
            Assert.Equal("1", await fresh.GetSecretAsync("from-first", CancellationToken.None));
            Assert.Equal("2", await fresh.GetSecretAsync("from-second", CancellationToken.None));
        }

        [Fact]
        public async Task A_file_written_under_another_key_ring_is_not_written_over()
        {
            var owner = Create();
            await owner.SetSecretAsync("facility-client-secret", "s3cret", CancellationToken.None);
            var before = await File.ReadAllTextAsync(SecretsFile);

            var otherRing = Create(new EphemeralDataProtectionProvider());
            var saved = await otherRing.SetSecretAsync("other", "value", CancellationToken.None);

            Assert.False(saved);
            Assert.Equal(before, await File.ReadAllTextAsync(SecretsFile));
            Assert.Equal("value", await otherRing.GetSecretAsync("other", CancellationToken.None));
            Assert.Equal("s3cret", await owner.GetSecretAsync("facility-client-secret", CancellationToken.None));
        }

        [Fact]
        public async Task A_service_on_another_key_ring_still_has_a_bearer_key_in_memory()
        {
            Create();

            var otherRing = Create(new EphemeralDataProtectionProvider());

            Assert.False(string.IsNullOrEmpty(await otherRing.GetSecretAsync(BearerKey, CancellationToken.None)));
        }

        [Fact]
        public async Task Deleting_a_secret_removes_it_for_every_process()
        {
            var first = Create();
            var second = Create();
            await first.SetSecretAsync("temporary", "x", CancellationToken.None);

            await second.DeleteSecretAsync("temporary", CancellationToken.None);

            Assert.Null(await first.GetSecretAsync("temporary", CancellationToken.None));
        }

        [Fact]
        public async Task Concurrent_writers_lose_no_secrets_and_leave_no_temporary_files()
        {
            var managers = Enumerable.Range(0, 4).Select(_ => Create()).ToArray();

            await Task.WhenAll(managers.SelectMany((manager, m) => Enumerable.Range(0, 10).Select(i =>
                Task.Run(() => manager.SetSecretAsync($"secret-{m}-{i}", $"{m}-{i}", CancellationToken.None)))));

            var fresh = Create();
            for (var m = 0; m < managers.Length; m++)
            for (var i = 0; i < 10; i++)
                Assert.Equal($"{m}-{i}", await fresh.GetSecretAsync($"secret-{m}-{i}", CancellationToken.None));
            Assert.Empty(Directory.GetFiles(_directory, "*.tmp"));
        }
    }
}
