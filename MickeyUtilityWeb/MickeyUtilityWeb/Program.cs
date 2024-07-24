using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MickeyUtilityWeb;
using MickeyUtilityWeb.Services;
using Microsoft.AspNetCore.Components.WebAssembly.Authentication;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Files.Read.All");
});

builder.Services.AddScoped<SGItineraryService>();

builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

// Configure base path for GitHub Pages
if (builder.HostEnvironment.BaseAddress.Contains("github.io"))
{
    builder.Services.AddScoped(sp =>
        new HttpClient
        {
            BaseAddress = new Uri(builder.HostEnvironment.BaseAddress + "MickeyUtilityWeb/")
        });
}

await builder.Build().RunAsync();