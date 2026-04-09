#!/usr/bin/env bash
# Fetches an Entra ID access token for the Forex MCP Server.
# Used by VS Code MCP client config to authenticate to the remote server.
#
# Prerequisites:
#   - Azure CLI installed and logged in (az login)
#   - APP_CLIENT_ID env var set to the Entra ID app registration client ID
#
# Usage:
#   export APP_CLIENT_ID="<your-app-registration-client-id>"
#   ./scripts/get-mcp-token.sh

set -euo pipefail

if [ -z "${APP_CLIENT_ID:-}" ]; then
    echo "Error: APP_CLIENT_ID environment variable not set." >&2
    echo "Set it to your Entra ID app registration client ID." >&2
    exit 1
fi

az account get-access-token \
    --resource "api://${APP_CLIENT_ID}" \
    --query accessToken \
    --output tsv
