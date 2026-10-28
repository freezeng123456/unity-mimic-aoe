# Scenario 069 data and runtime contract

Project rules P. This is a format/implementation contract, not an acceptance result.

`ScenarioDocument` schema 1: `id/name/description/rulesHash`, `width=128/height=128/seed/waterHeightCm=-40/victoryPlayer/enableElimination=true`, `int[] heightsCm` (129² vertices, centimetres), `int[] surfaces` (128² cells: 0 grass, 1 sand, 2 mud, 3 blocking rock), and the arrays below. Height samples are row-major `x+y*129`; surfaces use `x+y*128`. Rivers derive water/shore geometry; there is no competing water mask. Bridge height is absolute deck Y. The compiler, not the codec, checks terrain, physical overlap, navigation, bridges and usable starts; structurally valid drafts may be saved while playtest validation reports geography errors.

| Array / DTO | Fields |
|---|---|
| rivers / ScenarioRiver | `id, Vector2[] points, float[] halfWidths, bool smooth=true`; free non-monotone controls, widths 1–48, control coordinates −32…160; the bounds retain existing estuary templates |
| bridges / ScenarioBridge | `id, Vector2 center/direction, float halfLength/halfWidth/rampLength/height`; direction must be unit length |
| starts / ScenarioStart | `int player/team`, `int age=1`, `string civilization="coast"`, `Cost initialResources=(300,300,100)`, `Vector2 town/explorer, Vector2[] workers`; exactly 2–4 ordered slots numbered 0…n−1, positions and starting balances are explicit. Offline playtest uses this roster/economy/era; LAN uses lobby civ/team while preserving document resources/era. The codec checks the catalog era range; controller draft validation and World initialization check the match era cap. |
| resources, objects / ScenarioPlacement | `id, definition, Vector2 position, int owner=-1, float remaining=-1, bool complete=true`; resources use resource catalog kinds; objects use unit/building kinds; −1 remaining uses the catalog amount |
| tradeRoute | `Vector2[]`, sequential route waypoints |
| tradeSockets / ScenarioTradeSocket | `id, Vector2 position, int waypoint` into tradeRoute |
| treasures / ScenarioTreasure | `id, int definition` into `rules.landmarks.treasures`, `Vector2 position, Vector2[] guardPositions`; definition binds the reward/XP and positions bind actual guards |
| neutralBindings / ScenarioNeutralBinding | `definition` neutral ally catalog ID, `socketId` stable socket ID; one binding per definition/socket |
| regions / ScenarioRegion | `id, Vector2 center/size`; axis-aligned rectangle entirely on map |
| objectives / ScenarioObjective | `id/title/description/kind/objectId/regionId`, `int player`, `bool required=true`, `ResourceKind resource`, `float amount=1/holdSeconds=30/deadline=0`, `string[] prerequisites` |
| triggers / ScenarioTrigger | `id, bool enabled=true/matchAny=false, int maxFires=1, float repeatSeconds=1, ScenarioCondition[] conditions, ScenarioAction[] actions` |
| ScenarioCondition | `kind/objectId/regionId/objectiveId/triggerId`, `int player`, `ResourceKind resource`, `float amount=1/time=0` |
| ScenarioAction | `kind/objectiveId/regionId/objectId/triggerId/definition/targetId/message`, `int player/count=1`, `Vector2 point`, `ResourceKind resource`, `float amount=1` |

Objective kinds: `manual`, `hold_region`, `destroy_object`, `reach_region`, `collect_resource`. Conditions: `time`, `object_dead`, `units_in_region`, `resource`, `objective_complete`, `objective_failed`, `trigger_fired`. Actions: `message`, `spawn`, `order_move`, `order_attack`, `grant_resource`, `complete_objective`, `fail_objective`, `win`, `lose`, `enable_trigger`, `disable_trigger`. Strings are bounded white lists; no script execution. Empty references mean unused fields. Players are slot numbers, resource values are the existing `ResourceKind` enum (Food/Wood/Coin where used).

Stable IDs use 1–64 ASCII lowercase letters/digits/underscore/hyphen and are globally unique across authored objects, rivers, bridges, sockets, treasures, regions, objectives and triggers. Spawn actions can predeclare a new nonempty `objectId` group; later references resolve that group. Empty spawn IDs create unnamed runtime units. `object_dead` is false before a group exists and true only after all its members die; repeated spawns append members to the same group. Starting actors have reserved stable IDs `start-{player}-town`, `start-{player}-explorer`, `start-{player}-worker-{index}`; treasure guards use `{treasureId}-guard-{index}`. Authored IDs cannot shadow these. Targets use stable IDs, never runtime entity numbers.

