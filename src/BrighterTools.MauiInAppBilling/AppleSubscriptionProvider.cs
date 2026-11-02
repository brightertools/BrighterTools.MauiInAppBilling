#if IOS || MACCATALYST
namespace BrighterTools.MauiInAppBilling;
internal sealed class AppleSubscriptionProvider : IStoreSubscriptionProvider
{
    public string Provider => "Apple";
    public event EventHandler? PurchasesChanged;
    public AppleSubscriptionProvider() => AppleBillingProvider.NativeChanged += Changed;
    private void Changed(object? sender, EventArgs e) => PurchasesChanged?.Invoke(this, e);
    public async Task<IReadOnlyList<SubscriptionProduct>> GetProductsAsync(IReadOnlyList<SubscriptionRegistration> products, CancellationToken token)
    {
        var result = await AppleBillingProvider.Call("subscription-products", products.Select(p => p.AppleProductId).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray(), token);
        return result.GetProperty("products").EnumerateArray().Select(p => {
            var id = p.GetProperty("id").GetString()!;
            return new SubscriptionProduct(products.Single(x => x.AppleProductId == id).Key, id, p.GetProperty("title").GetString()!,
                p.GetProperty("price").GetString()!, p.GetProperty("period").GetString()!);
        }).ToArray();
    }
    public async Task<SubscriptionQuery> QueryAsync(bool restore, CancellationToken token)
    {
        try
        {
            var result = await AppleBillingProvider.Call(restore ? "subscription-restore" : "subscription-refresh", "", token);
            return new(true, result.GetProperty("purchases").EnumerateArray().Where(p => p.GetProperty("verified").GetBoolean()).Select(p => new SubscriptionPurchase(
                Provider, p.GetProperty("product").GetString()!, p.GetProperty("transaction").GetString()!, p.GetProperty("proof").GetString()!,
                p.GetProperty("finish").GetBoolean(), OriginalTransactionId: p.GetProperty("originalTransaction").GetString(),
                AccountToken: p.GetProperty("accountToken").GetString())).ToArray());
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new(false, [], ex.Message); }
    }
    public async Task<PurchaseResult> PurchaseAsync(SubscriptionPurchaseRequest request, CancellationToken token)
    {
        var result = await AppleBillingProvider.Call("subscription-purchase", request, token);
        return new(Enum.Parse<PurchaseStatus>(result.GetProperty("status").GetString()!), result.TryGetProperty("message", out var m) ? m.GetString() : null);
    }
    public async Task CompleteAsync(SubscriptionPurchase purchase, CancellationToken token) => _ = await AppleBillingProvider.Call("finish", purchase.TransactionId, token);
    public async Task OpenManagementAsync(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        await Launcher.Default.OpenAsync("https://apps.apple.com/account/subscriptions");
    }
    public void Dispose() => AppleBillingProvider.NativeChanged -= Changed;
}
#endif
