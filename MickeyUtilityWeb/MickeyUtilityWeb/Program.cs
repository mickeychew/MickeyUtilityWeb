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
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Files.ReadWrite.All");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Files.Read.All");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Files.ReadWrite");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Files.Read");
    options.ProviderOptions.DefaultAccessTokenScopes.Add("https://graph.microsoft.com/Sites.ReadWrite.All");
    options.ProviderOptions.LoginMode = "redirect";

    // Set the correct redirect URI
    var baseUri = builder.HostEnvironment.BaseAddress.TrimEnd('/');
    var githubPagesUri = "https://mickeychew.github.io/MickeyUtilityWeb";

    if (baseUri.StartsWith(githubPagesUri))
    {
        options.ProviderOptions.Authentication.RedirectUri = $"{githubPagesUri}/authentication/login-callback";
        options.ProviderOptions.Authentication.PostLogoutRedirectUri = githubPagesUri;
    }
    else
    {
        options.ProviderOptions.Authentication.RedirectUri = $"{baseUri}/authentication/login-callback";
        options.ProviderOptions.Authentication.PostLogoutRedirectUri = baseUri;
    }
});

builder.Services.AddScoped<SGItineraryService>();

await builder.Build().RunAsync();