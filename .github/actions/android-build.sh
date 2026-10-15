#!/usr/bin/env bash
# Publishes the signed Android APK and collects it as a release asset.
#
# Environment (set by the workflow):
#   SEMVER                       release semver, e.g. 2026.910.0-g0v0
#   ANDROID_VERSION_CODE         numeric version code for the APK
#   ANDROID_KEYSTORE_PASSWORD    keystore password
#   ANDROID_KEY_ALIAS            key alias
#   ANDROID_KEY_PASSWORD         key password
#   SENTRY_DSN                   Sentry DSN injected into the build
#   ANDROID_SDK_ROOT             Android SDK location
set -euo pipefail

keystore_pass="${ANDROID_KEYSTORE_PASSWORD:-temp123}"
key_alias="${ANDROID_KEY_ALIAS:-osu}"
# PKCS12 keystores require key password == store password
key_pass="$keystore_pass"

echo "Using Java version: $(java -version 2>&1 | head -n 1)"
echo "Android SDK Root: $ANDROID_SDK_ROOT"
echo "Keystore file exists: $(test -f osu.keystore && echo 'Yes' || echo 'No')"
echo "Using key alias: $key_alias"

if [[ ! -f "osu.keystore" ]]; then
  echo "ERROR: Keystore file not found!"
  exit 1
fi

dotnet publish osu.Android/osu.Android.csproj \
  --configuration Release \
  --framework net10.0-android \
  --verbosity normal \
  --no-restore \
  --self-contained \
  -p:Version="$SEMVER" \
  -p:ApplicationVersion="$ANDROID_VERSION_CODE" \
  -p:ApplicationDisplayVersion="$SEMVER" \
  -p:SentryDsn="$SENTRY_DSN" \
  -p:AndroidSdkDirectory="$ANDROID_SDK_ROOT" \
  -p:AndroidKeyStore=true \
  -p:AndroidSigningKeyStore="$(pwd)/osu.keystore" \
  -p:AndroidSigningKeyAlias="$key_alias" \
  -p:AndroidSigningKeyPass="$key_pass" \
  -p:AndroidSigningStorePass="$keystore_pass" \
  -p:AndroidPackageFormat=apk

mkdir -p Releases

apk_file=$(find osu.Android/bin/Release/ -name "*-Signed.apk" | head -1)
if [[ -z "$apk_file" ]]; then
  echo "ERROR: No Signed APK file found after build!"
  exit 1
fi

echo "Found APK: $apk_file"
cp "$apk_file" Releases/g0v0.apk

if command -v aapt2 &> /dev/null; then
  echo "Verifying APK signature..."
  aapt2 dump badging Releases/g0v0.apk | grep -E "application-label|package" || true
fi

# The APK installs fine even when the wrong flavour of a shared library ends up inside it, and the
# mistake only shows up as an immediate crash on a real device, so verify the packaged libraries.
#
# ppy.osu.Framework.NativeLibs ships glibc builds of libbass / libbassmix / libbass_fx under
# runtimes/linux-*/native/ for the desktop platforms and an empty runtimes/android/native/_._
# placeholder so that NuGet resolves no native assets at all for android-* RIDs. When that
# placeholder is not honoured it falls back to the matching linux-<arch> RID instead, and the glibc
# libraries are packaged into lib/<abi>/, where they shadow the bionic ones extracted from
# osu.Framework.Android's aar (the build only warns "APK already contains the item
# lib/arm64-v8a/libbass.so; ignoring"). The app then dies on startup with
# "System.DllNotFoundException: bass" on arm64 devices.
# Fail closed if the ELF inspection tool is unavailable or a library cannot be inspected.
command -v readelf > /dev/null || { echo "ERROR: readelf is required to verify Android native libraries."; exit 1; }

tmp_lib=$(mktemp)
trap 'rm -f "$tmp_lib"' EXIT
for abi in arm64-v8a armeabi-v7a x86; do
  for lib in libbass.so libbassmix.so libbass_fx.so; do
    if ! unzip -p Releases/g0v0.apk "lib/$abi/$lib" > "$tmp_lib" || [[ ! -s "$tmp_lib" ]]; then
      rm -f "$tmp_lib"
      echo "ERROR: lib/$abi/$lib is missing from the packaged APK."
      exit 1
    fi

    if ! dependencies=$(readelf -d "$tmp_lib"); then
      echo "ERROR: lib/$abi/$lib is not a readable ELF library."
      exit 1
    fi

    if grep -qE 'libc\.so\.6|libdl\.so\.2|libpthread\.so\.0' <<< "$dependencies"; then
      echo "ERROR: lib/$abi/$lib in the packaged APK is a desktop (glibc) build and can never be loaded on Android."
      exit 1
    fi

    if ! grep -qE '\(NEEDED\).*\[libc\.so\]' <<< "$dependencies"; then
      echo "ERROR: lib/$abi/$lib does not declare the expected Android libc dependency."
      exit 1
    fi
  done
done
rm -f "$tmp_lib"

echo "Verified that the packaged audio libraries are Android builds for every supported ABI."
