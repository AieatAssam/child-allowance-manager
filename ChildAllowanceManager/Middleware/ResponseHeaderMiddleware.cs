namespace ChildAllowanceManager.Middleware;

public class ResponseHeaderMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // allow embedding from anywhere
        context.Response.Headers.Remove("x-frame-options");
        //context.Response.Headers.ContentSecurityPolicy = "frame-ancestors 'self' *"; // <== now handled in program.cs
        await next.Invoke(context);
    }
}