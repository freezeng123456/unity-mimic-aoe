#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
unity_editor="${UNITY_EDITOR:-}"
if [[ -z "$unity_editor" ]]; then
  unity_editor="/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity"
fi
if [[ ! -x "$unity_editor" ]]; then
  echo 'Set UNITY_EDITOR to the installed Unity 6000.6.0f1 executable.' >&2
  exit 2
fi
mkdir -p "$project_dir/Builds/macOS" "$project_dir/Builds/Logs"
export FRONTIER_BUILD_PATH="${FRONTIER_BUILD_PATH:-$project_dir/Builds/macOS/Frontier Reckoning.app}"
"$unity_editor" -batchmode -quit -projectPath "$project_dir" -executeMethod Frontier.Editor.BuildTools.PrepareAndBuildMac -logFile "$project_dir/Builds/Logs/build-macos.log"
echo "Built: $FRONTIER_BUILD_PATH"
