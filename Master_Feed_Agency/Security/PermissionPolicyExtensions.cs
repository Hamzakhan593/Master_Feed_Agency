using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace Master_Feed_Agency.Security;

public static class PermissionPolicyExtensions
{
    public static IServiceCollection AddMasterFeedAuthorization(this IServiceCollection services)
    {
        services.AddAuthorization(options =>
        {
            foreach (var permission in AppPermissions.All)
            {
                options.AddPolicy(permission.Value, policy =>
                {
                    policy.RequireAuthenticatedUser();
                    policy.RequireClaim(AppPermissions.ClaimType, permission.Value);
                });
            }
        });

        return services;
    }

    public static bool HasPermission(this ClaimsPrincipal user, string permission) =>
        user.HasClaim(AppPermissions.ClaimType, permission);
}
