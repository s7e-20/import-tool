namespace NVGI.Projet.Importtool.Services;

public sealed class ApiKeyMiddleware
{
    private readonly RequestDelegate _next;

    public ApiKeyMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context, ApiKeyStore keys)
    {
        if (!RequiresKey(context.Request.Path))
        {
            await _next(context);
            return;
        }

        if (!keys.IsValid(ReadKey(context.Request)))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new { message = "Sign in again." });
            return;
        }

        await _next(context);
    }

    public static string? ReadKey(HttpRequest request)
    {
        var header = request.Headers.Authorization.ToString();
        const string prefix = "Bearer ";
        if (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return header[prefix.Length..].Trim();
        return null;
    }

    private static bool RequiresKey(PathString path)
    {
        if (!path.StartsWithSegments("/api"))
            return false;
        return !path.StartsWithSegments("/api/auth/login");
    }
}
