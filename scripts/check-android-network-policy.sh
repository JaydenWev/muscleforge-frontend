#!/usr/bin/env bash
set -euo pipefail

if [[ $# -ne 2 || ! -f ${1:-} || ! ${2:-} =~ ^(Debug|Release)$ ]]; then
  echo "Usage: $0 <apk> <Debug|Release>" >&2
  exit 2
fi
apk=$1
configuration=$2
aapt2=${AAPT2:-${ANDROID_HOME:?Set ANDROID_HOME or AAPT2}/build-tools/36.0.0/aapt2}
fail() { echo "Android network policy check failed: $*" >&2; exit 1; }
manifest=$("$aapt2" dump xmltree --file AndroidManifest.xml "$apk") || fail "cannot inspect manifest"
resources=$("$aapt2" dump resources "$apk") || fail "cannot inspect resources"
policy=$("$aapt2" dump xmltree --file res/xml/network_security_config.xml "$apk") || fail "cannot inspect network security config"

# Restrict checks to attributes directly on the sole application element.
application=$(printf '%s\n' "$manifest" | awk '
  /^      E: application \(line=/ { count++; in_app=1; next }
  in_app && /^          E:/ { in_app=0 }
  in_app && /^        A:/ { print }
  END { if (count != 1) exit 1 }
') || fail "expected one application"
attribute() { printf '%s\n' "$application" | grep -E "^        A: http://schemas.android.com/apk/res/android:$1\\(0x[0-9a-f]+\\)=" || true; }
cleartext=$(attribute usesCleartextTraffic)
[[ $cleartext =~ \)=false$ ]] || fail "usesCleartextTraffic must be false"
debuggable=$(attribute debuggable)
if [[ $configuration == Debug ]]; then
  [[ $debuggable =~ \)=true$ ]] || fail "Debug APK must be debuggable"
else
  [[ -z $debuggable || $debuggable =~ \)=false$ ]] || fail "Release APK must not be debuggable"
fi
reference=$(attribute networkSecurityConfig)
[[ $reference =~ \)=@(0x[0-9a-f]+)$ ]] || fail "networkSecurityConfig must reference a resource"
id=${BASH_REMATCH[1]}
# Reject alternate resource configurations: any one could change the runtime policy.
resource=$(printf '%s\n' "$resources" | awk -v id="$id" '$1 == "resource" && $2 == id { print; getline; print; if (getline > 0 && $1 != "resource") print }')
[[ $(printf '%s\n' "$resource" | wc -l) -eq 2 && $resource == *"resource $id xml/network_security_config"*$'\n'*'() (file) res/xml/network_security_config.xml type=XML' ]] || fail "unexpected network security resource or variants"

# Strictly accept only the intended compiled tree, including its attributes and hierarchy.
normalized=$(printf '%s\n' "$policy" | sed -E 's/ \(line=[0-9]+\)$//; s/ \(Raw: "[^"]*"\)$//')
expected=$(cat <<'TREE'
E: network-security-config
    E: base-config
      A: cleartextTrafficPermitted=false
        E: trust-anchors
            E: certificates
              A: src="system"
    E: debug-overrides
        E: trust-anchors
            E: certificates
              A: src="user"
TREE
)
[[ $normalized == "$expected" ]] || fail "compiled network security config differs from the approved policy"
echo "$configuration APK network policy verified: $apk"
