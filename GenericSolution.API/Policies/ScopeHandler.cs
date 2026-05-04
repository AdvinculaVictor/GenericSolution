using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;

namespace GenericSolution.API.Policies
{
    public class ScopeHandler : AuthorizationHandler<ScopeRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
        {
            var scopeClaim = context.User.Claims.FirstOrDefault(c => c.Type == "scp" && c.Issuer == requirement.Issuer);
            if (scopeClaim != null)
            {
                var scopes = scopeClaim.Value.Split(' ');
                if (scopes.Any(s => s.Equals(requirement.Scope, StringComparison.OrdinalIgnoreCase)))
                {
                    context.Succeed(requirement);
                }
            }
            return Task.CompletedTask;
        }
    }
}