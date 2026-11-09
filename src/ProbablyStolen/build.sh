#!/usr/bin/env bash
# Builds the Probably Stolen side mod (StardewPortal.dll) with the Roslyn compiler
# that ships with the .NET SDK, so there are no NuGet dependencies.
#
#   PS="/path/to/Probably Stolen" bash build.sh
#
set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
GAME="${PS:-${PS_ROOT:-}}"
if [ -z "$GAME" ]; then
  echo "set PS to the Probably Stolen install folder, e.g. PS=\"/c/Program Files (x86)/Steam/steamapps/common/Probably Stolen\"" >&2
  exit 1
fi

MANAGED="$GAME/Probably Stolen_Data/Managed"
OUT="$HERE/out"
mkdir -p "$OUT"

# find csc.dll inside the installed SDK
CSC=""
for candidate in "$(dirname "$(command -v dotnet)")"/sdk/*/Roslyn/bincore/csc.dll \
                 /c/Program\ Files/dotnet/sdk/*/Roslyn/bincore/csc.dll; do
  [ -f "$candidate" ] && CSC="$candidate" && break
done
if [ -z "$CSC" ]; then
  echo "could not find csc.dll; is the .NET SDK installed?" >&2
  exit 1
fi

dotnet "$CSC" \
  -nologo -noconfig -nostdlib+ -target:library -codepage:65001 -langversion:9.0 \
  -out:"$OUT/StardewPortal.dll" \
  -r:"$MANAGED/mscorlib.dll" \
  -r:"$MANAGED/System.dll" \
  -r:"$MANAGED/System.Core.dll" \
  -r:"$MANAGED/netstandard.dll" \
  -r:"$MANAGED/UnityEngine.dll" \
  -r:"$MANAGED/UnityEngine.CoreModule.dll" \
  -r:"$MANAGED/UnityEngine.ImageConversionModule.dll" \
  -r:"$MANAGED/Assembly-CSharp.dll" \
  -r:"$MANAGED/Newtonsoft.Json.dll" \
  "$HERE/StardewPortal.cs" \
  "$HERE/PortalInbox.cs"

echo "built: $OUT/StardewPortal.dll"
