using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OAuth;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Authentication.CdcSams
{
    public class SamsOptions : OAuthOptions
    {
        public SamsOptions()
        {
            CallbackPath = "/signin-sams";
            Scope.Add("openid");
            Scope.Add("profile");
            Scope.Add("email");

            ClaimActions.MapJsonKey("sub", "sub");
            ClaimActions.MapJsonSubKey("account_type", "profile", "account_type");
            ClaimActions.MapJsonSubKey("account_id", "profile", "account_id");
            ClaimActions.MapJsonSubKey("name", "profile", "name");
            ClaimActions.MapJsonSubKey("family_name", "profile", "family_name");
            ClaimActions.MapJsonSubKey("middle_name", "profile", "middle_name");
            ClaimActions.MapJsonSubKey("given_name", "profile", "given_name");
            ClaimActions.MapJsonSubKey("preferred_name", "profile", "preferred_name");
            ClaimActions.MapJsonSubKey("name_suffix", "profile", "name_suffix");
            ClaimActions.MapJsonKey("email", "email");
        }

        public override void Validate()
        {
            ArgumentException.ThrowIfNullOrEmpty(AppId);
            ArgumentException.ThrowIfNullOrEmpty(AppSecret);

            base.Validate();
        }

        public string AppId
        {
            get { return ClientId; }
            set { ClientId = value; }
        }

        public string AppSecret
        {
            get { return ClientSecret; }
            set { ClientSecret = value; }
        }   
    }
}
