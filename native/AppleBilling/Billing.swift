import Foundation
import StoreKit

typealias BillingReply = @convention(c) (Int64, UnsafePointer<CChar>?) -> Void

@MainActor
private enum BillingBridge {
    static var updates: Task<Void, Never>?
    static var transactions: [String: Transaction] = [:]
    static func emit(_ id: Int64, _ object: [String: Any], _ reply: BillingReply) {
        let data = (try? JSONSerialization.data(withJSONObject: object)) ?? Data("{}".utf8)
        String(decoding: data, as: UTF8.self).withCString { reply(id, $0) }
    }
    static func listen(_ reply: @escaping BillingReply) {
        guard updates == nil else { return }
        updates = Task {
            for await result in Transaction.updates {
                if case .verified(let transaction) = result {
                    transactions[String(transaction.id)] = transaction
                }
                emit(0, ["changed": true], reply)
            }
        }
    }
    static func entitlements(subscriptions: Bool = false) async throws -> [[String: Any]] {
        var unfinished = Set<String>()
        for await result in Transaction.unfinished {
            if case .verified(let transaction) = result {
                let id = String(transaction.id)
                transactions[id] = transaction
                unfinished.insert(id)
            }
        }
        var values: [[String: Any]] = []
        for await result in Transaction.currentEntitlements {
            switch result {
            case .verified(let transaction):
                guard transaction.productType == (subscriptions ? .autoRenewable : .nonConsumable) else { continue }
                let id = String(transaction.id)
                transactions[id] = transaction
                values.append(["product": transaction.productID, "transaction": id, "verified": true,
                               "finish": unfinished.contains(id), "proof": result.jwsRepresentation,
                               "originalTransaction": String(transaction.originalID), "accountToken": transaction.appAccountToken?.uuidString ?? ""])
            case .unverified(let transaction, _):
                values.append(["product": transaction.productID, "transaction": String(transaction.id),
                               "verified": false, "finish": false, "proof": result.jwsRepresentation])
            }
        }
        return values
    }
    static func run(_ id: Int64, _ operation: String, _ argument: String, _ reply: @escaping BillingReply) async {
        listen(reply)
        do {
            let value = try JSONSerialization.jsonObject(with: Data(argument.utf8), options: [.fragmentsAllowed])
            switch operation {
            case "subscription-products":
                let products = try await Product.products(for: value as? [String] ?? [])
                emit(id, ["products": products.filter { $0.type == .autoRenewable }.map {
                    ["id": $0.id, "title": $0.displayName, "price": $0.displayPrice,
                     "period": $0.subscription.map { "\($0.subscriptionPeriod.value) \($0.subscriptionPeriod.unit)" } ?? ""]
                }], reply)
            case "subscription-refresh", "subscription-restore":
                if operation == "subscription-restore" { try await AppStore.sync() }
                emit(id, ["purchases": try await entitlements(subscriptions: true)], reply)
            case "subscription-purchase":
                guard let request = value as? [String: Any], let productID = request["ProductId"] as? String,
                      let account = request["AccountToken"] as? String, let accountToken = UUID(uuidString: account),
                      let product = try await Product.products(for: [productID]).first,
                      product.type == .autoRenewable else {
                    emit(id, ["status": "Unavailable"], reply); return
                }
                switch try await product.purchase(options: [.appAccountToken(accountToken)]) {
                case .success(let result):
                    switch result {
                    case .verified(let transaction):
                        transactions[String(transaction.id)] = transaction
                        emit(id, ["status": "Purchased"], reply)
                    case .unverified:
                        emit(id, ["status": "Unverified", "message": "The App Store transaction could not be verified."], reply)
                    }
                case .pending: emit(id, ["status": "Pending"], reply)
                case .userCancelled: emit(id, ["status": "Cancelled"], reply)
                @unknown default: emit(id, ["status": "Failed"], reply)
                }
            case "products":
                let products = try await Product.products(for: value as? [String] ?? [])
                emit(id, ["products": products.filter { $0.type == .nonConsumable }.map {
                    ["id": $0.id, "title": $0.displayName, "price": $0.displayPrice]
                }], reply)
            case "restore", "refresh":
                if operation == "restore" { try await AppStore.sync() }
                emit(id, ["purchases": try await entitlements()], reply)
            case "purchase":
                guard let productID = value as? String,
                      let product = try await Product.products(for: [productID]).first,
                      product.type == .nonConsumable else {
                    emit(id, ["status": "Unavailable"], reply); return
                }
                switch try await product.purchase() {
                case .success(let result):
                    switch result {
                    case .verified(let transaction):
                        transactions[String(transaction.id)] = transaction
                        emit(id, ["status": "Purchased"], reply)
                    case .unverified:
                        emit(id, ["status": "Unverified", "message": "The App Store transaction could not be verified."], reply)
                    }
                case .pending: emit(id, ["status": "Pending"], reply)
                case .userCancelled: emit(id, ["status": "Cancelled"], reply)
                @unknown default: emit(id, ["status": "Failed"], reply)
                }
            case "finish":
                if let transactionID = value as? String, let transaction = transactions[transactionID] {
                    await transaction.finish()
                    transactions.removeValue(forKey: transactionID)
                    emit(id, ["finished": true], reply)
                } else { emit(id, ["error": "Transaction is not available for finalization. Refresh and retry."], reply) }
            default: emit(id, ["error": "Unknown billing operation."], reply)
            }
        } catch { emit(id, ["error": error.localizedDescription], reply) }
    }
}

@_cdecl("bt_billing_request")
public func billingRequest(_ id: Int64, _ operation: UnsafePointer<CChar>, _ argument: UnsafePointer<CChar>, _ reply: @escaping @convention(c) (Int64, UnsafePointer<CChar>?) -> Void) {
    let op = String(cString: operation)
    let arg = String(cString: argument)
    Task { @MainActor in await BillingBridge.run(id, op, arg, reply) }
}
