#!/usr/bin/env bash
# 使用受控命令验证打包失败传播与符号排除，不依赖本机 macOS 工具。
set -euo pipefail
source "$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)/package_macos.sh"

fixture_root="$(mktemp -d "${CODEWF_TEST_TMP:-${TMPDIR:-/tmp}}/codewf-macos-test-XXXXXX")"
[[ "$fixture_root" == */codewf-macos-test-* ]] || die "Unexpected test directory."
trap 'rm -rf "$fixture_root"' EXIT
PUBLISH_ROOT="$fixture_root/publish"
ARTIFACTS_ROOT="$fixture_root/artifacts"
WORK_ROOT="$fixture_root/work"
CODESIGN_IDENTITY=""
NOTARIZE=0
mkdir -p "$PUBLISH_ROOT/osx-x64/$APP_NAME/$APP_NAME.dSYM" "$ARTIFACTS_ROOT" "$WORK_ROOT"
printf '#!/bin/sh\nexit 0\n' >"$PUBLISH_ROOT/osx-x64/$APP_NAME/$APP_NAME"
chmod +x "$PUBLISH_ROOT/osx-x64/$APP_NAME/$APP_NAME"
printf 'symbols' >"$PUBLISH_ROOT/osx-x64/$APP_NAME/$APP_NAME.dSYM/data"
printf 'symbols' >"$PUBLISH_ROOT/osx-x64/$APP_NAME/$APP_NAME.pdb"
printf 'resource' >"$PUBLISH_ROOT/osx-x64/$APP_NAME/settings.json"

shasum() { sha256sum "${!#}"; }
ditto() { cp -R "$1" "$2"; }
create_icon() { printf 'icon' >"$1"; }
publish_app() { :; }
sign_app_bundle() { :; }
ln() { printf '/Applications' >"${!#}"; }
hdiutil() {
  [[ "${simulate_failure:-0}" == "0" ]] || return 28
  case "$1" in
    create) printf 'writable image' >"${!#}" ;;
    attach|detach) : ;;
    convert) while [[ $# -gt 0 ]]; do if [[ "$1" == -o ]]; then printf 'compressed image' >"$2"; fi; shift; done ;;
    verify) [[ -s "$2" ]] ;;
    *) return 2 ;;
  esac
}
osascript() { printf 'Finder metadata' >"$2/.DS_Store"; }
sync() { :; }

app_dir="$(create_app_bundle osx-x64 1.5.5)"
[[ -f "$app_dir/Contents/MacOS/$APP_NAME" ]] || die "Executable missing."
[[ -f "$app_dir/Contents/Resources/settings.json" ]] || die "Runtime resource missing."
[[ ! -e "$app_dir/Contents/Resources/$APP_NAME.dSYM" ]] || die "Debug directory entered the bundle."
[[ ! -e "$app_dir/Contents/Resources/$APP_NAME.pdb" ]] || die "Debug symbols entered the bundle."
[[ -f "$PUBLISH_ROOT/osx-x64/$APP_NAME/$APP_NAME.dSYM/data" ]] || die "Source symbols must be retained."
dmg_path="$(create_dmg osx-x64 1.5.5 "$app_dir")"
[[ -s "$dmg_path" && ! -e "$WORK_ROOT/dmg-osx-x64" ]] || die "Successful staging was not released."

simulate_failure=1
if (package_rid osx-x64 1.5.5 1.5.5) >"$fixture_root/failure.log" 2>&1; then
  die "A failed DMG command reported success."
fi
if grep -q 'Created:' "$fixture_root/failure.log"; then
  die "A failed DMG command printed a success message."
fi
echo "PASS: debug symbol exclusion, runtime resources, staging cleanup and DMG failure propagation."
