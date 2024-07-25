using MickeyUtilityWeb.Services;
using MickeyUtilityWeb;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Determine the base address
var baseAddress = builder.HostEnvironment.BaseAddress;
if (baseAddress.Contains("github.io"))
{
    baseAddress = "https://mickeychew.github.io/MickeyUtilityWeb/";
}

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(baseAddress) });

builder.Services.AddMsalAuthentication(options =>
{
    builder.Configuration.Bind("AzureAd", options.ProviderOptions.Authentication);
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Files.ReadWrite.All");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Files.Read.All");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Files.ReadWrite");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Files.Read");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Sites.ReadWrite.All");
    options.ProviderOptions.LoginMode = "redirect";

    options.ProviderOptions.Authentication.RedirectUri = $"{baseAddress}authentication/login-callback";
    options.ProviderOptions.Authentication.PostLogoutRedirectUri = baseAddress;
});

builder.Services.AddScoped<SGItineraryService>();

await builder.Build().RunAsync();