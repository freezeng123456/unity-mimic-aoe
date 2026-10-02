#!/usr/bin/env bash
# Source this helper from the build and test entry points.
frontier_unity_editor() {
  local project_dir="$1" version candidate actual
  version="$(sed -n 's/^m_EditorVersion: //p' "$project_dir/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"
  if [[ -n "${UNITY_EDITOR:-}" ]]; then
    if [[ ! -x "$UNITY_EDITOR" ]]; then
      echo 'UNITY_EDITOR must point to an executable Unity editor.' >&2
      return 2
    fi
    candidate="$UNITY_EDITOR"
  else
    candidate=""
    for candidate in \
      "/Applications/Unity/Hub/Editor/$version/Unity.app/Contents/MacOS/Unity" \
      "$project_dir/../Editor/Unity.app/Contents/MacOS/Unity"; do
      [[ -x "$candidate" ]] && break
    done
  fi
  if [[ ! -x "$candidate" ]]; then
    printf 'Unity %s is not installed. Install it using Unity Hub and activate a license, or set UNITY_EDITOR to its executable.\n' "$version" >&2
    return 2
  fi
  actual="$("$candidate" -version)" || return 2
  if [[ "$actual" != "$version" ]]; then
    printf 'Unity version mismatch: project requires %s, editor reports %s.\n' "$version" "$actual" >&2
    return 2
  fi
  printf '%s\n' "$candidate"
}
