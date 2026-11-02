#if IOS || MACCATALYST
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text.Json.Serialization;
namespace BrighterTools.MauiInAppBilling;
internal sealed class AppleBillingProvider : IStoreBillingProvider
{
    private delegate void Reply(long id, nint json);
    private static readonly Reply callback = Receive;
    private static long nextId;
    private static readonly ConcurrentDictionary<long, TaskCompletionSource<string>> waiting = new();
    internal static event EventHandler? NativeChanged;
    public string StoreName => "AppStore";
    public event EventHandler? PurchasesChanged;
    public AppleBillingProvider() => NativeChanged += Changed;
    private void Changed(object? s, EventArgs e) => PurchasesChanged?.Invoke(this, e);
    public string ProductId(ProductRegistration p) => p.AppleProductId;
    [DllImport("__Internal", EntryPoint = "bt_billing_request")]
    private static extern void Request(long id, string operation, string argument, Reply reply);
    [ObjCRuntime.MonoPInvokeCallback(typeof(Reply))]
    private static void Receive(long id, nint pointer)
    {
        var text = Marshal.PtrToStringUTF8(pointer) ?? "{}";
        if (id == 0) { NativeChanged?.Invoke(null, EventArgs.Empty); return; }
        if (waiting.TryRemove(id, out var completion)) completion.TrySetResult(text);
    }
    internal static async Task<JsonElement> Call(string operation, object argument, CancellationToken token)
    {
        long id = Interlocked.Increment(ref nextId);
        var completion = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
        waiting[id] = completion;
        try
        {
            await MainThread.InvokeOnMainThreadAsync(() => Request(id, operation, SerializeArgument(argument), callback));
            var json = await completion.Task.WaitAsync(token);
            using var document = JsonDocument.Parse(json);
            var result = document.RootElement.Clone();
            if (result.TryGetProperty("error", out var error)) throw new InvalidOperationException(error.GetString());
            return result;
        }
        finally { waiting.TryRemove(id, out _); }
    }
    public async Task<IReadOnlyList<BillingProduct>> GetProductsAsync(IReadOnlyList<ProductRegistration> products, CancellationToken token)
    {
        var result = await Call("products", products.Select(p => p.AppleProductId).Where(p => !string.IsNullOrWhiteSpace(p)).ToArray(), token);
        return result.GetProperty("products").EnumerateArray().Select(p =>
        {
            string id = p.GetProperty("id").GetString()!;
            return new BillingProduct(products.Single(x => x.AppleProductId == id).Key, id, p.GetProperty("title").GetString()!, p.GetProperty("price").GetString()!);
        }).ToArray();
    }
    private async Task<EntitlementQuery> Query(string operation, CancellationToken token)
    {
        try
        {
            var result = await Call(operation, "", token);
            return new(true, result.GetProperty("purchases").EnumerateArray().Select(p => new StorePurchase(
                p.GetProperty("product").GetString()!, p.GetProperty("transaction").GetString()!,
                p.GetProperty("verified").GetBoolean(), p.GetProperty("finish").GetBoolean(), p.GetProperty("proof").GetString())).ToArray());
        }
        catch (Exception ex) when (ex is not OperationCanceledException) { return new(false, [], ex.Message); }
    }
    public Task<EntitlementQuery> RefreshAsync(CancellationToken token) => Query("refresh", token);
    public Task<EntitlementQuery> RestoreAsync(CancellationToken token) => Query("restore", token);
    public async Task<StorePurchaseResult> PurchaseAsync(string productId, CancellationToken token)
    {
        var result = await Call("purchase", productId, token);
        return new(Enum.Parse<PurchaseStatus>(result.GetProperty("status").GetString()!), Message: result.TryGetProperty("message", out var m) ? m.GetString() : null);
    }
    public async Task CompleteAsync(StorePurchase p, CancellationToken token) => _ = await Call("finish", p.TransactionId, token);
    private static string SerializeArgument(object value) => value switch {
        string text => JsonSerializer.Serialize(text, AppleBillingJson.Default.String),
        string[] items => JsonSerializer.Serialize(items, AppleBillingJson.Default.StringArray),
        SubscriptionPurchaseRequest request => JsonSerializer.Serialize(request, AppleBillingJson.Default.SubscriptionPurchaseRequest),
        _ => throw new ArgumentException("Unsupported native billing argument.")
    };
    public void Dispose() => NativeChanged -= Changed;
}
[JsonSerializable(typeof(string))]
[JsonSerializable(typeof(string[]))]
[JsonSerializable(typeof(SubscriptionPurchaseRequest))]
internal partial class AppleBillingJson : JsonSerializerContext { }
#endif
