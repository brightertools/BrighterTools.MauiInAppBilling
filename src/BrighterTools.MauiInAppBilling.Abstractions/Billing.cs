namespace BrighterTools.MauiInAppBilling;
public enum PurchaseStatus { Purchased, AlreadyOwned, Pending, Cancelled, Unavailable, Failed, Unverified }
public enum EntitlementStatus { Owned, NotOwned, Unavailable }
public sealed record BillingProduct(string Key, string StoreId, string Title, string FormattedPrice);
public sealed record ProductRegistration(string Key, string WindowsOfferToken, string AppleProductId, string GoogleProductId);
public sealed record PurchaseResult(PurchaseStatus Status, string? Message = null);
public sealed record EntitlementSnapshot(IReadOnlySet<string> OwnedKeys, bool IsCurrent, string? Message = null)
{
    public bool Owns(string key) => OwnedKeys.Contains(key);
}
public sealed class BillingOptions
{
    public List<ProductRegistration> Products { get; } = [];
    public bool IsStoreBuild { get; set; }
    public string? AndroidPublicKey { get; set; }
    public string ApplicationId { get; set; } = "";
}
public sealed record StorePurchase(string ProductId, string TransactionId, bool Verified, bool NeedsCompletion, string? Proof = null, string? Signature = null);
public sealed record EntitlementQuery(bool Confirmed, IReadOnlyList<StorePurchase> Purchases, string? Message = null);
public sealed record StorePurchaseResult(PurchaseStatus Status, StorePurchase? Purchase = null, string? Message = null);
public interface IStoreBillingProvider : IDisposable
{
    string StoreName { get; }
    event EventHandler? PurchasesChanged;
    string ProductId(ProductRegistration product);
    Task<IReadOnlyList<BillingProduct>> GetProductsAsync(IReadOnlyList<ProductRegistration> products, CancellationToken token);
    Task<EntitlementQuery> RefreshAsync(CancellationToken token);
    Task<StorePurchaseResult> PurchaseAsync(string productId, CancellationToken token);
    Task<EntitlementQuery> RestoreAsync(CancellationToken token);
    Task CompleteAsync(StorePurchase purchase, CancellationToken token);
}
public interface IBillingStateStore
{
    Task<string?> ReadAsync(string key);
    Task WriteAsync(string key, string value);
}
public interface IPurchaseVerifier
{
    Task<bool> VerifyAsync(string store, StorePurchase purchase, CancellationToken token);
}
public interface IBillingService : IDisposable
{
    EntitlementSnapshot Entitlements { get; }
    event EventHandler? EntitlementsChanged;
    Task InitializeAsync(CancellationToken token = default);
    Task<IReadOnlyList<BillingProduct>> GetProductsAsync(CancellationToken token = default);
    Task<PurchaseResult> PurchaseAsync(string key, CancellationToken token = default);
    Task<EntitlementSnapshot> RefreshEntitlementsAsync(CancellationToken token = default);
    Task<EntitlementSnapshot> RestorePurchasesAsync(CancellationToken token = default);
}
