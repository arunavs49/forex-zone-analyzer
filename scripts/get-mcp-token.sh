#!/usr/bin/env bash
# Fetches an Entra ID access token for the Forex MCP Server.
# Used by VS Code MCP client config to authenticate to the remote server.
#
# Prerequisites:
#   - Azure CLI installed and logged in (az login)
#
# Usage:
#   ./scripts/get-mcp-token.sh

set -euo pipefail

APP_CLIENT_ID="${APP_CLIENT_ID:-c1bba0b6-1125-40c1-b496-0ef773bfd7b4}"

az account get-access-token \
    --resource "api://${APP_CLIENT_ID}" \
    --query accessToken \
    --output tsv
