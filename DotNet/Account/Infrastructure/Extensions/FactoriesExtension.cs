using LantanaGroup.Link.Account.Application.Factories.Role;
using LantanaGroup.Link.Account.Application.Factories.User;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.Role;
using LantanaGroup.Link.Account.Application.Interfaces.Factories.User;
using LantanaGroup.Link.Shared.Application.Factories;
using LantanaGroup.Link.Shared.Application.Interfaces;
using LantanaGroup.Link.Shared.Application.Models.Kafka;

namespace LantanaGroup.Link.Account.Infrastructure.Extensions
{
    public static class FactoriesExtension
    {
        public static IServiceCollection AddFactories(this IServiceCollection services)
        {
            services.AddTransient<IKafkaProducerFactory<string, object>, KafkaProducerFactory<string, object>>();
            
            //Add user factories
            services.AddTransient<ILinkUserModelFactory, LinkUserModelFactory>();
            services.AddTransient<IGroupedUserModelFactory, GroupedUserModelFactory>();
            services.AddTransient<IUserSearchFilterRecordFactory, UserSearchFilterRecordFactory>();
            
            //Add role factories
            services.AddTransient<ILinkRoleModelFactory, LinkRoleModelFactory>();
            services.AddTransient<IListRoleModelFactory, ListRoleModelFactory>();

            return services;
        }
    }
}
