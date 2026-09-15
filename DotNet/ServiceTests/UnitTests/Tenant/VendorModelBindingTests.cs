using LantanaGroup.Link.Shared.Application.Models.Tenant;
using System.Text.Json;

namespace UnitTests.Tenant;

public class VendorModelBindingTests
{
    private static readonly JsonSerializerOptions WebDefaults = new(JsonSerializerDefaults.Web);

    [Fact]
    public void CreateVendorModel_BindsTheNestedSigningKeySecretId()
    {
        const string payload = """
            { "name": "Epic", "authentication": { "signingKeySecretId": "epic-signing-key" } }
            """;

        var model = JsonSerializer.Deserialize<CreateVendorModel>(payload, WebDefaults);

        Assert.Equal("Epic", model?.Name);
        Assert.Equal("epic-signing-key", model?.Authentication?.SigningKeySecretId);
    }

    [Fact]
    public void UpdateVendorModel_BindsTheNestedSigningKeySecretId()
    {
        const string payload = """
            { "name": "Epic", "authentication": { "signingKeySecretId": "epic-signing-key" } }
            """;

        var model = JsonSerializer.Deserialize<UpdateVendorModel>(payload, WebDefaults);

        Assert.Equal("epic-signing-key", model?.Authentication?.SigningKeySecretId);
    }

    [Fact]
    public void UpdateVendorModel_DistinguishesAClearedKeyFromAnOmittedOne()
    {
        var cleared = JsonSerializer.Deserialize<UpdateVendorModel>(
            """{ "name": "Epic", "authentication": { "signingKeySecretId": null } }""", WebDefaults);
        var omitted = JsonSerializer.Deserialize<UpdateVendorModel>(
            """{ "name": "Epic" }""", WebDefaults);

        Assert.NotNull(cleared?.Authentication);
        Assert.Null(cleared!.Authentication!.SigningKeySecretId);
        Assert.Null(omitted?.Authentication);
    }
}
