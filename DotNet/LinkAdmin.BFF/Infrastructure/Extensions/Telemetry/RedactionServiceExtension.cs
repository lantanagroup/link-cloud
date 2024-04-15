using LantanaGroup.Link.LinkAdmin.BFF.Application.Models;
using LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Logging;
using Microsoft.Extensions.Compliance.Classification;
using Microsoft.Extensions.Compliance.Redaction;
using System.Text;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.Telemetry
{
    public static class RedactionServiceExtension
    {

        public static IServiceCollection AddRedactionService(this IServiceCollection services, Action<RedactionServiceOptions>? options = null)
        {
            var redactionServiceOptions = new RedactionServiceOptions();
            options?.Invoke(redactionServiceOptions);

            services.AddRedaction(x =>
            {

                x.SetRedactor<StarRedactor>(new DataClassificationSet(DataTaxonomy.SensitiveData));


                if (!string.IsNullOrEmpty(redactionServiceOptions.HmacKey))
                {
                    x.SetHmacRedactor(opts =>
                    {
                        opts.Key = Convert.ToBase64String(Encoding.UTF8.GetBytes(redactionServiceOptions.HmacKey));
                        opts.KeyId = 808;
                    }, new DataClassificationSet(DataTaxonomy.PiiData));
                }
            });

            return services;
        }

        public class RedactionServiceOptions
        {
            public string? HmacKey { get; set; } = null;
        }
    }
}
