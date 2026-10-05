using System;
using UnityEngine;

namespace Frontier
{
    public enum GameSessionRole { Single, Replay, LanHost, LanClient }
    public enum CommandRequestStatus { Pending, Applied, Rejected }
    public sealed class CommandRequest
    {
        public long Sequence {get;internal set;}
        public CommandRequestStatus Status {get;private set;}=CommandRequestStatus.Pending;
        public CommandResult Result {get;private set;}
        public bool IsTerminal=>Status!=CommandRequestStatus.Pending;
        internal Action<CommandResult> Callback;
        internal void Resolve(CommandResult result)
        {
            if(IsTerminal)return;
            Result=result;Status=result.accepted?CommandRequestStatus.Applied:CommandRequestStatus.Rejected;
            var callback=Callback;Callback=null;callback?.Invoke(result);
        }
    }
    public static class LanProtocol
    {
        public const int Version=1,MaxCommandBytes=65536,MaxActors=1024,MaxString=256;
        public const int MaxPeerPending=256,MaxHostPending=1024,ResultWindow=1024;
        public const int MaxMessagesPerPump=8;
        public const int MaxExpandedBytes=32*1024*1024,MaxWireBytes=8*1024*1024;
        public const double HeartbeatSeconds=1,TimeoutSeconds=5,ReconnectSeconds=30;
    }
    public enum LanOperation { Command, Pause, Resume }
    [Serializable] public sealed class LanCommandRequest
    {
        public string matchId="",epoch="",peerId="";
        public long sequence,expectedQueueId;
        public int generation;
        public LanOperation operation;
        public GameCommand command;
    }
    [Serializable] public sealed class LanCommandAck
    {
        public string matchId="",epoch="",peerId="",reason="";
        public long sequence,hostSequence,stateRevision;
        public int generation,appliedStep,ordinal;
        public bool terminal,accepted,protocolRejected;
    }
    [Serializable] public sealed class LanLobbySlot
    {
        public int player;
        public bool human,ready;
        public string civilization="",peerId="";
        public string[] deck=Array.Empty<string>();
    }
    [Serializable] public sealed class LanLobbyState
    {
        public string mapId="",difficulty="standard",objective="elimination";
        public int seed=314159,maxAge=5;
        public bool started;
        public LanLobbySlot[] slots=Array.Empty<LanLobbySlot>();
    }
    [Serializable] public sealed class LanHello
    {
        public ReplayIdentity identity;
        public string peerId="",resumeToken="",matchId="",epoch="";
        public long lastConfirmedSequence,stateRevision;
    }
    [Serializable] public sealed class LanWelcome
    {
        public string peerId="",resumeToken="",matchId="",epoch="";
        public int player,generation;
        public ReplayIdentity identity;
        public LanLobbyState lobby;
    }
    [Serializable] public sealed class LanReady
    {
        public bool ready;
        public string civilization="";
        public string[] deck=Array.Empty<string>();
    }
    [Serializable] public sealed class LanFullState
    {
        public string matchId="",epoch="",digest="";
        public long revision,eventCursor;
        public int completedStep;
        public WorldSnapshot world;
        public LanQueueIdentity[] queues=Array.Empty<LanQueueIdentity>();
    }
    [Serializable] public sealed class LanStateDelta
    {
        public string matchId="",epoch="",digest="";
        public long baseRevision,revision,eventStart,eventCursor;
        public int completedStep;
        // globals excludes entities/events; large grid layers are supplied only
        // when their content changes. The receiver merges into an owned baseline.
        public WorldSnapshot globals;
        public EntitySnapshot[] changed=Array.Empty<EntitySnapshot>();
        public int[] removed=Array.Empty<int>(),entityOrder=Array.Empty<int>();
        public bool hasLandNavigation,hasWaterNavigation,hasVisibility,hasTerrain;
        public SimEvent[] events=Array.Empty<SimEvent>();
        public LanQueueIdentity[] queues=Array.Empty<LanQueueIdentity>();
    }
    [Serializable] public sealed class LanPacket
    {
        public int schema;
        public string kind="",error="",matchId="",epoch="";
        public long nonce,stateRevision;
        public LanHello hello;
        public LanWelcome welcome;
        public LanReady ready;
        public LanLobbyState lobby;
        public LanCommandRequest request;
        public LanCommandAck ack;
        public LanFullState full;
        public LanStateDelta delta;
    }
}
