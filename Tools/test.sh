#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
unity_editor="${UNITY_EDITOR:-/Applications/Unity/Hub/Editor/6000.6.0f1/Unity.app/Contents/MacOS/Unity}"
mkdir -p "$project_dir/Builds/Tests"
"$unity_editor" -batchmode -nographics -projectPath "$project_dir" -runTests -testPlatform EditMode -testResults "$project_dir/Builds/Tests/EditMode.xml" -logFile "$project_dir/Builds/Tests/EditMode.log"
"$unity_editor" -batchmode -projectPath "$project_dir" -runTests -testPlatform PlayMode -testResults "$project_dir/Builds/Tests/PlayMode.xml" -logFile "$project_dir/Builds/Tests/PlayMode.log"

python3 "$project_dir/Tools/verify-delivery.py" --reports-dir "$project_dir/Builds/Tests"
