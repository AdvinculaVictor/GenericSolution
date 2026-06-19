using Microsoft.AspNetCore.Authorization;
using System.Linq;
using System.Threading.Tasks;

namespace GenericSolution.API.Policies
{
    public class ScopeHandler : AuthorizationHandler<ScopeRequirement>
    {
        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, ScopeRequirement requirement)
        {
            var scopeClaim = context.User.Claims.FirstOrDefault(c => (c.Type == "scp"||c.Type == "roles") && c.Issuer == requirement.Issuer);
            var requirements = context.Requirements.OfType<ScopeRequirement>().Where(r => r.Issuer == requirement.Issuer);
            foreach (var req in requirements)
            {
                if (scopeClaim != null)
                {
                    var scopes = scopeClaim.Value.Split(' ');
                    if (scopes.Any(s => s.Equals(req.Scope, StringComparison.OrdinalIgnoreCase)))
                    {
                        context.Succeed(requirement);
                        return Task.CompletedTask;
                    }
                }
            }
            // if (scopeClaim != null)
            // {
            //     var scopes = scopeClaim.Value.Split(' ');
            //     if (scopes.Any(s => s.Equals(requirement.Scope, StringComparison.OrdinalIgnoreCase)))
            //     {
            //         context.Succeed(requirement);
            //     }
            // }
            return Task.CompletedTask;
        }
    }
}