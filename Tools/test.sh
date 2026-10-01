#!/usr/bin/env bash
set -euo pipefail
project_dir="$(cd "$(dirname "$0")/.." && pwd)"
source "$project_dir/Tools/unity-editor.sh"
unity_editor="$(frontier_unity_editor "$project_dir")"
mkdir -p "$project_dir/Builds/Tests"
reports="$(mktemp -d "$project_dir/Builds/Tests/run-XXXXXXXX")"
fingerprint="$(python3 "$project_dir/Tools/verification_evidence.py")"
printf 'This run writes fresh results to: %s\n' "$reports"
"$unity_editor" -batchmode -nographics -projectPath "$project_dir" -runTests -testPlatform EditMode -testResults "$reports/EditMode.xml" -logFile "$reports/EditMode.log"
"$unity_editor" -batchmode -projectPath "$project_dir" -runTests -testPlatform PlayMode -testResults "$reports/PlayMode.xml" -logFile "$reports/PlayMode.log"

python3 "$project_dir/Tools/verification_evidence.py" --record "$reports" --expected-fingerprint "$fingerprint"
python3 "$project_dir/Tools/verify-delivery.py" --reports-dir "$reports" --require-current-source
printf '%s\n' "$(basename "$reports")" > "$project_dir/Builds/Tests/latest-run.txt"
