# 0.2.20 shadow-removal scan

Read-only source scan; no Unity, GUI, or project files were changed.

## Actual shadow sources

The authoritative scene light is `Assets/Frontier/Scenes/FrontierSkirmish.unity:145-160`: a directional light has `m_Shadows.m_Type: 2` (soft shadows), strength 1. The generated equivalent is `Assets/Frontier/Editor/BuildTools.cs:38-40`, where `Late afternoon sun` is created with `LightShadows.Soft`.

The URP asset still enables the main-light shadow pipeline: `Assets/Frontier/Settings/FrontierURP.asset:44-66` has `m_MainLightShadowsSupported: 1`, `m_ShadowDistance: 85`, `m_AnyShadowsSupported: 1`. The scene camera also has `m_RenderShadows: 1` at `Assets/Frontier/Scenes/FrontierSkirmish.unity:249-250`. These are real projected-light shadow paths.

`Assets/Frontier/Editor/BuildTools.cs:24-26` additionally writes an 85-metre shadow distance and `QualitySettings.shadows = ShadowQuality.All`; this can re-enable shadows after asset changes unless the preparation path is updated as well. `ProjectSettings/QualitySettings.asset` contains several quality tiers with nonzero shadow settings (`:118-126`, `:171-179`, `:224-232`, `:277-285`).

## Renderers that can receive/cast shadows

`Assets/Frontier/Presentation/WorldView.cs:43-49` creates the terrain `MeshRenderer` without disabling casting or receiving. The same is true for generated rocks at `WorldView.cs:75` and entity models created through `ProceduralArt.cs:21-25`. Imported infantry renderers are traversed in `AuthoredInfantry.cs:25`; no shadow flags are changed there, so their `MeshRenderer` defaults remain eligible for both casting and receiving.

The existing explicit exclusions at `WorldView.cs:261` and `CombatFeedback.cs:68` apply only to line/effect renderers. They do not cover terrain, rocks, or unit models. No separate blob/ellipse/procedural fake-shadow implementation was found in the scanned Frontier presentation/shader sources; the visible shadow source is the light/URP path plus default renderer flags.

## Minimum complete removal scope

1. In the generated scene/build setup, disable the directional light's shadows (`BuildTools.cs:39` and the serialized scene light), and set `pipeline.shadowDistance = 0` plus `QualitySettings.shadows = ShadowQuality.Disable` in `BuildTools.cs:24-26`.
2. Disable URP main-light/any shadow support and shadow distance in `FrontierURP.asset:44-66`; set the scene camera's `m_RenderShadows` to `0` if the scene remains serialized as an input.
3. For defense against any future light or pipeline re-enabling, set `shadowCastingMode = Off` and `receiveShadows = false` on every generated terrain/rock/entity `MeshRenderer` (`WorldView.cs:43-49`, `:75`, `ProceduralArt.cs:21-25`) and on imported infantry renderers (`AuthoredInfantry.cs:25`).
4. Keep the existing line/effect exclusions; they are already correct but are insufficient alone.

The smallest reliable change is the renderer-level exclusions plus the BuildTools/URP/scene light settings above. Changing only the current quality tier is insufficient because `BuildTools.Prepare` explicitly restores shadows and the URP asset still advertises the shadow pipeline.
