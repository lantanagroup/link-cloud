using LantanaGroup.Link.Account.Application.Commands.AuditEvent;
using LantanaGroup.Link.Account.Application.Commands.Role;
using LantanaGroup.Link.Account.Application.Commands.User;
using LantanaGroup.Link.Account.Application.Queries.Role;
using LantanaGroup.Link.Account.Application.Queries.User;

namespace LantanaGroup.Link.Account.Infrastructure.Extensions
{
    public static class CommandAndQueryExtension
    {
        public static IServiceCollection AddCommandAndQueries(this IServiceCollection services)
        {
            //register user commands and queries
            services.AddTransient<ICreateUser, CreateUser>();
            services.AddTransient<IUpdateUser, UpdateUser>();
            services.AddTransient<IDeleteUser, DeleteUser>();
            services.AddTransient<IActiviateUser, ActivateUser>();
            services.AddTransient<IDeactivateUser, DeactivateUser>();
            services.AddTransient<IDeleteUser, DeleteUser>();
            services.AddTransient<IRecoverUser, RecoverUser>();
            services.AddTransient<IUpdateUserClaims, UpdateUserClaims>();
            services.AddTransient<IGetUserByid, GetUserById>();
            services.AddTransient<IGetUserByEmail, GetUserByEmail>();
            services.AddTransient<IGetLinkUserEntity, GetLinkUserEntity>();
            services.AddTransient<IGetRoleUsers, GetRoleUsers>();
            services.AddTransient<IGetFacilityUsers, GetFacilityUsers>();
            services.AddTransient<ISearchUsers, SearchUsers>();
            services.AddTransient<ISearchFacilityUsers, SearchFacilityUsers>();

            //register role commands and queries
            services.AddTransient<ICreateRole, CreateRole>();
            services.AddTransient<IUpdateRole, UpdateRole>();
            services.AddTransient<IDeleteRole, DeleteRole>();
            services.AddTransient<IUpdateRoleClaims, UpdateRoleClaims>();
            services.AddTransient<IGetRole, GetRole>();
            services.AddTransient<IGetRoleByName, GetRoleByName>();
            services.AddTransient<IGetRoles, GetRoles>();

            //register audit event command
            services.AddTransient<ICreateAuditEvent, CreateAuditEvent>();

            return services;
        }
    }
}
