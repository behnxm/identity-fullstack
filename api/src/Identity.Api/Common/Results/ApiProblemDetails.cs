using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Identity.Api.Common.Results;

/// <summary>
/// Factory for RFC 9457 Problem Details objects with stable machine-readable codes and tracing.
/// </summary>
public static class ApiProblemDetails
{
    public static ProblemDetails Create(
        int statusCode,
        string code,
        string title,
        string? detail = null,
        string? instance = null,
        IDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = title,
            Detail = detail,
            Instance = instance,
            Type = $"https://identity.local/errors/{code}"
        };

        problem.Extensions["code"] = code;
        problem.Extensions["traceId"] = Activity.Current?.Id ?? Guid.NewGuid().ToString();

        if (extensions != null)
        {
            foreach (var (key, value) in extensions)
            {
                problem.Extensions[key] = value;
            }
        }

        return problem;
    }

    public static IResult CreateResult(
        int statusCode,
        string code,
        string title,
        string? detail = null,
        string? instance = null,
        IDictionary<string, object?>? extensions = null)
    {
        var problem = Create(statusCode, code, title, detail, instance, extensions);
        return TypedResults.Problem(problem);
    }
}
