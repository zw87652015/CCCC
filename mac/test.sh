#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")"
tmp=$(mktemp -d)
trap 'rm -rf "$tmp"' EXIT
swiftc Sources/CCCC/PauseControl.swift Sources/CCCC/ConfigFile.swift tests/main.swift -o "$tmp/tests"
"$tmp/tests"
