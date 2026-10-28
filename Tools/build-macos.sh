#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
source "$project_dir/Tools/unity-editor.sh"
unity_editor="$(frontier_unity_editor "$project_dir")"
mkdir -p "$project_dir/Builds/macOS" "$project_dir/Builds/Logs"
export FRONTIER_BUILD_PATH="${FRONTIER_BUILD_PATH:-$project_dir/Builds/macOS/Frontier Reckoning.app}"
"$unity_editor" -batchmode -quit -projectPath "$project_dir" -executeMethod Frontier.Editor.BuildTools.PrepareAndBuildMac -logFile "$project_dir/Builds/Logs/build-macos.log"
echo "Built: $FRONTIER_BUILD_PATH"
