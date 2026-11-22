using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Ims.Api.Infrastructure;

/// <summary>
/// Global filter: every unsafe request (POST/PUT/DELETE/PATCH) must carry a valid antiforgery token in the
/// "X-XSRF-TOKEN" header. Safe methods (GET/HEAD/OPTIONS) pass. This is what [AutoValidateAntiforgeryToken]
/// does in MVC, written against IAntiforgery directly because an API-only app does not register the view services
/// that attribute needs.
/// </summary>
public class ValidateAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var method = context.HttpContext.Request.Method;
        if (HttpMethods.IsGet(method) || HttpMethods.IsHead(method) || HttpMethods.IsOptions(method) || HttpMethods.IsTrace(method))
            return;
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = 400, Title = "Invalid antiforgery token", Detail = "Reload the page and try again."
            }) { StatusCode = 400 };
        }
    }
}
