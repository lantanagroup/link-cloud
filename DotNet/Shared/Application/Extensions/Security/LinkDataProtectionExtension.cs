using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LantanaGroup.Link.Shared.Application.Extensions.Security
{
    public static class LinkDataProtectionExtension
    {
        public static IServiceCollection AddLinkDataProtection(this IServiceCollection services, Action<LinkDataProtectionOptions>? options)
        {
            var linkDataProtectionOptions = new LinkDataProtectionOptions();
            options?.Invoke(linkDataProtectionOptions);

            if(linkDataProtectionOptions.Environment.IsDevelopment())
            {
                services.AddDataProtection()
                    .SetApplicationName(linkDataProtectionOptions.KeyRing)
                    .DisableAutomaticKeyGeneration();
            }       
          
            return services;
        }

        public class LinkDataProtectionOptions
        {
            public IWebHostEnvironment Environment { get; set; } = null!;
            public string KeyRing { get; set; } = "Link";
        }
    }
}
