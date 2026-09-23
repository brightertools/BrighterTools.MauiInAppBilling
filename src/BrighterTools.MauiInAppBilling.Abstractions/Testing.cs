namespace BrighterTools.MauiInAppBilling;
public sealed class MemoryBillingStateStore : IBillingStateStore
{
    private readonly Dictionary<string,string> values = [];
    public Task<string?> ReadAsync(string key) => Task.FromResult(values.GetValueOrDefault(key));
    public Task WriteAsync(string key, string value) { values[key] = value; return Task.CompletedTask; }
}
public sealed class UnavailableBillingProvider : IStoreBillingProvider
{
    public string StoreName => "Unavailable";
    public event EventHandler? PurchasesChanged { add { } remove { } }
    public string ProductId(ProductRegistration p) => "";
    public Task<IReadOnlyList<BillingProduct>> GetProductsAsync(IReadOnlyList<ProductRegistration> p, CancellationToken t) => Task.FromResult<IReadOnlyList<BillingProduct>>([]);
    public Task<EntitlementQuery> RefreshAsync(CancellationToken t) => Task.FromResult(new EntitlementQuery(true, [], "Purchases are available in Store builds only."));
    public Task<EntitlementQuery> RestoreAsync(CancellationToken t) => RefreshAsync(t);
    public Task<StorePurchaseResult> PurchaseAsync(string p, CancellationToken t) => Task.FromResult(new StorePurchaseResult(PurchaseStatus.Unavailable));
    public Task CompleteAsync(StorePurchase p, CancellationToken t) => Task.CompletedTask;
    public void Dispose() { }
}
// Explicitly injected by tests or the demo. The consumer app never discovers this automatically.
public sealed class FakeBillingProvider : IStoreBillingProvider
{
    public string StoreName => "Fake";
    public bool Owned { get; set; }
    public bool Available { get; set; } = true;
    public bool Verified { get; set; } = true;
    public bool FailCompletion { get; set; }
    public int CompletionCount { get; private set; }
    public PurchaseStatus NextPurchase { get; set; } = PurchaseStatus.Purchased;
    public event EventHandler? PurchasesChanged;
    public string ProductId(ProductRegistration p) => p.Key;
    public void Notify() => PurchasesChanged?.Invoke(this, EventArgs.Empty);
    public Task<IReadOnlyList<BillingProduct>> GetProductsAsync(IReadOnlyList<ProductRegistration> p, CancellationToken t) =>
        Task.FromResult<IReadOnlyList<BillingProduct>>(p.Select(x => new BillingProduct(x.Key, x.Key, "Test upgrade", "Test price")).ToArray());
    public Task<EntitlementQuery> RefreshAsync(CancellationToken t) => Task.FromResult(new EntitlementQuery(Available,
        Owned ? [new("pro", "fake-transaction", Verified, CompletionCount == 0)] : [], Available ? null : "Offline test"));
    public Task<EntitlementQuery> RestoreAsync(CancellationToken t) => RefreshAsync(t);
    public Task<StorePurchaseResult> PurchaseAsync(string p, CancellationToken t)
    { if (NextPurchase is PurchaseStatus.Purchased or PurchaseStatus.AlreadyOwned) Owned = true; return Task.FromResult(new StorePurchaseResult(NextPurchase)); }
    public Task CompleteAsync(StorePurchase p, CancellationToken t)
    { if (FailCompletion) throw new IOException("Test completion failure"); CompletionCount++; return Task.CompletedTask; }
    public void Dispose() { }
}
