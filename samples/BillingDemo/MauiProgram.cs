using BrighterTools.MauiInAppBilling;
using System.Reflection;
namespace BillingDemo;
public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder=MauiApp.CreateBuilder().UseMauiApp<App>();
#if DEMO_STORE
        builder.Services.AddMauiInAppBilling(o=>{
            string Value(string key)=>typeof(App).Assembly.GetCustomAttributes<AssemblyMetadataAttribute>().FirstOrDefault(a=>a.Key==key)?.Value??"";
            o.ApplicationId=AppInfo.PackageName; o.IsStoreBuild=true; o.AndroidPublicKey=Value("GooglePublicKey");
            o.Products.Add(new("pro",Value("WindowsOfferToken"),Value("AppleProductId"),Value("GoogleProductId")));
        },()=>Application.Current?.Windows.FirstOrDefault());
#else
        builder.Services.AddSingleton(new FakeBillingProvider());
        builder.Services.AddSingleton<IBillingService>(s=>new BillingService(s.GetRequiredService<FakeBillingProvider>(),new MemoryBillingStateStore(),
            new BillingOptions {ApplicationId="demo",Products={new("pro","pro","pro","pro")}}));
#endif
        builder.Services.AddSingleton<MainPage>(); return builder.Build();
    }
}