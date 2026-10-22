#!/usr/bin/env bash
# Fail if API / client / packaging version pins disagree.
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
EXPECTED="${1:-1.2.34}"

fail=0
check() {
  local label="$1" file="$2" pattern="$3"
  if [[ ! -f "$file" ]]; then
    echo "MISSING: $label ($file)"
    fail=1
    return
  fi
  if ! grep -Eq "$pattern" "$file"; then
    echo "MISMATCH: $label — expected /$pattern/ in $file"
    fail=1
  else
    echo "OK: $label"
  fi
}

check "ProductVersions.Api" \
  "$ROOT/services/shared/WorkshopOS.Contracts/Common/CommonDtos.cs" \
  "Api = \"$EXPECTED\""
check "ProductVersions.ClientMinimum" \
  "$ROOT/services/shared/WorkshopOS.Contracts/Common/CommonDtos.cs" \
  "ClientMinimum = \"$EXPECTED\""
check "Client csproj Version" \
  "$ROOT/apps/windows-client/WorkshopOS.Client/WorkshopOS.Client.csproj" \
  "<Version>$EXPECTED</Version>"
check "Connect banner" \
  "$ROOT/apps/windows-client/WorkshopOS.Client/Views/ServerConnectPage.xaml" \
  "Client $EXPECTED"
check "Settings banner" \
  "$ROOT/apps/windows-client/WorkshopOS.Client/Views/SettingsPage.xaml" \
  "Client $EXPECTED"
check "build-client.ps1 default" \
  "$ROOT/packaging/build-client.ps1" \
  "Version = \"$EXPECTED\""
check "one-shot Version" \
  "$ROOT/scripts/update-github-and-build-client.ps1" \
  "Version = '$EXPECTED'"
check "one-shot tag" \
  "$ROOT/scripts/update-github-and-build-client.ps1" \
  "ReleaseTag = 'v$EXPECTED'"
check "sync zip name" \
  "$ROOT/scripts/update-github-and-build-client.ps1" \
  "WorkshopOS-$EXPECTED-github-sync.zip"
check "get-workshopos PRODUCT_VERSION" \
  "$ROOT/scripts/get-workshopos.sh" \
  "PRODUCT_VERSION=\"$EXPECTED\\+\""
check "README current" \
  "$ROOT/README.md" \
  "Current version: $EXPECTED"

if [[ "$fail" -ne 0 ]]; then
  echo "Version pin check FAILED for $EXPECTED"
  exit 1
fi
echo "All version pins match $EXPECTED"
