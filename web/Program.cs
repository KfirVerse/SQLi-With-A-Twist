// ============================================================================
//  MSSQL "SQLi -> RCE" Lab - web front end (ASP.NET Core 8, Razor Pages)
//  INTENTIONALLY INSECURE.  Educational, localhost-only.  See README.
// ============================================================================

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

// Don't let Kestrel advertise itself; we optionally spoof an IIS "Server"
// header below so Burp screenshots look like a classic Windows/.NET target.
builder.WebHost.ConfigureKestrel(options => options.AddServerHeader = false);

var app = builder.Build();

// ----------------------------------------------------------------------------
//  VERBOSE ERRORS ON PURPOSE — but MINIMAL.
//  Instead of the framework's large HTML "developer exception page", we surface
//  ONLY the raw SQL exception text with an HTTP 500. In Burp the entire response
//  body is then just the SQL error message — and for a failed CONVERT/UNION that
//  message contains the leaked value. This is the error-based extraction channel.
//  Real applications must NEVER expose raw errors like this — see README.
// ----------------------------------------------------------------------------
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.Clear();
            context.Response.StatusCode = 500;
            context.Response.ContentType = "text/plain; charset=utf-8";
            await context.Response.WriteAsync(ex.Message);
        }
    }
});

// ----------------------------------------------------------------------------
//  Cosmetic only: make responses resemble an IIS / ASP.NET (Framework) app so
//  screenshots in Burp Suite look authentic.  Toggle with FakeIisHeaders.
// ----------------------------------------------------------------------------
if (app.Configuration.GetValue<bool>("FakeIisHeaders"))
{
    app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            headers["Server"] = "Microsoft-IIS/10.0";
            headers["X-Powered-By"] = "ASP.NET";
            headers["X-AspNet-Version"] = "4.0.30319";
            return Task.CompletedTask;
        });
        await next();
    });
}

app.MapRazorPages();

app.Run();
