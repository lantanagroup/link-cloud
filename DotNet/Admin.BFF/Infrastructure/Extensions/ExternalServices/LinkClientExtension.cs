using LantanaGroup.Link.LinkAdmin.BFF.Application.Clients;

namespace LantanaGroup.Link.LinkAdmin.BFF.Infrastructure.Extensions.ExternalServices
{
    public static class LinkClientExtension
    {
        public static IServiceCollection AddLinkClients(this IServiceCollection services)
        {
            services.AddHttpClient<AccountService>();
            services.AddHttpClient<AuditService>();
            services.AddHttpClient<CensusService>();
            services.AddHttpClient<DataAcquisitionService>();
            services.AddHttpClient<MeasureEvalService>();
            services.AddHttpClient<NormalizationService>();
            services.AddHttpClient<NotificationService>();           
            services.AddHttpClient<QueryDispatchService>();
            services.AddHttpClient<ReportService>();
            services.AddHttpClient<SubmissionService>();   
            services.AddHttpClient<TenantService>();
            services.AddHttpClient<ValidationService>();

            return services;
        }
    }
}
