using Microsoft.Extensions.DependencyInjection;
namespace BrighterTools.MauiInAppBilling;
public static class SubscriptionRegistrationExtensions
{
    // Register a server-backed ISubscriptionDelivery separately. There is deliberately no local verifier fallback.
    public static IServiceCollection AddMauiSubscriptionBilling(this IServiceCollection services, bool isStoreBuild)
    {
        services.AddTransient<IStoreSubscriptionProvider>(_ => CreateProvider(isStoreBuild));
        services.AddTransient<ISubscriptionBillingService, SubscriptionBillingService>();
        return services;
    }
    public static IStoreSubscriptionProvider CreateProvider(bool isStoreBuild)
    {
        if (!isStoreBuild) return new UnavailableSubscriptionProvider();
#if IOS || MACCATALYST
        return new AppleSubscriptionProvider();
#elif ANDROID
        return new AndroidSubscriptionProvider();
#else
        return new UnavailableSubscriptionProvider();
#endif
    }
}
internal sealed class UnavailableSubscriptionProvider : IStoreSubscriptionProvider
{
    public string Provider => "Unavailable";
    public event EventHandler? PurchasesChanged { add { } remove { } }
    public Task<IReadOnlyList<SubscriptionProduct>> GetProductsAsync(IReadOnlyList<SubscriptionRegistration> products, CancellationToken token) => Task.FromResult<IReadOnlyList<SubscriptionProduct>>([]);
    public Task<PurchaseResult> PurchaseAsync(SubscriptionPurchaseRequest request, CancellationToken token) => Task.FromResult(new PurchaseResult(PurchaseStatus.Unavailable, "Store subscription purchasing is not enabled in this build."));
    public Task<SubscriptionQuery> QueryAsync(bool restore, CancellationToken token) => Task.FromResult(new SubscriptionQuery(false, [], "Store subscription purchasing is not enabled in this build."));
    public Task CompleteAsync(SubscriptionPurchase purchase, CancellationToken token) => throw new InvalidOperationException("No store is available.");
    public Task OpenManagementAsync(CancellationToken token) => throw new InvalidOperationException("Store subscription management is unavailable in this build.");
    public void Dispose() { }
}
