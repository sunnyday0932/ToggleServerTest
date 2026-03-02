using System.Security.Claims;

namespace ToggleServer.Api.Middleware;

public class MockAuthMiddleware
{
    private readonly RequestDelegate _next;

    public MockAuthMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // 只有 Management API 需要驗證
        if (!context.Request.Path.StartsWithSegments("/api/v1/management"))
        {
            await _next(context);
            return;
        }

        // 簡單驗證 Header (暫時註解，方便本地免 Auth 測試)
        // if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) ||
        //     !authHeader.ToString().StartsWith("Bearer mock-token"))
        // {
        //     context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        //     await context.Response.WriteAsync("Unauthorized: Missing or invalid mock token.");
        //     return;
        // }

        // TODO: 之後實作真正的 Auth 時，要從 Token/Cookie 解出實際使用者的 Identity
        // 目前先寫死一個預設的 Identity 讓所有 Request 都能暢通無阻並帶有預設的 Operator 資訊
        var claims = new[]
        {
            new Claim(ClaimTypes.Name, "local_test_operator"),
            new Claim(ClaimTypes.Role, "Admin")
        };
        var identity = new ClaimsIdentity(claims, "MockAuth");
        context.User = new ClaimsPrincipal(identity);

        await _next(context);
    }
}
