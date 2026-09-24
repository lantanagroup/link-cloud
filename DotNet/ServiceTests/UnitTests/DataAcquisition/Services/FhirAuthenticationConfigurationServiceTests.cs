using Azure;
using DataAcquisition.Domain.Application.Models;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Managers;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Api.Configuration;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Models.Exceptions;
using LantanaGroup.Link.DataAcquisition.Domain.Application.Services;
using LantanaGroup.Link.DataAcquisition.Domain.Infrastructure.Models;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Moq;
using System.Text.RegularExpressions;
using Task = System.Threading.Tasks.Task;

namespace UnitTests.DataAcquisition.Services;

[Trait("Category", "UnitTests")]
public class FhirAuthenticationConfigurationServiceTests
{
    private const string FacilityId = "test-facility";
    private const string TokenUrl = "https://vendor.test/oauth2/token";
    private const string ClientIdValue = "link-client";
    private const string ClientSecretValue = "s3cret";
    private const string Scope = "system/*.read";

    private static readonly Regex KeyVaultSecretName = new("^[0-9a-zA-Z-]{1,127}$", RegexOptions.Compiled);

    private readonly Mock<IFhirQueryConfigurationManager> _manager = new();
    private readonly Mock<IFhirQueryConfigurationQueries> _queries = new();
    private readonly Mock<ISecretManager> _secretManager = new();
    private readonly Mock<ICacheService> _cacheService = new();
    private readonly Mock<ILogger<FhirAuthenticationConfigurationService>> _logger = new();

