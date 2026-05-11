using McBrokers.Infrastructure;
using McBrokers.Infrastructure.Observability;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseMcBrokersSerilog();

builder.Services.AddMcBrokersTelemetry(builder.Configuration);
builder.Services.AddMcBrokersInfrastructure(builder.Configuration);

builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin", "RequireAdmin");
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseMcBrokersCorrelationId();
app.UseSerilogRequestLogging();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
