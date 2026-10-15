#!/usr/bin/env bash
# 从已有发布产物打包 Vex macOS .app 与 dmg。
# 发布由工作流（dotnet publish，仓库自带 PublishProfile）完成，本脚本只做 bundle / 签名 / dmg。
# 用法：./package_macos.sh osx-x64|osx-arm64|all
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
APP_NAME="${APP_NAME:-Vex}"
EXECUTABLE_NAME="${EXECUTABLE_NAME:-Vex}"
BUNDLE_ID="${BUNDLE_ID:-com.codewf.vex}"
ICON_SOURCE="${ICON_SOURCE:-$ROOT_DIR/logo.png}"
PUBLISH_ROOT="${PUBLISH_ROOT:-$ROOT_DIR/artifacts/publish}"
ARTIFACTS_ROOT="${ARTIFACTS_ROOT:-$ROOT_DIR/artifacts/macos}"
WORK_ROOT="$ARTIFACTS_ROOT/work"
CODESIGN_IDENTITY="${CODESIGN_IDENTITY:-}"
ENTITLEMENTS="${ENTITLEMENTS:-}"
NOTARIZE="${NOTARIZE:-0}"
NOTARY_KEYCHAIN_PROFILE="${NOTARY_KEYCHAIN_PROFILE:-}"

usage() {
  cat <<USAGE
Usage:
  ./package_macos.sh osx-x64|osx-arm64|all

Environment:
  APP_NAME                 App bundle name. Default: Vex
  EXECUTABLE_NAME          Executable inside publish dir. Default: Vex
  BUNDLE_ID                CFBundleIdentifier. Default: com.codewf.vex
  CODESIGN_IDENTITY        Developer ID identity. Empty means ad-hoc signing.
  ENTITLEMENTS             Optional entitlements plist used with Developer ID signing.
  NOTARIZE                 Set to 1 to submit DMGs with xcrun notarytool.
  NOTARY_KEYCHAIN_PROFILE  notarytool keychain profile name.
USAGE
}

die() {
  echo "Error: $*" >&2
  exit 1
}

resolve_version() {
  local version
  version="$(sed -n 's:.*<Version>\(.*\)</Version>.*:\1:p' "$ROOT_DIR/Directory.Build.props" | head -n 1)"
  [[ -n "$version" ]] || die "Unable to resolve project version from Directory.Build.props."
  echo "$version"
}

to_macos_version() {
  local version="$1"
  local core major minor patch
  core="${version%%[-+]*}"
  IFS='.' read -r major minor patch _ <<<"$core"
  major="${major:-0}"; minor="${minor:-0}"; patch="${patch:-0}"
  [[ "$major" =~ ^[0-9]+$ ]] || major="0"
  [[ "$minor" =~ ^[0-9]+$ ]] || minor="0"
  [[ "$patch" =~ ^[0-9]+$ ]] || patch="0"
  echo "$major.$minor.$patch"
}

create_icon() {
  local icon_path="$1"
  local iconset_dir="$2"

  rm -rf "$iconset_dir"
  mkdir -p "$iconset_dir"

  sips -z 16 16 "$ICON_SOURCE" --out "$iconset_dir/icon_16x16.png" >/dev/null
  sips -z 32 32 "$ICON_SOURCE" --out "$iconset_dir/icon_16x16@2x.png" >/dev/null
  sips -z 32 32 "$ICON_SOURCE" --out "$iconset_dir/icon_32x32.png" >/dev/null
  sips -z 64 64 "$ICON_SOURCE" --out "$iconset_dir/icon_32x32@2x.png" >/dev/null
  sips -z 128 128 "$ICON_SOURCE" --out "$iconset_dir/icon_128x128.png" >/dev/null
  sips -z 256 256 "$ICON_SOURCE" --out "$iconset_dir/icon_128x128@2x.png" >/dev/null
  sips -z 256 256 "$ICON_SOURCE" --out "$iconset_dir/icon_256x256.png" >/dev/null
  sips -z 512 512 "$ICON_SOURCE" --out "$iconset_dir/icon_256x256@2x.png" >/dev/null
  sips -z 512 512 "$ICON_SOURCE" --out "$iconset_dir/icon_512x512.png" >/dev/null
  sips -z 1024 1024 "$ICON_SOURCE" --out "$iconset_dir/icon_512x512@2x.png" >/dev/null

  iconutil -c icns "$iconset_dir" -o "$icon_path"
}

