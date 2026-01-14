using RestSharp;
using System.Text;
using System.Text.Json;

namespace LantanaGroup.Link.Tests.E2ETests;

public static class AuthHelper
{
    public static string GetBasicAuthorization(TestConfig.BasicAuthConfig config)
    {
        if (string.IsNullOrEmpty(config.Username))
            throw new InvalidOperationException("Basic auth username is not configured");
            
        if (string.IsNullOrEmpty(config.Password))
            throw new InvalidOperationException("Basic auth password is not configured");
    
        var authString = $"{config.Username}:{config.Password}";
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(authString));
    }
    
    public static string GetBearerToken(TestConfig.OAuthConfig config)
    {
        if (string.IsNullOrEmpty(config.TokenEndpoint))
            throw new InvalidOperationException("OAuth token endpoint is not configured");
        
        if (string.IsNullOrEmpty(config.ClientId))
            throw new InvalidOperationException("OAuth client ID is not configured");
            
        if (string.IsNullOrEmpty(config.Username))
            throw new InvalidOperationException("OAuth username is not configured");
            
        if (string.IsNullOrEmpty(config.Password))
            throw new InvalidOperationException("OAuth password is not configured");
    
        var client = new RestClient(config.TokenEndpoint);
        var request = new RestRequest("", Method.Post);
        
        request.AddParameter("grant_type", "password");
        request.AddParameter("client_id", config.ClientId);
        if (!string.IsNullOrEmpty(config.ClientSecret))
            request.AddParameter("client_secret", config.ClientSecret);
        request.AddParameter("username", config.Username);
        request.AddParameter("password", config.Password);
        request.AddParameter("scope", config.Scope);
    
        var response = client.Execute(request);
        
        if (!response.IsSuccessful)
            throw new InvalidOperationException($"Failed to obtain OAuth token: {response.ErrorMessage}");
    
        var responseModel = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(response.Content!);
        
        if (!responseModel.ContainsKey("access_token"))
            throw new InvalidOperationException("OAuth token response does not contain access token");
        
        return responseModel["access_token"].GetString();
    }
}