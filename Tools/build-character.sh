#!/bin/sh
set -eu
project_root=$(CDPATH= cd -- "$(dirname -- "$0")/.." && pwd)
character_blender=${BLENDER_EDITOR:-/Applications/Blender.app/Contents/MacOS/Blender}
character_source="$project_root/SourceAssets/Characters/Infantry024"
character_output=${FRONTIER_CHARACTER_OUTPUT:-"$project_root/Builds/CharacterAuthoring"}
"$character_blender" -b --python "$character_source/build_character.py" -- --out "$character_output"
printf '%s\n' "Authored files written to $character_output. Review renders, then copy FrontierInfantry.fbx to Assets/Resources/Models and verify Unity import before accepting."
