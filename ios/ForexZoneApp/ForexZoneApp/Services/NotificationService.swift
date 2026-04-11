import Foundation
import UserNotifications
import UIKit

/// Manages push notification registration and Azure Notification Hub device registration.
class NotificationService: NSObject, ObservableObject, UNUserNotificationCenterDelegate {
    static let shared = NotificationService()

    @Published var isAuthorized = false
    @Published var deviceToken: String?

    private var hubConnectionString: String?
    private var hubName: String?

    private override init() {
        super.init()
        UNUserNotificationCenter.current().delegate = self
    }

    /// Configure the Notification Hub connection details.
    func configure(connectionString: String, hubName: String) {
        self.hubConnectionString = connectionString
        self.hubName = hubName

        // Re-register if we already have a token
        if let token = deviceToken {
            registerWithHub(deviceToken: token)
        }
    }

    /// Request notification permission and register for remote notifications.
    func requestAuthorization() {
        UNUserNotificationCenter.current().requestAuthorization(options: [.alert, .sound, .badge]) { granted, error in
            DispatchQueue.main.async {
                self.isAuthorized = granted
            }
            if let error = error {
                print("[NotificationService] Authorization error: \(error.localizedDescription)")
                return
            }
            if granted {
                DispatchQueue.main.async {
                    UIApplication.shared.registerForRemoteNotifications()
                }
            }
        }
    }

    /// Called by AppDelegate when APNs provides a device token.
    func didRegisterForRemoteNotifications(deviceToken data: Data) {
        let token = data.map { String(format: "%02.2hhx", $0) }.joined()
        DispatchQueue.main.async {
            self.deviceToken = token
        }
        print("[NotificationService] APNs device token: \(token)")
        registerWithHub(deviceToken: token)
    }

    /// Called by AppDelegate when APNs registration fails.
    func didFailToRegisterForRemoteNotifications(error: Error) {
        print("[NotificationService] APNs registration failed: \(error.localizedDescription)")
    }

    // MARK: - Azure Notification Hub Registration

    /// Register the device token with Azure Notification Hub using the REST API.
    private func registerWithHub(deviceToken: String) {
        guard let connectionString = hubConnectionString, !connectionString.isEmpty,
              let hubName = hubName, !hubName.isEmpty else {
            print("[NotificationService] Hub not configured, skipping registration")
            return
        }

        guard let (endpoint, sasToken) = parseSASToken(connectionString: connectionString, hubName: hubName) else {
            print("[NotificationService] Failed to parse connection string")
            return
        }

        let urlString = "\(endpoint)\(hubName)/registrations/?api-version=2015-01"
        guard let url = URL(string: urlString) else { return }

        // APNs native registration XML with "all" tag
        let body = """
        <?xml version="1.0" encoding="utf-8"?>
        <entry xmlns="http://www.w3.org/2005/Atom">
            <content type="application/xml">
                <AppleRegistrationDescription xmlns:i="http://www.w3.org/2001/XMLSchema-instance" xmlns="http://schemas.microsoft.com/netservices/2010/10/servicebus/connect">
                    <Tags>all</Tags>
                    <DeviceToken>\(deviceToken)</DeviceToken>
                </AppleRegistrationDescription>
            </content>
        </entry>
        """

        var request = URLRequest(url: url)
        request.httpMethod = "POST"
        request.setValue("application/atom+xml;type=entry;charset=utf-8", forHTTPHeaderField: "Content-Type")
        request.setValue(sasToken, forHTTPHeaderField: "Authorization")
        request.httpBody = body.data(using: .utf8)

        URLSession.shared.dataTask(with: request) { data, response, error in
            if let error = error {
                print("[NotificationService] Hub registration failed: \(error.localizedDescription)")
                return
            }
            if let httpResponse = response as? HTTPURLResponse {
                if (200...299).contains(httpResponse.statusCode) {
                    print("[NotificationService] Successfully registered with Notification Hub")
                } else {
                    let body = data.flatMap { String(data: $0, encoding: .utf8) } ?? "no body"
                    print("[NotificationService] Hub registration failed (\(httpResponse.statusCode)): \(body)")
                }
            }
        }.resume()
    }

    /// Parse a Notification Hub connection string and generate a SAS token.
    private func parseSASToken(connectionString: String, hubName: String) -> (endpoint: String, token: String)? {
        var endpoint = ""
        var sasKeyName = ""
        var sasKey = ""

        for part in connectionString.components(separatedBy: ";") {
            if part.hasPrefix("Endpoint=") {
                endpoint = part.replacingOccurrences(of: "Endpoint=", with: "")
                    .replacingOccurrences(of: "sb://", with: "https://")
            } else if part.hasPrefix("SharedAccessKeyName=") {
                sasKeyName = part.replacingOccurrences(of: "SharedAccessKeyName=", with: "")
            } else if part.hasPrefix("SharedAccessKey=") {
                sasKey = part.replacingOccurrences(of: "SharedAccessKey=", with: "")
            }
        }

        guard !endpoint.isEmpty, !sasKeyName.isEmpty, !sasKey.isEmpty else { return nil }

        let targetUri = endpoint.lowercased()
        let expiry = Int(Date().timeIntervalSince1970) + 60 * 60 * 24 // 24 hours

        guard let encodedUri = targetUri.addingPercentEncoding(withAllowedCharacters: .urlHostAllowed),
              let keyData = Data(base64Encoded: sasKey) else { return nil }

        let stringToSign = "\(encodedUri)\n\(expiry)"
        guard let stringData = stringToSign.data(using: .utf8) else { return nil }

        // HMAC-SHA256
        var hmac = [UInt8](repeating: 0, count: Int(CC_SHA256_DIGEST_LENGTH))
        stringData.withUnsafeBytes { stringBytes in
            keyData.withUnsafeBytes { keyBytes in
                CCHmac(CCHmacAlgorithm(kCCHmacAlgSHA256),
                        keyBytes.baseAddress, keyData.count,
                        stringBytes.baseAddress, stringData.count,
                        &hmac)
            }
        }

        let signature = Data(hmac).base64EncodedString()
        guard let encodedSignature = signature.addingPercentEncoding(withAllowedCharacters: .urlQueryAllowed) else { return nil }

        let token = "SharedAccessSignature sr=\(encodedUri)&sig=\(encodedSignature)&se=\(expiry)&skn=\(sasKeyName)"
        return (endpoint, token)
    }

    // MARK: - UNUserNotificationCenterDelegate

    /// Show notification banner even when app is in foreground.
    func userNotificationCenter(_ center: UNUserNotificationCenter,
                                willPresent notification: UNNotification,
                                withCompletionHandler completionHandler: @escaping (UNNotificationPresentationOptions) -> Void) {
        completionHandler([.banner, .sound, .badge])
    }

    /// Handle notification tap — parse zone data for potential deep linking.
    func userNotificationCenter(_ center: UNUserNotificationCenter,
                                didReceive response: UNNotificationResponse,
                                withCompletionHandler completionHandler: @escaping () -> Void) {
        let userInfo = response.notification.request.content.userInfo
        if let zoneData = userInfo["zone"] as? [String: Any],
           let instrument = zoneData["instrument"] as? String {
            print("[NotificationService] User tapped zone alert for \(instrument)")
            NotificationCenter.default.post(
                name: .zoneAlertTapped,
                object: nil,
                userInfo: ["instrument": instrument]
            )
        }
        completionHandler()
    }
}

// MARK: - CommonCrypto bridging
import CommonCrypto

extension Notification.Name {
    static let zoneAlertTapped = Notification.Name("zoneAlertTapped")
}
