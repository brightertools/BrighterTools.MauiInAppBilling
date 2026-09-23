#!/bin/bash
set -euo pipefail
rid="${1:?runtime identifier required}"
out="${2:?output directory required}"
src="$(cd "$(dirname "$0")" && pwd)/Billing.swift"
mkdir -p "$out"
if [[ -f "$out/libBrighterToolsBilling.a" && "$out/libBrighterToolsBilling.a" -nt "$src" && "$out/libBrighterToolsBilling.a" -nt "$0" ]]; then exit 0; fi
case "$rid" in
 ios-arm64) sdk=iphoneos; triple=arm64-apple-ios15.0 ;;
 iossimulator-arm64) sdk=iphonesimulator; triple=arm64-apple-ios15.0-simulator ;;
 iossimulator-x64) sdk=iphonesimulator; triple=x86_64-apple-ios15.0-simulator ;;
 maccatalyst-arm64) sdk=macosx; triple=arm64-apple-ios15.0-macabi ;;
 maccatalyst-x64) sdk=macosx; triple=x86_64-apple-ios15.0-macabi ;;
 *) echo "Unsupported Apple RID: $rid" >&2; exit 1 ;;
esac
sdkpath="$(xcrun --sdk "$sdk" --show-sdk-path)"
extra=()
if [[ "$rid" == maccatalyst-* ]]; then
 extra=(-F "$sdkpath/System/iOSSupport/System/Library/Frameworks" -I "$sdkpath/System/iOSSupport/usr/include")
fi
xcrun --sdk "$sdk" swiftc -swift-version 5 -parse-as-library -emit-library -static -module-name BrighterToolsBilling -target "$triple" -sdk "$sdkpath" "${extra[@]}" "$src" -o "$out/libBrighterToolsBilling.a"
