using System.Text.Json;
using System.Collections.Frozen;
using System.Text.Json.Serialization;
namespace BrighterTools.MauiInAppBilling;

// Store adapters verify evidence; this coordinator serializes delivery and durable finalization.
public sealed class BillingService : IBillingService
{
    private readonly IStoreBillingProvider provider;
    private readonly IBillingStateStore state;
    private readonly BillingOptions options;
    private readonly IPurchaseVerifier? verifier;
    private readonly SemaphoreSlim gate = new(1);
    private readonly CancellationTokenSource lifetime = new();
    private bool initialized;
    private bool disposed;
    private int retryScheduled;
    private BillingJournal journal = new(1, [], []);
    private string StateKey => "billing-v1/" + options.ApplicationId + "/" + provider.StoreName;
    public EntitlementSnapshot Entitlements { get; private set; } = new(new HashSet<string>(), false);
    public event EventHandler? EntitlementsChanged;
    public BillingService(IStoreBillingProvider provider, IBillingStateStore state, BillingOptions options, IPurchaseVerifier? verifier = null)
    {
        this.provider = provider; this.state = state; this.options = options; this.verifier = verifier;
        if (options.Products.Any(p => string.IsNullOrWhiteSpace(p.Key)) || options.Products.Select(p => p.Key).Distinct().Count() != options.Products.Count)
            throw new ArgumentException("Product keys must be unique and non-empty.");
        provider.PurchasesChanged += OnPurchasesChanged;
    }
    private async void OnPurchasesChanged(object? sender, EventArgs e)
    {
        try { await RefreshEntitlementsAsync(lifetime.Token); }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Publish(false, ex.Message); }
    }
    public async Task InitializeAsync(CancellationToken token = default) => _ = await RefreshEntitlementsAsync(token);
    public Task<IReadOnlyList<BillingProduct>> GetProductsAsync(CancellationToken token = default) =>
        provider.GetProductsAsync(options.Products, token);
    private async Task LoadAsync()
    {
        if (initialized) return;
        var text = await state.ReadAsync(StateKey);
        if (text != null)
        {
            try
            {
                var loaded = JsonSerializer.Deserialize(text, BillingJson.Default.BillingJournal);
                if (loaded is { Version: 1, Owned: not null, Pending: not null }) journal = loaded;
            }
            catch (JsonException) { }
        }
        initialized = true;
    }
    private void Publish(bool current, string? message = null)
    {
        if (disposed) return;
        Entitlements = new(journal.Owned.Where(k => options.Products.Any(p => p.Key == k)).ToFrozenSet(StringComparer.Ordinal), current, message);
        EntitlementsChanged?.Invoke(this, EventArgs.Empty);
    }
    public Task<EntitlementSnapshot> RefreshEntitlementsAsync(CancellationToken token = default) => Refresh(false, token);
    public Task<EntitlementSnapshot> RestorePurchasesAsync(CancellationToken token = default) => Refresh(true, token);
    private async Task<EntitlementSnapshot> Refresh(bool restore, CancellationToken token)
    {
        await gate.WaitAsync(token);
        try
        {
            await LoadAsync();
            var query = restore ? await provider.RestoreAsync(token) : await provider.RefreshAsync(token);
            await ApplyAsync(query, token);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { Publish(false, ex.Message); }
        finally { gate.Release(); }
        return Entitlements;
    }
    private async Task ApplyAsync(EntitlementQuery query, CancellationToken token)
    {
        if (!query.Confirmed) { Publish(false, query.Message); if (journal.Pending.Count != 0) ScheduleRetry(); return; }
        var owned = new List<string>();
        var pending = new List<StorePurchase>();
        bool unverified = false;
        foreach (var purchase in query.Purchases)
        {
            var registration = options.Products.FirstOrDefault(p => provider.ProductId(p) == purchase.ProductId);
            if (registration == null) continue;
            if (!purchase.Verified || (verifier != null && !await verifier.VerifyAsync(provider.StoreName, purchase, token)))
            { unverified = true; continue; }
            owned.Add(registration.Key);
            if (purchase.NeedsCompletion) pending.Add(purchase);
        }
        // An unverifiable result cannot erase previously verified access.
        if (unverified) { Publish(false, "A store transaction could not be verified."); return; }
        var next = new BillingJournal(1, owned.Distinct().ToList(), pending.DistinctBy(p => p.TransactionId).ToList());
        await state.WriteAsync(StateKey, JsonSerializer.Serialize(next, BillingJson.Default.BillingJournal));
        journal = next; // Delivery persisted before acknowledging/finishing.
        string? message = query.Message;
        foreach (var purchase in pending)
        {
            try
            {
                await provider.CompleteAsync(purchase, token);
                journal = journal with { Pending = journal.Pending.Where(p => p.TransactionId != purchase.TransactionId).ToList() };
                await state.WriteAsync(StateKey, JsonSerializer.Serialize(journal, BillingJson.Default.BillingJournal));
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            { message = "Purchase delivered. Store finalization will retry: " + ex.Message; }
        }
        Publish(true, message);
        if (journal.Pending.Count != 0) ScheduleRetry();
    }
    private void ScheduleRetry()
    {
        if (Interlocked.Exchange(ref retryScheduled, 1) != 0 || disposed) return;
        _ = RetryAsync();
    }
    private async Task RetryAsync()
    {
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1), lifetime.Token);
            Interlocked.Exchange(ref retryScheduled, 0);
            await RefreshEntitlementsAsync(lifetime.Token);
            if (journal.Pending.Count != 0) ScheduleRetry();
        }
        catch (OperationCanceledException) { }
        catch { if (!disposed) { Interlocked.Exchange(ref retryScheduled, 0); ScheduleRetry(); } }
    }
    public async Task<PurchaseResult> PurchaseAsync(string key, CancellationToken token = default)
    {
        await gate.WaitAsync(token);
        try
        {
            await LoadAsync();
            var product = options.Products.SingleOrDefault(p => p.Key == key);
            if (product == null || string.IsNullOrWhiteSpace(provider.ProductId(product))) return new(PurchaseStatus.Unavailable, "This product is not configured.");
            var result = await provider.PurchaseAsync(provider.ProductId(product), token);
            if (result.Status is PurchaseStatus.Purchased or PurchaseStatus.AlreadyOwned)
            {
                await ApplyAsync(await provider.RefreshAsync(token), token);
                if (!Entitlements.Owns(key)) return new(PurchaseStatus.Pending, "The store has not yet confirmed ownership. Refresh or restore purchases.");
            }
            return new(result.Status, result.Message);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return new(PurchaseStatus.Failed, ex.Message); }
        finally { gate.Release(); }
    }
    public void Dispose() { if (disposed) return; disposed = true; lifetime.Cancel(); provider.PurchasesChanged -= OnPurchasesChanged; provider.Dispose(); }
}
internal sealed record BillingJournal(int Version, List<string> Owned, List<StorePurchase> Pending);
[JsonSerializable(typeof(BillingJournal))]
internal partial class BillingJson : JsonSerializerContext { }
