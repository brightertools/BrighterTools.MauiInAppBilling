using Microsoft.Extensions.DependencyInjection;
namespace BrighterTools.MauiInAppBilling;
public static class BillingRegistration
{
    public static IServiceCollection AddMauiInAppBilling(this IServiceCollection services, Action<BillingOptions> configure, Func<Window?> window)
    {
        var options = new BillingOptions(); configure(options);
        services.AddSingleton(options);
        services.AddSingleton<IBillingStateStore, SecureBillingStateStore>();
        services.AddSingleton<IStoreBillingProvider>(_ =>
        {
            if (!options.IsStoreBuild) return new UnavailableBillingProvider();
#if WINDOWS
            return new WindowsBillingProvider(window);
#elif IOS || MACCATALYST
            return new AppleBillingProvider();
#elif ANDROID
            return new AndroidBillingProvider(options);
#else
            return new UnavailableBillingProvider();
#endif
        });
        services.AddSingleton<IBillingService>(s => new BillingService(s.GetRequiredService<IStoreBillingProvider>(), s.GetRequiredService<IBillingStateStore>(), options, s.GetService<IPurchaseVerifier>()));
        return services;
    }
}
public sealed class SecureBillingStateStore : IBillingStateStore
{
    public Task<string?> ReadAsync(string key) => SecureStorage.Default.GetAsync(key);
    public Task WriteAsync(string key, string value) => SecureStorage.Default.SetAsync(key, value);
}
