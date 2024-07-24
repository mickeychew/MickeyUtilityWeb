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

    // Set the redirect URI dynamically based on the environment
    var baseUri = builder.HostEnvironment.BaseAddress.TrimEnd('/');
    if (baseUri.Contains("github.io"))
    {
        options.ProviderOptions.Authentication.RedirectUri = $"{baseUri}/authentication/login-callback";
    }
    else
    {
        options.ProviderOptions.Authentication.RedirectUri = $"{baseUri}/authentication/login-callback";
    }
});

builder.Services.AddScoped<SGItineraryService>();

await builder.Build().RunAsync();