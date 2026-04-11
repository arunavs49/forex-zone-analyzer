import Foundation
import MSAL

/// Handles Entra ID authentication via MSAL, providing automatic token
/// acquisition and silent refresh for the MCP server.
@MainActor
class AuthService: ObservableObject {
    @Published var isSignedIn = false
    @Published var userDisplayName: String?
    @Published var errorMessage: String?

    private var msalApplication: MSALPublicClientApplication?
    private var currentAccount: MSALAccount?

    // Entra ID app registration for the MCP server
    static let clientId = "c1bba0b6-1125-40c1-b496-0ef773bfd7b4"
    static let tenantId = "common" // multi-tenant; restrict if single-tenant
    static let redirectUri = "msauth.com.zoneradar.app://auth"
    static let scopes = ["api://c1bba0b6-1125-40c1-b496-0ef773bfd7b4/.default"]

    init() {
        setupMSAL()
        loadCurrentAccount()
    }

    // MARK: - Setup

    private func setupMSAL() {
        guard let authority = try? MSALAADAuthority(
            url: URL(string: "https://login.microsoftonline.com/\(Self.tenantId)")!
        ) else {
            errorMessage = "Failed to create MSAL authority"
            return
        }

        let config = MSALPublicClientApplicationConfig(
            clientId: Self.clientId,
            redirectUri: Self.redirectUri,
            authority: authority
        )

        do {
            msalApplication = try MSALPublicClientApplication(configuration: config)
        } catch {
            errorMessage = "MSAL init failed: \(error.localizedDescription)"
        }
    }

    private func loadCurrentAccount() {
        guard let app = msalApplication else { return }
        // Check for cached account from a previous session
        if let account = try? app.allAccounts().first {
            currentAccount = account
            isSignedIn = true
            userDisplayName = account.username
        }
    }

    // MARK: - Sign In (interactive)

    func signIn() async {
        guard let app = msalApplication else {
            errorMessage = "MSAL not initialized"
            return
        }

        guard let windowScene = UIApplication.shared.connectedScenes
            .compactMap({ $0 as? UIWindowScene }).first,
              let rootVC = windowScene.windows.first?.rootViewController else {
            errorMessage = "No root view controller"
            return
        }

        let webviewParams = MSALWebviewParameters(authPresentationViewController: rootVC)
        let interactiveParams = MSALInteractiveTokenParameters(scopes: Self.scopes, webviewParameters: webviewParams)

        do {
            let result = try await app.acquireToken(with: interactiveParams)
            currentAccount = result.account
            isSignedIn = true
            userDisplayName = result.account.username
            errorMessage = nil
        } catch {
            errorMessage = "Sign-in failed: \(error.localizedDescription)"
        }
    }

    // MARK: - Sign Out

    func signOut() {
        guard let app = msalApplication, let account = currentAccount else { return }
        try? app.remove(account)
        currentAccount = nil
        isSignedIn = false
        userDisplayName = nil
    }

    // MARK: - Get Access Token (silent with fallback)

    /// Returns a valid access token, refreshing silently if needed.
    /// Call this before every MCP request.
    func getAccessToken() async -> String? {
        guard let app = msalApplication, let account = currentAccount else { return nil }

        let silentParams = MSALSilentTokenParameters(scopes: Self.scopes, account: account)

        do {
            let result = try await app.acquireTokenSilent(with: silentParams)
            return result.accessToken
        } catch let error as NSError where error.domain == MSALErrorDomain &&
                    error.code == MSALError.interactionRequired.rawValue {
            // Token expired and refresh token is also expired — need interactive sign-in
            await MainActor.run {
                isSignedIn = false
                errorMessage = "Session expired. Please sign in again."
            }
            return nil
        } catch {
            await MainActor.run {
                errorMessage = "Token refresh failed: \(error.localizedDescription)"
            }
            return nil
        }
    }

    // MARK: - Handle redirect URL from MSAL browser

    func handleRedirectURL(_ url: URL) {
        MSALPublicClientApplication.handleMSALResponse(
            url,
            sourceApplication: nil
        )
    }
}