write_info_plist() {
  local plist_path="$1"
  local macos_version="$2"

  cat >"$plist_path" <<PLIST
<?xml version="1.0" encoding="UTF-8"?>
<!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "https://www.apple.com/DTDs/PropertyList-1.0.dtd">
<plist version="1.0">
<dict>
  <key>CFBundleDevelopmentRegion</key>
  <string>en</string>
  <key>CFBundleDisplayName</key>
  <string>$APP_NAME</string>
  <key>CFBundleExecutable</key>
  <string>$EXECUTABLE_NAME</string>
  <key>CFBundleIconFile</key>
  <string>$APP_NAME</string>
  <key>CFBundleIdentifier</key>
  <string>$BUNDLE_ID</string>
  <key>CFBundleInfoDictionaryVersion</key>
  <string>6.0</string>
  <key>CFBundleName</key>
  <string>$APP_NAME</string>
  <key>CFBundlePackageType</key>
  <string>APPL</string>
  <key>CFBundleShortVersionString</key>
  <string>$macos_version</string>
  <key>CFBundleVersion</key>
  <string>$macos_version</string>
  <key>LSApplicationCategoryType</key>
  <string>public.app-category.productivity</string>
  <key>NSHighResolutionCapable</key>
  <true/>
  <key>LSMinimumSystemVersion</key>
  <string>11.0</string>
  <key>CFBundleDocumentTypes</key>
  <array>
    <dict>
      <key>CFBundleTypeName</key><string>Markdown</string>
      <key>CFBundleTypeExtensions</key><array><string>md</string><string>markdown</string><string>mdown</string></array>
      <key>CFBundleTypeRole</key><string>Editor</string>
      <key>LSHandlerRank</key><string>Alternate</string>
      <key>LSItemContentTypes</key><array><string>net.daringfireball.markdown</string></array>
      <key>CFBundleTypeIconFile</key><string>$APP_NAME</string>
    </dict>
  </array>
  <key>UTImportedTypeDeclarations</key>
  <array>
    <dict>
      <key>UTTypeIdentifier</key><string>net.daringfireball.markdown</string>
      <key>UTTypeConformsTo</key><array><string>public.plain-text</string></array>
      <key>UTTypeDescription</key><string>Markdown</string>
      <key>UTTypeTagSpecification</key>
      <dict>
        <key>public.filename-extension</key><array><string>md</string><string>markdown</string><string>mdown</string></array>
        <key>public.mime-type</key><string>text/markdown</string>
      </dict>
    </dict>
  </array>
</dict>
</plist>
PLIST
}

create_app_bundle() {
  local rid="$1"
  local macos_version="$2"
  local publish_dir="$PUBLISH_ROOT/$rid/Vex"
  local app_dir="$WORK_ROOT/$rid/$APP_NAME.app"
  local contents_dir="$app_dir/Contents"
  local macos_dir="$contents_dir/MacOS"
  local resources_dir="$contents_dir/Resources"
  local iconset_dir="$WORK_ROOT/$rid/$APP_NAME.iconset"

  [[ -x "$publish_dir/$EXECUTABLE_NAME" ]] || die "Published executable was not found: $publish_dir/$EXECUTABLE_NAME (run dotnet publish first)."

  rm -rf "$app_dir"
  mkdir -p "$macos_dir" "$resources_dir"
  # 标准包结构，语言和文档进入 Resources，调试符号留在原发布目录。
  printf 'APPL????' >"$contents_dir/PkgInfo"
  local entry name destination
  for entry in "$publish_dir"/*; do
    [[ -e "$entry" || -L "$entry" ]] || continue
    name="${entry##*/}"
    case "$name" in
      *.dSYM|*.pdb|*.dbg) continue ;;
      "$EXECUTABLE_NAME"|*.dylib) destination="$macos_dir/$name" ;;
      *) destination="$resources_dir/$name" ;;
    esac
    ditto "$entry" "$destination" || die "Could not copy bundle resource: $name"
  done
  chmod +x "$macos_dir/$EXECUTABLE_NAME" || die "Could not set executable permissions."
  create_icon "$resources_dir/$APP_NAME.icns" "$iconset_dir" || die "Could not create application icon."
  write_info_plist "$contents_dir/Info.plist" "$macos_version" || die "Could not write application metadata."

  echo "$app_dir"
}

