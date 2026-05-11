using McBrokers.Infrastructure;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

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

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
