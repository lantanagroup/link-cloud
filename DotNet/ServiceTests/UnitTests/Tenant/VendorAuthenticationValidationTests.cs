using LantanaGroup.Link.Shared.Application.Models.Tenant;
using System.ComponentModel.DataAnnotations;

namespace UnitTests.Tenant;

public class VendorAuthenticationValidationTests
{
    private static IList<ValidationResult> Validate(string? signingKeySecretId)
    {
        var settings = new VendorAuthenticationSettings { SigningKeySecretId = signingKeySecretId };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(settings, new ValidationContext(settings), results, validateAllProperties: true);

        return results;
    }

    [Fact]
    public void NullSecretId_IsValid_BecauseItClearsTheAssociation()
    {
        Assert.Empty(Validate(null));
    }

    [Fact]
    public void ValidSecretId_IsAccepted()
    {
        Assert.Empty(Validate("epic-signing-key"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t")]
    public void BlankSecretId_IsRejected(string signingKeySecretId)
    {
        var results = Validate(signingKeySecretId);

        Assert.NotEmpty(results);
        Assert.Contains(nameof(VendorAuthenticationSettings.SigningKeySecretId),
            results.SelectMany(result => result.MemberNames));
    }

    [Theory]
    [InlineData("has space")]
    [InlineData("has/slash")]
    [InlineData("has_underscore")]
    [InlineData("has.dot")]
    public void SecretIdOutsideKeyVaultsCharacterSet_IsRejected(string signingKeySecretId)
    {
        Assert.NotEmpty(Validate(signingKeySecretId));
    }

    [Fact]
    public void SecretIdLongerThanKeyVaultAllows_IsRejected()
    {
        Assert.NotEmpty(Validate(new string('a', 128)));
    }

    [Fact]
    public void SecretIdAtKeyVaultsMaximumLength_IsAccepted()
    {
        Assert.Empty(Validate(new string('a', 127)));
    }
}
