using System.Security.Claims;

namespace RelayWorks.ControlPlane.Api;

public sealed class TenantContext(IHttpContextAccessor accessor, IConfiguration configuration)
{
    public Guid RequireTenantId()
    {
        if (!configuration.GetValue("Authentication:Enabled", true))
            return Guid.Parse(configuration["Authentication:DevelopmentTenantId"]
                ?? throw new InvalidOperationException("DevelopmentTenantId is required when authentication is disabled."));
        var value = accessor.HttpContext?.User.FindFirstValue("relayworks_tenant_id");
        return Guid.TryParse(value, out var tenantId) ? tenantId
            : throw new UnauthorizedAccessException("The token does not contain a valid relayworks_tenant_id claim.");
    }

    public string RequireActorId() => accessor.HttpContext?.User.FindFirstValue("oid")
        ?? (configuration.GetValue("Authentication:Enabled", true) ? throw new UnauthorizedAccessException() : "local-developer");
}