Limits: 2 MiB encoded UTF-8 file; 2048 resources+objects; 8 rivers/512 total control points; 32 bridges; 32 trade sockets/512 route points; 32 treasures/32 guards each; 64 regions; 32 objectives; 64 triggers, each 1–8 conditions and actions; maxFires 1–64, repeatSeconds ≥0.1; each spawn action 1–64 units and total `sum(count*maxFires)` ≤4096. Heights −2000…10000 cm, water −2000…10000 cm; ordinary map positions 0…128. All floating numbers must be finite and bounded. Objective prerequisites cannot cycle; all references and catalog definitions must exist. Format, rule hash, unknown/duplicate/missing JSON fields, scalar types and stored SHA-256 are strictly validated before replacing any state.

Resource catalog radii describe selection/gathering extents, not registered Navigation/MovementCombat hard bodies. Resource–resource and resource–unit pairs may overlap, so dense trees and nearby huntables survive template expansion without deletion or relocation. Unit–unit bodies must remain separated; buildings require their full footprint to be clear of units/resources and other buildings. Terrain, water/land placement and navigation checks still apply, including to resource placements. A compiler success is an opening geometry check, not proof of mission solvability, balance or AI strategy.

Public APIs:

```csharp
string ScenarioCodec.Validate(ScenarioDocument doc, RuleCatalog rules); // empty = valid
string ScenarioCodec.Encode(ScenarioDocument doc, RuleCatalog rules); // envelope JSON
ScenarioDocument ScenarioCodec.Decode(string json, RuleCatalog rules); // rejects invalid envelope
string ScenarioCodec.ContentHash(ScenarioDocument doc); // SHA-256 of normalized JsonUtility DTO JSON
ScenarioDocument ScenarioCodec.Clone(ScenarioDocument doc);
ScenarioDocument ScenarioEditorSession.FromMap(RuleCatalog rules, string mapId, int seed, int playerCount);
new ScenarioEditorSession(RuleCatalog rules, ScenarioDocument doc);
session.Document; session.CanUndo; session.CanRedo; // Document is a deep copy
bool session.Apply(Action<ScenarioDocument> mutation, out string reason);
bool session.Undo(); bool session.Redo();
new ScenarioStore(string directory, RuleCatalog rules);
store.DirectoryPath;
ScenarioFileInfo[] store.List(); // id/name/contentHash/path/reason; damaged rows have reason
ScenarioDocument store.Read(string id); ScenarioDocument store.ReadFile(string path);
string store.Write(ScenarioDocument doc); // returns written path
ScenarioDocument store.Import(string json); // parses only, no library mutation
string store.Export(string id); string store.ExportFile(string path, ScenarioDocument doc);
```

An Apply is one complete edit transaction. It validates a deep copy before replacing the draft/history and copies the result again to prevent retained mutation references from owning the draft. A failed edit leaves both unchanged; a valid no-op adds no history. Undo and Redo each retain at most 32 whole-document states/8 MiB. Playtest receives a separate deep copy. ContentHash binds the engine's normalized DTO serialization, not a general cross-build JSON canonicalization promise.

The library has a 128-JSON-file limit, including damaged rows; exceeding it fails without removing entries. Atomic disk writes use an exclusive temporary file, durable flush and replacement; failure preserves the previous file. Import parses a portable document without mutating the library. Controller exports use sibling `ScenarioExports`, and recovery files use sibling `ScenarioDrafts`. Successful draft replacement first writes a portable recovery copy, then retains at most four previous in-memory sessions plus the current session. Those sessions retain Undo/Redo; portable and latest files retain only documents. Recovery files are not automatically removed, so the recovery directory's total file count is not bounded by the library cap. A failed recovery write prevents replacement; a failed close-time persistence write keeps the editor open. Normal exit attempts latest-document persistence; forced termination does not guarantee unsaved recovery.

FromMap expands actual template terrain, river samples, opening actor/resource positions, trade and neutral sites into document data, never just saves a map ID pointer. It records base/bank heights without baking bridge decks into ground and retains permitted resource clusters. Starting actors, sockets, treasures and their guards are represented by their specific arrays rather than duplicated as generic objects.

Runtime compiles document copies with `ScenarioCompiler.Validate(doc,rules,players,teamsOverride=null)` and `Compile(doc,rules,players)`; freezes content hash; runs ordered finite triggers only at authoritative simulation steps; stores trigger/objective/group bindings in the world snapshot. Custom LAN requires four explicit starts and applies the cooperation lobby's civilization/team roster; clients apply host state rather than firing triggers independently. LAN readiness and save/replay identity include the frozen document/hash. Scene document schema 1, world snapshot schema 4 and compressed match-save envelope schema 2 are separate version fields. Restore is designed to work after deleting the source map file while still requiring compatible application/rules/build identity. Checkpoint load creates a regular custom match, not a restored editor session or playtest return marker. These behaviors require independent runtime and native acceptance; a successful codec roundtrip alone does not prove them. Current 0.12 candidate 2 acceptance is tracked in [Completion-Status](Completion-Status.md).
