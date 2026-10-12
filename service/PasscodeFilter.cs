using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc.Filters;

namespace HouseholdApi.Services;

public class PasscodeFilter(IConfiguration configuration) : IAsyncActionFilter
{
    private readonly byte[] _passcode = Encoding.UTF8.GetBytes(
        configuration["Passcode"] ?? throw new InvalidOperationException("Passcode is not configured."));

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var supplied = Encoding.UTF8.GetBytes(context.HttpContext.Request.Headers["X-Passcode"].ToString());
        if (!CryptographicOperations.FixedTimeEquals(supplied, _passcode))
        {
            context.Result = new Microsoft.AspNetCore.Mvc.UnauthorizedResult();
            return;
        }

        await next();
    }
}