sign_app_bundle() {
  local app_dir="$1"

  if [[ -n "$CODESIGN_IDENTITY" ]]; then
    local sign_args=(--force --deep --options runtime --timestamp --sign "$CODESIGN_IDENTITY")
    if [[ -n "$ENTITLEMENTS" ]]; then
      [[ -f "$ENTITLEMENTS" ]] || die "Entitlements file does not exist: $ENTITLEMENTS"
      sign_args+=(--entitlements "$ENTITLEMENTS")
    fi

    echo "Signing app with Developer ID..."
    codesign "${sign_args[@]}" "$app_dir"
  else
    echo "Ad-hoc signing app..."
    codesign --force --deep --sign - "$app_dir"
  fi
}

create_dmg() {
  local rid="$1"
  local version="$2"
  local app_dir="$3"

  local dmg_stage="$WORK_ROOT/dmg-$rid"
  local dmg_path="$ARTIFACTS_ROOT/$APP_NAME-$version-$rid.dmg"

  rm -rf "$dmg_stage" "$dmg_path"
  mkdir -p "$dmg_stage"
  mv "$app_dir" "$dmg_stage/$APP_NAME.app" || die "Could not stage the application bundle."
  ln -s /Applications "$dmg_stage/Applications"

  echo "Creating DMG $dmg_path ..." >&2
  hdiutil create \
    -volname "$APP_NAME" \
    -srcfolder "$dmg_stage" \
    -ov \
    -format UDZO \
    "$dmg_path" >/dev/null || die "DMG creation failed."

  local sha
  sha="$(shasum -a 256 "$dmg_path" | awk '{print $1}')" || die "DMG checksum failed."
  printf '%s  %s\n' "$sha" "${dmg_path##*/}" >"$dmg_path.sha256"

  rm -rf "$dmg_stage"

  echo "$dmg_path"
}

notarize_dmg() {
  local dmg_path="$1"

  [[ "$NOTARIZE" == "1" ]] || return 0
  [[ -n "$CODESIGN_IDENTITY" ]] || die "NOTARIZE=1 requires CODESIGN_IDENTITY."
  [[ -n "$NOTARY_KEYCHAIN_PROFILE" ]] || die "NOTARIZE=1 requires NOTARY_KEYCHAIN_PROFILE."

  echo "Submitting for notarization..."
  xcrun notarytool submit "$dmg_path" --keychain-profile "$NOTARY_KEYCHAIN_PROFILE" --wait
  xcrun stapler staple "$dmg_path"
}

package_rid() {
  local rid="$1"
  local version="$2"
  local macos_version="$3"
  local app_dir
  local dmg_path

  app_dir="$(create_app_bundle "$rid" "$macos_version")" || die "Bundle creation failed for $rid."
  sign_app_bundle "$app_dir" || die "Bundle signing failed for $rid."
  dmg_path="$(create_dmg "$rid" "$version" "$app_dir")" || die "DMG creation failed for $rid."
  notarize_dmg "$dmg_path" || die "DMG notarization failed for $rid."

  echo "Created: $dmg_path"
}

main() {
  local target="${1:-all}"
  local rids=()
  local version
  local macos_version

  if [[ "${1:-}" == "-h" || "${1:-}" == "--help" ]]; then
    usage
    exit 0
  fi

  case "$target" in
    osx-x64) rids=(osx-x64) ;;
    osx-arm64) rids=(osx-arm64) ;;
    all) rids=(osx-x64 osx-arm64) ;;
    *) die "Unknown target: $target (use osx-x64, osx-arm64 or all)" ;;
  esac

  version="$(resolve_version)"
  macos_version="$(to_macos_version "$version")"

  mkdir -p "$ARTIFACTS_ROOT"

  local rid
  for rid in "${rids[@]}"; do
    echo "==> Packaging $rid (version $version)"
    package_rid "$rid" "$version" "$macos_version"
  done

  echo "All macOS packages are under $ARTIFACTS_ROOT"
}

if [[ "${BASH_SOURCE[0]}" == "$0" ]]; then
  main "$@"
fi
