using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.InputSystem;

namespace Frontier
{
    [Serializable] public sealed class CaptureMetadata
    {
        public int schemaVersion = 4, eventSchemaVersion = SimEvent.CurrentSchemaVersion;
        public string pngFile, pngSha256, utc, appVersion, buildGuid, unityVersion, matchId, requestMatchId;
        public int requestFrame, captureFrame, width, height, seed;
        public float requestWorldTime, worldTime;
        public bool inMenu, paused, hasPlayer;
        public Cost resources;
        public PopulationSnapshot population;
        public string hudResources, hudMatchId;
        public int hudUpdatedFrame;
        public float hudWorldTime;
        public CaptureCamera camera;
        public string aiDifficulty="",mapId="";
        public float aiDecisionSeconds,aiArmySeconds;
        public CaptureProduction[] production = Array.Empty<CaptureProduction>();
        public bool generatedTerrain,requiresTransport;public int mapGeneratorVersion;
        public MapObjectiveState objective;public CaptureUnit[] units=Array.Empty<CaptureUnit>();
        public CaptureNeutralService[] neutralServices=Array.Empty<CaptureNeutralService>();
        public CapturePlayer[] players=Array.Empty<CapturePlayer>();
        public string skirmishMode="",guidedId="",guidedStatus="",guidedStage="";
        public PlayerSettings preferences;
        public int observerPlayer;
        public CaptureReplay replay;

        // Detached observer-owned state; this does not refresh UI or inspect
        // another player's production, units or unseen neutral information.
        public static CaptureMetadata Read(GameController game)
        {
            var w = game.World;
            var result = new CaptureMetadata
            {
                utc = DateTime.UtcNow.ToString("O"), appVersion = Application.version,
                buildGuid = Application.buildGUID, unityVersion = Application.unityVersion,
                matchId = game.MatchId, captureFrame = Time.frameCount,
                width = Screen.width, height = Screen.height, inMenu = game.InMenu,
                paused = game.IsPaused, seed = w?.Seed ?? 0, worldTime = w?.Time ?? 0,
                hudResources = game.Hud.ResourceSummary, hudUpdatedFrame = game.Hud.ResourceSummaryFrame,
                hudWorldTime = game.Hud.ResourceSummaryTime, hudMatchId = game.Hud.ResourceSummaryMatchId,
                skirmishMode=game.SkirmishMode,preferences=game.Settings.Current,
                observerPlayer=game.ObserverPlayer,replay=CaptureReplay.Read(game),
                guidedId=game.Guided?.Definition.Id??"",guidedStatus=game.Guided?.Status.ToString()??"",guidedStage=game.Guided?.Stage?.Title??"",
                camera = new CaptureCamera
                {
                    focus = game.Controls.CameraFocus,
                    pointer = Mouse.current?.position.ReadValue() ?? Vector2.zero,
                    applicationFocused = Application.isFocused,
                    orthographicSize = game.GameCamera.orthographicSize
                }
            };
            if (w == null || game.InMenu) return result;
            result.mapId=w.MapId;result.generatedTerrain=w.Layout.Generated;result.requiresTransport=w.Layout.RequiresTransport;result.mapGeneratorVersion=w.Layout.Generated?MapGeneration.Version:0;
            result.objective=SnapshotCodec.Clone(w.MapObjective);
            result.aiDifficulty=game.Simulation?.Difficulty.Id??"";
            result.aiDecisionSeconds=game.Simulation?.Difficulty.DecisionSeconds??0;
            result.aiArmySeconds=game.Simulation?.Difficulty.ArmySeconds??0;
            result.players=new CapturePlayer[w.Players.Length];for(int i=0;i<w.Players.Length;i++){var p=w.Players[i];result.players[i]=new CapturePlayer{slot=i,team=p.team,civilization=p.civilization,resigned=p.resigned,eliminated=p.eliminated,winner=w.IsPlayerWinner(i)};}
            int observer=game.ObserverPlayer;
            result.hasPlayer = true; result.resources = w.Players[observer].resources;
            result.population = PopulationSnapshot.From(w.Players[observer]);
            var buildings = new List<CaptureProduction>();
            foreach (var e in w.Entities)
            {
                if (!e.Alive || e.owner != observer || e.def.kind != EntityKind.Building) continue;
                var b = new CaptureProduction
                {
                    entity = e.id, definition = e.def.id, position = e.position, hp = e.hp,
                    construction = e.construction, factoryMode=e.def.id=="factory"?e.factoryMode:"",
                    factoryElapsed=e.def.id=="factory"?e.factoryElapsed:0, queue = new CaptureProductionJob[e.queue.Count]
                };
                for (int i = 0; i < e.queue.Count; i++)
                {
                    var j = e.queue[i];
                    b.queue[i] = new CaptureProductionJob
                    { kind = j.kind, item = j.item, count = j.count, population = j.population, elapsed = j.elapsed, duration = j.duration };
                }
                buildings.Add(b);
            }
            result.production = buildings.ToArray();
            var units=new List<CaptureUnit>();foreach(var unit in w.Entities)if(unit.Alive&&unit.owner==observer&&unit.def.kind==EntityKind.Unit)
            {var order=unit.orders.Count>0?unit.orders.Peek():null;units.Add(new CaptureUnit{entity=unit.id,definition=unit.def.id,status=unit.status,position=unit.position,hp=unit.hp,garrisonedIn=unit.garrisonedIn,cargo=unit.occupants.ToArray(),load=unit.def.transportShip?w.TransportLoad(unit):0,capacity=unit.def.transportCapacity,order=order?.kind.ToString()??"",target=order?.target??0,destination=order?.point??unit.position});}result.units=units.ToArray();
            var neutral=new List<CaptureNeutralService>();foreach(var state in w.NeutralAllies)if(w.IsVisible(observer,w.Get(state.site)))
            {var job=state.owner==observer&&state.jobs.Count>0?state.jobs[0]:null;neutral.Add(new CaptureNeutralService{id=state.id,site=state.site,owner=state.owner,claimant=state.claimant,contested=state.contested,claimProgress=state.claimant==observer?state.claimProgress:0,service=job?.kind??"",item=job?.item??"",elapsed=job?.elapsed??0,duration=job?.duration??0});}result.neutralServices=neutral.ToArray();
            return result;
        }
    }
    [Serializable] public sealed class CaptureReplay
    {
        public bool watching,recording,seek,paused,worldPaused,verified,incomplete;
        public string segment="",file="",lastSavedFile="",error="",incompleteReason="",fileError="",libraryError="";
        public int startStep,endStep,currentStep,observerPlayer;
        public float speed=1;
        public ReplayIdentity identity;
        public static CaptureReplay Read(GameController game)
        {
            var playback=game.Playback;var recorder=game.Recorder;
            var result=new CaptureReplay
            {
                watching=game.IsWatching,recording=recorder?.IsRecording??false,
                paused=game.IsPaused,worldPaused=game.World?.Paused??false,
                currentStep=game.Simulation?.StepCount??0,observerPlayer=game.ObserverPlayer,
                speed=game.IsWatching?game.ReplaySpeed:game.Autoplay?game.SimulationSpeed:1,
                fileError=game.ReplayFileError,libraryError=game.ReplayLibraryError,lastSavedFile=game.LastReplayFile,
                identity=game.CurrentReplayIdentity()
            };
            if(playback!=null)
            {
                result.segment=game.WatchingDocument.segmentId;result.file=game.ReplayFile;
                result.startStep=playback.StartStep;result.endStep=playback.EndStep;result.currentStep=playback.CurrentStep;
                result.seek=playback.IsSeeking;result.verified=playback.Verified;result.error=playback.Error;
                result.incomplete=game.WatchingDocument.incomplete;result.incompleteReason=game.WatchingDocument.incompleteReason;
            }
            else if(recorder!=null)
            {
                result.segment=recorder.SegmentId;result.startStep=recorder.StartStep;result.endStep=recorder.RecordedEndStep;
                result.error=recorder.Error;result.incomplete=!string.IsNullOrEmpty(recorder.Error);result.incompleteReason=recorder.Error;
                // A previous match's saved path must not be associated with a
                // fresh recorder. Only a successful save of this segment counts.
                string candidate=ReplayStore.PathFor(game.ReplayDirectory,recorder.SegmentId);
                if(game.LastReplayFile==candidate)result.file=game.LastReplayFile;
            }
            return result;
        }
    }
    [Serializable] public sealed class CaptureUnit
    {public int entity,garrisonedIn,load,capacity,target;public string definition,status,order;public Vector2 position,destination;public float hp;public int[] cargo;}
    [Serializable] public sealed class CapturePlayer
    {public int slot,team;public string civilization;public bool resigned,eliminated,winner;}
    [Serializable] public sealed class CaptureNeutralService
    {public string id,service,item;public int site,owner,claimant;public bool contested;public float claimProgress,elapsed,duration;}
    [Serializable] public sealed class CaptureCamera
    { public Vector2 focus, pointer; public bool applicationFocused; public float orthographicSize; }
    [Serializable] public sealed class CaptureProduction
    { public int entity; public string definition,factoryMode; public Vector2 position; public float hp, construction,factoryElapsed; public CaptureProductionJob[] queue; }
    [Serializable] public sealed class CaptureProductionJob
    { public string kind, item; public int count, population; public float elapsed, duration; }