    public FhirAuthenticationConfigurationServiceTests()
    {
        _secretManager
            .Setup(x => x.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    private FhirAuthenticationConfigurationService CreateService()
    {
        return new FhirAuthenticationConfigurationService(_manager.Object,
                                                          _queries.Object,
                                                          _secretManager.Object,
                                                          _cacheService.Object,
                                                          _logger.Object);
    }

    private static FhirAuthenticationConfigurationRequest CreateRequest() => new()
    {
        TokenUrl = TokenUrl,
        ClientId = ClientIdValue,
        ClientSecret = ClientSecretValue,
        Scope = Scope
    };

    /// <summary>
    /// The row the queries return for a facility already configured for generic OAuth.
    /// </summary>
    private static AuthenticationConfigurationModel StoredOAuth(string clientSecretName) => new()
    {
        AuthType = nameof(AuthType.OAuth),
        TokenUrl = TokenUrl,
        ClientId = "existing-client-id-name",
        ClientSecret = clientSecretName,
        Scope = Scope
    };

    private void StoredConfiguration(AuthenticationConfigurationModel? stored)
    {
        _queries
            .Setup(x => x.GetAuthenticationConfigurationByFacilityId(FacilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(stored);
    }

    private void SecretResolvesTo(string secretName, string? value)
    {
        _secretManager
            .Setup(x => x.GetSecretAsync(secretName, It.IsAny<CancellationToken>()))
            .ReturnsAsync(value);
    }

    private void SecretNotFound(string secretName)
    {
        _secretManager
            .Setup(x => x.GetSecretAsync(secretName, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "Secret not found."));
    }

    private AuthenticationConfiguration CaptureSavedConfiguration()
    {
        AuthenticationConfiguration? saved = null;
        _manager
            .Setup(x => x.UpdateAuthenticationConfiguration(FacilityId,
                                                            It.IsAny<AuthenticationConfiguration>(),
                                                            It.IsAny<CancellationToken>()))
            .Callback<string, AuthenticationConfiguration, CancellationToken>((_, config, _) => saved = config)
            .ReturnsAsync(new AuthenticationConfigurationModel());

        return saved ??= new AuthenticationConfiguration();
    }

    // ---- CreateOrUpdateAsync: the happy path ----

    [Fact]
    public async Task CreateOrUpdateAsync_NewConfiguration_WritesBothSecretValuesToTheSecretManager()
    {
        StoredConfiguration(null);
        var writes = new Dictionary<string, string>();
        _secretManager
            .Setup(x => x.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((name, value, _) => writes[name] = value)
            .ReturnsAsync(true);

        await CreateService().CreateOrUpdateAsync(FacilityId, CreateRequest(), CancellationToken.None);

        Assert.Equal(2, writes.Count);
        Assert.Contains(writes, w => w.Key.EndsWith("-client-id") && w.Value == ClientIdValue);
        Assert.Contains(writes, w => w.Key.EndsWith("-client-secret") && w.Value == ClientSecretValue);
    }

    [Fact]
    public async Task CreateOrUpdateAsync_NewConfiguration_StoresSecretNamesOnTheRowNeverTheValues()
    {
        StoredConfiguration(null);
        AuthenticationConfiguration? saved = null;
        _manager
            .Setup(x => x.UpdateAuthenticationConfiguration(FacilityId,
                                                            It.IsAny<AuthenticationConfiguration>(),
                                                            It.IsAny<CancellationToken>()))
            .Callback<string, AuthenticationConfiguration, CancellationToken>((_, config, _) => saved = config)
            .ReturnsAsync(new AuthenticationConfigurationModel());

        await CreateService().CreateOrUpdateAsync(FacilityId, CreateRequest(), CancellationToken.None);

        Assert.NotNull(saved);
        Assert.Equal(nameof(AuthType.OAuth), saved!.AuthType);
        Assert.Equal(TokenUrl, saved.TokenUrl);
        Assert.Equal(Scope, saved.Scope);
        Assert.EndsWith("-client-id", saved.ClientId);
        Assert.EndsWith("-client-secret", saved.ClientSecret);
        Assert.NotEqual(ClientIdValue, saved.ClientId);
        Assert.NotEqual(ClientSecretValue, saved.ClientSecret);
    }

    [Fact]
    public async Task CreateOrUpdateAsync_Success_EvictsTheFacilitysCachedToken()
    {
        StoredConfiguration(null);

        await CreateService().CreateOrUpdateAsync(FacilityId, CreateRequest(), CancellationToken.None);

        _cacheService.Verify(x => x.RemoveAsync(FacilityId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateOrUpdateAsync_Success_ReturnsSafeFieldsAndTheStoredFlag()
    {
        StoredConfiguration(null);

        var response = await CreateService().CreateOrUpdateAsync(FacilityId, CreateRequest(), CancellationToken.None);

        Assert.Equal(TokenUrl, response.TokenUrl);
        Assert.Equal(ClientIdValue, response.ClientId);
        Assert.Equal(Scope, response.Scope);
        Assert.True(response.ClientSecretStored);
    }

    [Fact]
    public async Task CreateOrUpdateAsync_PaddedValues_AreTrimmedBeforeBeingStored()
    {
        StoredConfiguration(null);
        var writes = new Dictionary<string, string>();
        _secretManager
            .Setup(x => x.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((name, value, _) => writes[name] = value)
            .ReturnsAsync(true);

        AuthenticationConfiguration? saved = null;
        _manager
            .Setup(x => x.UpdateAuthenticationConfiguration(FacilityId,
                                                            It.IsAny<AuthenticationConfiguration>(),
                                                            It.IsAny<CancellationToken>()))
            .Callback<string, AuthenticationConfiguration, CancellationToken>((_, config, _) => saved = config)
            .ReturnsAsync(new AuthenticationConfigurationModel());

        var request = new FhirAuthenticationConfigurationRequest
        {
            TokenUrl = $"  {TokenUrl}  ",
            ClientId = $"  {ClientIdValue}  ",
            ClientSecret = $"  {ClientSecretValue}  ",
            Scope = $"  {Scope}  "
        };

        await CreateService().CreateOrUpdateAsync(FacilityId, request, CancellationToken.None);

        Assert.Equal(TokenUrl, saved!.TokenUrl);
        Assert.Equal(Scope, saved.Scope);
        Assert.Contains(writes, w => w.Value == ClientIdValue);
        Assert.Contains(writes, w => w.Value == ClientSecretValue);
    }

    /// <summary>
    /// The secrets must land before the row, so the row can never point at a name that was never
    /// written. The reverse order would leave acquisition unable to resolve the credentials.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_WritesTheSecretsBeforeTheRow()
    {
        StoredConfiguration(null);
        var order = new List<string>();

        _secretManager
            .Setup(x => x.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("secret"))
            .ReturnsAsync(true);
        _manager
            .Setup(x => x.UpdateAuthenticationConfiguration(FacilityId,
                                                            It.IsAny<AuthenticationConfiguration>(),
                                                            It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("row"))
            .ReturnsAsync(new AuthenticationConfigurationModel());

        await CreateService().CreateOrUpdateAsync(FacilityId, CreateRequest(), CancellationToken.None);

        Assert.Equal(new[] { "secret", "secret", "row" }, order);
    }

    // ---- CreateOrUpdateAsync: an omitted secret ----

    [Fact]
    public async Task CreateOrUpdateAsync_NoSecretSuppliedAndOneStored_KeepsTheStoredSecret()
    {
        const string existingSecretName = "hand-made-secret-name";
        StoredConfiguration(StoredOAuth(existingSecretName));
        SecretResolvesTo(existingSecretName, ClientSecretValue);

        AuthenticationConfiguration? saved = null;
        _manager
            .Setup(x => x.UpdateAuthenticationConfiguration(FacilityId,
                                                            It.IsAny<AuthenticationConfiguration>(),
                                                            It.IsAny<CancellationToken>()))
            .Callback<string, AuthenticationConfiguration, CancellationToken>((_, config, _) => saved = config)
            .ReturnsAsync(new AuthenticationConfigurationModel());

        var request = CreateRequest();
        request.ClientSecret = null;

        await CreateService().CreateOrUpdateAsync(FacilityId, request, CancellationToken.None);

        // The existing name is preserved rather than orphaned, and only the client id is rewritten.
        Assert.Equal(existingSecretName, saved!.ClientSecret);
        _secretManager.Verify(x => x.SetSecretAsync(It.Is<string>(n => n.EndsWith("-client-id")),
                                                    ClientIdValue,
                                                    It.IsAny<CancellationToken>()),
                              Times.Once);
        _secretManager.Verify(x => x.SetSecretAsync(It.IsAny<string>(),
                                                    ClientSecretValue,
                                                    It.IsAny<CancellationToken>()),
                              Times.Never);
    }

    [Fact]
    public async Task CreateOrUpdateAsync_NoSecretSuppliedAndNoneStored_ThrowsAndWritesNothing()
    {
        StoredConfiguration(null);
        var request = CreateRequest();
        request.ClientSecret = null;

        var ex = await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().CreateOrUpdateAsync(FacilityId, request, CancellationToken.None));

        Assert.Contains("ClientSecret is required", ex.Message);
        _secretManager.Verify(x => x.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                              Times.Never);
        _manager.Verify(x => x.UpdateAuthenticationConfiguration(It.IsAny<string>(),
                                                                 It.IsAny<AuthenticationConfiguration>(),
                                                                 It.IsAny<CancellationToken>()),
                        Times.Never);
    }

    /// <summary>
    /// A row can name a secret that is no longer in the vault, for instance after an out-of-band
    /// deletion. That counts as nothing stored, so a replacement secret is required.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_NoSecretSuppliedAndStoredNameMissingFromTheVault_Throws()
    {
        const string existingSecretName = "deleted-secret-name";
        StoredConfiguration(StoredOAuth(existingSecretName));
        SecretNotFound(existingSecretName);

        var request = CreateRequest();
        request.ClientSecret = null;

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().CreateOrUpdateAsync(FacilityId, request, CancellationToken.None));
    }

    /// <summary>
    /// A facility on Epic or Basic has no generic OAuth secret to keep, so switching it to OAuth
    /// requires the caller to supply one.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_NoSecretSuppliedAndFacilityIsOnAnotherAuthType_Throws()
    {
        StoredConfiguration(new AuthenticationConfigurationModel
        {
            AuthType = nameof(AuthType.Epic),
            ClientId = "epic-client-id-name",
            TokenUrl = TokenUrl
        });

        var request = CreateRequest();
        request.ClientSecret = null;

        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().CreateOrUpdateAsync(FacilityId, request, CancellationToken.None));
    }

    // ---- CreateOrUpdateAsync: failures ----

    [Fact]
    public async Task CreateOrUpdateAsync_SecretManagerRefusesTheWrite_LeavesTheRowUnchanged()
    {
        StoredConfiguration(null);
        _secretManager
            .Setup(x => x.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => CreateService().CreateOrUpdateAsync(FacilityId, CreateRequest(), CancellationToken.None));

        _manager.Verify(x => x.UpdateAuthenticationConfiguration(It.IsAny<string>(),
                                                                 It.IsAny<AuthenticationConfiguration>(),
                                                                 It.IsAny<CancellationToken>()),
                        Times.Never);
        _cacheService.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// The manager raises NotFoundException when the facility has no FHIR query configuration row to
    /// attach authentication to. That is the endpoint's 404, and it must not look like a success.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_NoFhirQueryConfigurationRow_PropagatesNotFoundAndSkipsTheCacheEviction()
    {
        StoredConfiguration(null);
        _manager
            .Setup(x => x.UpdateAuthenticationConfiguration(FacilityId,
                                                            It.IsAny<AuthenticationConfiguration>(),
                                                            It.IsAny<CancellationToken>()))
            .ThrowsAsync(new NotFoundException("No configuration found for facilityId: test-facility."));

        await Assert.ThrowsAsync<NotFoundException>(
            () => CreateService().CreateOrUpdateAsync(FacilityId, CreateRequest(), CancellationToken.None));

        _cacheService.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task CreateOrUpdateAsync_BlankFacilityId_Throws(string facilityId)
    {
        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().CreateOrUpdateAsync(facilityId, CreateRequest(), CancellationToken.None));
    }

    [Fact]
    public async Task CreateOrUpdateAsync_NullRequest_Throws()
    {
        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().CreateOrUpdateAsync(FacilityId, null!, CancellationToken.None));
    }

    // ---- Secret naming ----

    [Fact]
    public async Task CreateOrUpdateAsync_SecretNames_AreKeyVaultSafe()
    {
        var names = await CaptureSecretNamesFor("Facility_01.A");

        Assert.All(names, name => Assert.Matches(KeyVaultSecretName, name));
    }

    /// <summary>
    /// The slug replaces anything outside [0-9a-z-], so two facility ids can collapse to the same
    /// slug. The hash is what keeps their secrets apart.
    /// </summary>
    [Fact]
    public async Task CreateOrUpdateAsync_FacilityIdsThatShareASlug_GetDifferentSecretNames()
    {
        var first = await CaptureSecretNamesFor("a.b");
        var second = await CaptureSecretNamesFor("a_b");

        Assert.Empty(first.Intersect(second));
    }

    [Fact]
    public async Task CreateOrUpdateAsync_VeryLongFacilityId_StaysWithinTheKeyVaultNameLimit()
    {
        var names = await CaptureSecretNamesFor(new string('f', 200));

        Assert.All(names, name => Assert.Matches(KeyVaultSecretName, name));
        Assert.All(names, name => Assert.True(name.Length <= 127, $"Name was {name.Length} characters."));
    }

    private async Task<List<string>> CaptureSecretNamesFor(string facilityId)
    {
        var queries = new Mock<IFhirQueryConfigurationQueries>();
        queries
            .Setup(x => x.GetAuthenticationConfigurationByFacilityId(facilityId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((AuthenticationConfigurationModel?)null);

        var names = new List<string>();
        var secretManager = new Mock<ISecretManager>();
        secretManager
            .Setup(x => x.SetSecretAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Callback<string, string, CancellationToken>((name, _, _) => names.Add(name))
            .ReturnsAsync(true);

        var manager = new Mock<IFhirQueryConfigurationManager>();
        manager
            .Setup(x => x.UpdateAuthenticationConfiguration(facilityId,
                                                            It.IsAny<AuthenticationConfiguration>(),
                                                            It.IsAny<CancellationToken>()))
            .ReturnsAsync(new AuthenticationConfigurationModel());

        var service = new FhirAuthenticationConfigurationService(manager.Object,
                                                                 queries.Object,
                                                                 secretManager.Object,
                                                                 _cacheService.Object,
                                                                 _logger.Object);

        await service.CreateOrUpdateAsync(facilityId, CreateRequest(), CancellationToken.None);
        return names;
    }

    // ---- GetAsync ----

    [Fact]
    public async Task GetAsync_NothingStored_ReturnsNull()
    {
        StoredConfiguration(null);

        Assert.Null(await CreateService().GetAsync(FacilityId, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_FacilityIsOnAnotherAuthType_ReturnsNull()
    {
        StoredConfiguration(new AuthenticationConfigurationModel { AuthType = nameof(AuthType.Basic) });

        Assert.Null(await CreateService().GetAsync(FacilityId, CancellationToken.None));
    }

    [Fact]
    public async Task GetAsync_StoredConfiguration_ResolvesTheClientIdAndReportsTheSecretAsStored()
    {
        StoredConfiguration(StoredOAuth("stored-secret-name"));
        SecretResolvesTo("existing-client-id-name", ClientIdValue);
        SecretResolvesTo("stored-secret-name", ClientSecretValue);

        var response = await CreateService().GetAsync(FacilityId, CancellationToken.None);

        Assert.NotNull(response);
        Assert.Equal(TokenUrl, response!.TokenUrl);
        Assert.Equal(Scope, response.Scope);
        Assert.Equal(ClientIdValue, response.ClientId);
        Assert.True(response.ClientSecretStored);
    }

    [Fact]
    public async Task GetAsync_SecretManagerReturnsNull_ReportsTheSecretAsNotStored()
    {
        StoredConfiguration(StoredOAuth("stored-secret-name"));
        SecretResolvesTo("existing-client-id-name", ClientIdValue);
        SecretResolvesTo("stored-secret-name", null);

        var response = await CreateService().GetAsync(FacilityId, CancellationToken.None);

        Assert.False(response!.ClientSecretStored);
    }

    /// <summary>
    /// Azure Key Vault throws for a secret that is not there, where the local manager returns null.
    /// Both must read as "not stored".
    /// </summary>
    [Fact]
    public async Task GetAsync_SecretManagerThrowsNotFound_ReportsTheSecretAsNotStored()
    {
        StoredConfiguration(StoredOAuth("stored-secret-name"));
        SecretResolvesTo("existing-client-id-name", ClientIdValue);
        SecretNotFound("stored-secret-name");

        var response = await CreateService().GetAsync(FacilityId, CancellationToken.None);

        Assert.False(response!.ClientSecretStored);
    }

    /// <summary>
    /// A permission problem or an unreachable vault is not the same as an absent secret, and must not
    /// be reported to the caller as "no secret stored".
    /// </summary>
    [Fact]
    public async Task GetAsync_SecretManagerFailsForAnyOtherReason_Propagates()
    {
        StoredConfiguration(StoredOAuth("stored-secret-name"));
        _secretManager
            .Setup(x => x.GetSecretAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(403, "Forbidden."));

        await Assert.ThrowsAsync<RequestFailedException>(
            () => CreateService().GetAsync(FacilityId, CancellationToken.None));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_BlankFacilityId_Throws(string facilityId)
    {
        await Assert.ThrowsAsync<BadRequestException>(
            () => CreateService().GetAsync(facilityId, CancellationToken.None));
    }
}
