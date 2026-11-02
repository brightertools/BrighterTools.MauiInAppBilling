namespace BrighterTools.MauiInAppBilling;

public static class SubscriptionReplacementPolicy
{
    // Billing 9 item-level values. Same-product changes allow only FULL_PRICE / WITHOUT_PRORATION.
    // The server must retain the previously paid seat limit until renewal on a lower-tier change.
    public static int GoogleMode(bool sameProduct, bool reductionAtRenewal) => sameProduct
        ? reductionAtRenewal ? 3 : 4
        : reductionAtRenewal ? 5 : 2;
}

// Additive API: permanent-product contracts and their offline cache are unchanged.
public sealed record SubscriptionRegistration(string Key, string AppleProductId, string GoogleProductId, string GoogleBasePlanId);
public sealed record SubscriptionProduct(string Key, string ProductId, string Title, string FormattedPrice, string BillingPeriod, string? BasePlanId = null, string? OfferToken = null);
public sealed record SubscriptionPurchaseRequest(string ProductId, Guid AccountToken, string AccountId, string? BasePlanId = null, string? OfferToken = null,
    string? ReplacesPurchaseToken = null, bool DeferredReplacement = false);
public sealed record SubscriptionPurchase(string Provider, string ProductId, string TransactionId, string Proof, bool NeedsCompletion,
    string? Signature = null, string? OriginalTransactionId = null, string? AccountToken = null);
public sealed record SubscriptionQuery(bool Confirmed, IReadOnlyList<SubscriptionPurchase> Purchases, string? Message = null);
public sealed record SubscriptionDeliveryResult(bool Delivered, string? Message = null);
public interface ISubscriptionDelivery
{
    // Must verify with the provider and durably bind/deliver on the authenticated server.
    // A client signature check alone must never return Delivered=true.
    Task<SubscriptionDeliveryResult> VerifyAndDeliverAsync(SubscriptionPurchase purchase, CancellationToken token);
}
public interface IStoreSubscriptionProvider : IDisposable
{
    string Provider { get; }
    event EventHandler? PurchasesChanged;
    Task<IReadOnlyList<SubscriptionProduct>> GetProductsAsync(IReadOnlyList<SubscriptionRegistration> products, CancellationToken token);
    Task<PurchaseResult> PurchaseAsync(SubscriptionPurchaseRequest request, CancellationToken token);
    Task<SubscriptionQuery> QueryAsync(bool restore, CancellationToken token);
    Task CompleteAsync(SubscriptionPurchase purchase, CancellationToken token);
    Task OpenManagementAsync(CancellationToken token);
}
public interface ISubscriptionBillingService : IDisposable
{
    Task<IReadOnlyList<SubscriptionProduct>> GetProductsAsync(IReadOnlyList<SubscriptionRegistration> products, CancellationToken token = default);
    Task<PurchaseResult> PurchaseAsync(SubscriptionPurchaseRequest request, CancellationToken token = default);
    Task<PurchaseResult> RefreshAsync(bool restore = false, CancellationToken token = default);
    Task OpenManagementAsync(CancellationToken token = default);
}

public sealed class SubscriptionBillingService(IStoreSubscriptionProvider provider, ISubscriptionDelivery delivery) : ISubscriptionBillingService
{
    private readonly SemaphoreSlim gate = new(1);
    private readonly HashSet<string> configured = new(StringComparer.Ordinal);
    private bool disposed;

    public async Task<IReadOnlyList<SubscriptionProduct>> GetProductsAsync(IReadOnlyList<SubscriptionRegistration> products, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            configured.Clear();
            foreach (var product in products)
            {
                var id = provider.Provider == "Apple" ? product.AppleProductId : product.GoogleProductId;
                if (!string.IsNullOrWhiteSpace(id)) configured.Add(id);
            }
            return await provider.GetProductsAsync(products, token);
        }
        finally { gate.Release(); }
    }
    public async Task<PurchaseResult> PurchaseAsync(SubscriptionPurchaseRequest request, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!configured.Contains(request.ProductId) || request.AccountToken == Guid.Empty || string.IsNullOrWhiteSpace(request.AccountId))
                return new(PurchaseStatus.Unavailable, "Prepare this subscription with the server before purchasing.");
            var result = await provider.PurchaseAsync(request, token);
            return result.Status is PurchaseStatus.Purchased or PurchaseStatus.AlreadyOwned ? await DeliverAsync(false, token) : result;
        }
        finally { gate.Release(); }
    }
    public async Task<PurchaseResult> RefreshAsync(bool restore = false, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try { ObjectDisposedException.ThrowIf(disposed, this); return await DeliverAsync(restore, token); }
        finally { gate.Release(); }
    }
    private async Task<PurchaseResult> DeliverAsync(bool restore, CancellationToken token)
    {
        var query = await provider.QueryAsync(restore, token);
        if (!query.Confirmed) return new(PurchaseStatus.Unavailable, query.Message ?? "The store could not confirm subscription status.");
        if (query.Purchases.Count == 0) return new(PurchaseStatus.Unavailable, "No current subscription was returned. Your organisation's server status remains authoritative.");
        var delivered = false;
        foreach (var purchase in query.Purchases)
        {
            if (!configured.Contains(purchase.ProductId)) continue;
            // Do not persist ownership locally or finalise on a pending/unverified server response.
            var result = await delivery.VerifyAndDeliverAsync(purchase, token);
            if (!result.Delivered) return new(PurchaseStatus.Unverified, result.Message ?? "Subscription verification is pending. Restore or refresh to retry.");
            delivered = true;
            if (purchase.NeedsCompletion) await provider.CompleteAsync(purchase, token);
        }
        return delivered ? new(PurchaseStatus.Purchased, "Subscription verified by the server.") : new(PurchaseStatus.Unavailable, "No configured subscription was found.");
    }
    public Task OpenManagementAsync(CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return provider.OpenManagementAsync(token);
    }
    public void Dispose() { disposed = true; provider.Dispose(); }
}
