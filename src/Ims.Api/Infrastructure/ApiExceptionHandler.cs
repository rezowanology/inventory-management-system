using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace Ims.Api.Infrastructure;

/// <summary>
/// The ONE place where our exceptions become HTTP answers (RFC 7807 ProblemDetails).
/// Registered with AddExceptionHandler in Program.cs; unknown exceptions fall through to a generic 500.
/// </summary>
public class ApiExceptionHandler(IProblemDetailsService problemDetails) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext http, Exception ex, CancellationToken ct)
    {
        ProblemDetails? pd = ex switch
        {
            DomainException d => Build(422, "Business rule violated", d.Message, d.Code, d.Errors),
            DuplicateScanException s => BuildDuplicate(s),
            _ => null
        };
        if (pd is null) return false;

        http.Response.StatusCode = pd.Status!.Value;
        return await problemDetails.TryWriteAsync(new ProblemDetailsContext { HttpContext = http, ProblemDetails = pd });
    }

    private static ProblemDetails Build(int status, string title, string detail, string code, IDictionary<string, string[]>? errors)
    {
        ProblemDetails pd = errors is null ? new ProblemDetails() : new ValidationProblemDetails(errors);
        pd.Status = status;
        pd.Title = title;
        pd.Detail = detail;
        pd.Extensions["code"] = code;
        return pd;
    }

    private static ProblemDetails BuildDuplicate(DuplicateScanException s)
    {
        var pd = new ProblemDetails { Status = 409, Type = "duplicate-scan", Title = "Duplicate scan", Detail = s.Message };
        pd.Extensions["when"] = s.When;
        pd.Extensions["where"] = s.Where;
        pd.Extensions["by"] = s.By;
        return pd;
    }
}