    public sealed class NativeCapture : MonoBehaviour
    {
        private GameController game;
        public bool Busy { get; private set; }
        public void Initialize(GameController controller) { game = controller; }

        public bool Request()
        {
            if (game == null || !isActiveAndEnabled || Busy) return false;
            // Editor batch mode never resumes WaitForEndOfFrame. No fake success for headless runs.
            if (Application.isBatchMode || SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            { Debug.LogWarning("FRONTIER_CAPTURE_UNAVAILABLE no interactive rendered frame"); return false; }
            Busy = true;
            StartCoroutine(AtFrameEnd(game.MatchId, Time.frameCount, game.World?.Time ?? 0));
            return true;
        }

        private IEnumerator AtFrameEnd(string matchId, int requestFrame, float requestTime)
        {
            // A yield cannot occur inside a try block with a catch clause.
            yield return new WaitForEndOfFrame();
            Texture2D texture = null;
            try
            {
                if (game == null || game.MatchId != matchId)
                { Debug.LogWarning("FRONTIER_CAPTURE_CANCELLED session changed before rendering"); yield break; }
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null) throw new InvalidOperationException("Screen capture returned no texture");
                var metadata = CaptureMetadata.Read(game);
                metadata.requestMatchId = matchId; metadata.requestFrame = requestFrame;
                metadata.requestWorldTime = requestTime; metadata.width = texture.width; metadata.height = texture.height;
                byte[] png = texture.EncodeToPNG();
                if (png == null || png.Length == 0) throw new InvalidOperationException("PNG encoding returned no data");
                using (var hash = SHA256.Create())
                    metadata.pngSha256 = BitConverter.ToString(hash.ComputeHash(png)).Replace("-", "").ToLowerInvariant();
                string stem = "Frontier-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + "-f" + metadata.captureFrame + "-" + Guid.NewGuid().ToString("N");
                metadata.pngFile = stem + ".png";
                string directory = Path.Combine(Application.persistentDataPath, "Captures");
                Directory.CreateDirectory(directory);
                // JSON is the completion record. A partial PNG without JSON must not be treated as a valid pair.
                string path = Path.Combine(directory, stem);
                File.WriteAllBytes(path + ".png", png);
                File.WriteAllText(path + ".json", JsonUtility.ToJson(metadata, true));
                Debug.Log("FRONTIER_CAPTURE_OK " + path + ".json");
                game.Notify("Screenshot saved to Captures.");
            }
            catch (Exception ex)
            {
                Debug.LogError("FRONTIER_CAPTURE_FAILED " + ex.GetType().Name + ": " + ex.Message);
                if (game != null) game.Notify("Screenshot could not be saved.");
            }
            finally { if (texture != null) Destroy(texture); Busy = false; }
        }
        private void OnDisable() { StopAllCoroutines(); Busy = false; }
    }
}
