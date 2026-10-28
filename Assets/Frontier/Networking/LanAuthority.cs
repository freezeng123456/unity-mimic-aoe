using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Frontier
{
    // Only the owning simulation thread calls this class. Socket workers hand
    // detached frames to the controller; neither World nor Recorder is shared.
    public sealed class LanAuthority
    {
        public const int MaxCommandCount=1000,MaxCommandIndex=1000000;
        public const float MaxCoordinateMagnitude=1000000;
        readonly World world;readonly string matchId,epoch;
        readonly Func<GameCommand,CommandResult> apply;readonly Action<bool,string> pause;
        readonly Func<GameCommand,long,CommandResult> cancelQueue;
        readonly int threadId;
        readonly Dictionary<string,Peer> peers=new Dictionary<string,Peer>(StringComparer.Ordinal);
        readonly Queue<Entry> pending=new Queue<Entry>();
        static readonly UTF8Encoding StrictUtf8=new UTF8Encoding(false,true);
        long hostSequence,lastRevision;int lastStep=-1,nextOrdinal;bool draining;

        sealed class Peer
        {
            public string Id;public int Player,Generation,Pending;public bool Active;
            public long HighestSeen,RetiredThrough;
            public readonly Dictionary<long,Entry> Entries=new Dictionary<long,Entry>();
            public readonly Queue<Entry> Results=new Queue<Entry>();
        }
        sealed class Entry
        {
            public Peer Peer;public LanOperation Operation;public GameCommand Command;
            public string Digest;public long ExpectedQueueId;public LanCommandAck Ack;
        }

        public LanAuthority(World world,string matchId,string epoch,Func<GameCommand,CommandResult> apply,Action<bool,string> pause,Func<GameCommand,long,CommandResult> cancelQueue=null)
        {
            this.world=world??throw new ArgumentNullException(nameof(world));
            RequireIdentifier(matchId,nameof(matchId));RequireIdentifier(epoch,nameof(epoch));
            this.matchId=matchId;this.epoch=epoch;this.apply=apply??throw new ArgumentNullException(nameof(apply));
            this.pause=pause??throw new ArgumentNullException(nameof(pause));this.cancelQueue=cancelQueue;threadId=Thread.CurrentThread.ManagedThreadId;
        }
        public int PendingCount {get{RequireThread();return pending.Count;}}

        public void BindPeer(string peerId,int player,int generation)
        {
            RequireThread();RequireIdentifier(peerId,nameof(peerId));
            if(peerId=="server")throw new ArgumentException("The server identity is reserved",nameof(peerId));
            if((player!=0&&player!=2)||player>=world.Players.Length)throw new ArgumentOutOfRangeException(nameof(player),"This LAN mode binds human slots 0 and 2");
            if(generation<1)throw new ArgumentOutOfRangeException(nameof(generation));
            if(peers.TryGetValue(peerId,out var existing))
            {
                if(existing.Player!=player)throw new ArgumentException("A peer cannot change its player slot",nameof(player));
                if(generation<existing.Generation)throw new ArgumentException("Cannot bind an older connection generation",nameof(generation));
                existing.Generation=generation;existing.Active=true;return;
            }
            string replaced=null;
            foreach(var pair in peers)
            {
                if(pair.Value.Player!=player)continue;
                if(pair.Value.Active)throw new ArgumentException("The human slot is already bound",nameof(player));
                replaced=pair.Key;break;
            }
            // Retired peers cannot send wire requests. Old pending entries keep
            // their own Peer reference and still drain exactly once.
            if(replaced!=null)peers.Remove(replaced);
            peers.Add(peerId,new Peer{Id=peerId,Player=player,Generation=generation,Active=true});
        }
        public void RetirePeer(string peerId,int generation=0)
        {
            RequireThread();
            if(peerId!=null&&peers.TryGetValue(peerId,out var peer)&&(generation==0||generation==peer.Generation))peer.Active=false;
        }

        public LanCommandAck Submit(LanCommandRequest request)
        {
            RequireThread();
            if(request==null)return Reject(null,"Missing LAN request");
            if(request.matchId!=matchId||request.epoch!=epoch)return Reject(request,"LAN match or epoch does not match");
            if(request.peerId==null||!peers.TryGetValue(request.peerId,out var peer)||!peer.Active)return Reject(request,"LAN peer is not bound");
            if(request.generation!=peer.Generation)return Reject(request,"LAN connection generation is stale");
            if(request.sequence<1)return Reject(request,"LAN request sequence must be positive");

            string error=PayloadError(request.operation,request.command,peer.Player,false);
            if(error==null)error=QueueReferenceError(request.operation,request.command,request.expectedQueueId);
            // A malformed payload is bounded/rejected before hashing or copying
            // large arrays. All well-shaped payload fields, including null vs
            // empty, participate in the digest. Generation is deliberately not
            // in it: reconnect retransmits the same logical request.
            string digest=error==null?PayloadDigest(request.operation,request.command,request.expectedQueueId):null;
            if(peer.Entries.TryGetValue(request.sequence,out var previous))
            {
                if(previous.Digest==digest&&(digest!=null||previous.Ack.protocolRejected))return CopyAck(previous.Ack,peer.Generation);
                return Conflict(previous,peer.Generation);
            }
            var entry=new Entry{Peer=peer,Operation=request.operation,Digest=digest,
                Ack=NewAck(peer.Id,request.sequence,peer.Generation,NextHostSequence())};
            peer.Entries.Add(request.sequence,entry);
            if(request.sequence<=peer.HighestSeen||request.sequence<=peer.RetiredThrough)
                return CompleteRejected(entry,"LAN request sequence expired",true);
            peer.HighestSeen=request.sequence;
            if(error!=null)return CompleteRejected(entry,error,true);
            string ownership=OwnershipError(request.command,peer.Player);
            if(request.operation==LanOperation.Command&&ownership!=null)return CompleteRejected(entry,ownership,true);
            if(peer.Pending>=LanProtocol.MaxPeerPending||pending.Count>=LanProtocol.MaxHostPending)
                return CompleteRejected(entry,"LAN pending command limit exceeded",true);
            entry.Command=CopyCommand(request.command);entry.ExpectedQueueId=request.expectedQueueId;peer.Pending++;pending.Enqueue(entry);
            return CopyAck(entry.Ack,peer.Generation);
        }

        // Trusted host controls (timeout/Leave, etc.) use the same FIFO and
        // Recorder delegate, without pretending that peer 0 owns player 2.
        // This entry point is never dispatched from a received wire packet.
        public LanCommandAck SubmitSystem(LanOperation operation,GameCommand command=null)
        {
            RequireThread();var entry=new Entry{Operation=operation,Ack=NewAck("server",0,0,NextHostSequence())};
            string error=PayloadError(operation,command,-1,true);
            if(error==null)error=QueueReferenceError(operation,command,0);
            if(error!=null)return CompleteRejected(entry,error,true);
            if(operation==LanOperation.Command)
            {
                string ownership=OwnershipError(command,command.player);
                if(ownership!=null)return CompleteRejected(entry,ownership,true);
            }
            if(pending.Count>=LanProtocol.MaxHostPending)return CompleteRejected(entry,"LAN pending command limit exceeded",true);
            entry.Command=CopyCommand(command);pending.Enqueue(entry);return CopyAck(entry.Ack,0);
        }

        public LanCommandAck[] Drain(int completedStep,long stateRevision)
        {
            RequireThread();
            if(completedStep<0||completedStep<lastStep)throw new ArgumentOutOfRangeException(nameof(completedStep));
            if(stateRevision<0||stateRevision<lastRevision)throw new ArgumentOutOfRangeException(nameof(stateRevision));
            if(draining)throw new InvalidOperationException("LAN command draining cannot re-enter");
            if(completedStep!=lastStep){lastStep=completedStep;nextOrdinal=0;}lastRevision=stateRevision;
            var results=new List<LanCommandAck>(pending.Count);int count=pending.Count;draining=true;
            try
            {
                // A delegate may submit future work, but cannot extend this
                // pump without a bound. Accepted work survives reconnection.
                while(count-->0)
                {
                    var entry=pending.Dequeue();if(entry.Peer!=null)entry.Peer.Pending--;
                    CommandResult result;bool protocolRejected=false;
                    try
                    {
                        if(entry.Operation==LanOperation.Command)
                        {
                            int player=entry.Peer?.Player??entry.Command.player;
                            string ownership=OwnershipError(entry.Command,player);
                            if(ownership!=null){result=CommandResult.Fail(ownership);protocolRejected=true;}
                            else if(entry.Command.kind==CommandKind.CancelQueue)
                            {
                                if(cancelQueue==null){result=CommandResult.Fail("LAN stable queue cancellation is unavailable");protocolRejected=true;}
                                else result=cancelQueue(CopyCommand(entry.Command),entry.ExpectedQueueId);
                            }
                            else result=apply(CopyCommand(entry.Command));
                        }
                        else if(world.IsMatchEnded)result=CommandResult.Fail("Match has ended");
                        else
                        {
                            bool paused=entry.Operation==LanOperation.Pause;
                            pause(paused,(paused?"lan_pause:":"lan_resume:")+entry.Ack.peerId);
                            result=CommandResult.Ok(paused?"LAN match paused":"LAN match resumed");
                        }
                    }
                    catch(Exception failure)
                    {
                        // An Issue delegate might have already changed state.
                        // Freeze a visible terminal error, never retry it.
                        result=CommandResult.Fail("LAN command execution failed: "+failure.Message);
                    }
                    entry.Ack.terminal=true;entry.Ack.accepted=result.accepted;entry.Ack.protocolRejected=protocolRejected;
                    entry.Ack.reason=BoundReason(result.reason);entry.Ack.appliedStep=completedStep;
                    entry.Ack.ordinal=nextOrdinal++;entry.Ack.stateRevision=stateRevision;entry.Command=null;
                    Remember(entry);results.Add(CopyAck(entry.Ack,entry.Peer?.Generation??0));
                }
            }
            finally{draining=false;}
            return results.ToArray();
        }

        string PayloadError(LanOperation operation,GameCommand command,int player,bool trusted)
        {
            if(!Enum.IsDefined(typeof(LanOperation),operation))return "Invalid LAN operation";
            if(operation==LanOperation.Command&&command==null)return "Missing LAN command";
            if(command==null)return null;
            if(command.player<0||command.player>=world.Players.Length)return "Invalid LAN command player";
            if(operation==LanOperation.Command&&!trusted&&command.player!=player)return "LAN command player is not the bound slot";
            if(!Enum.IsDefined(typeof(CommandKind),command.kind)||!Enum.IsDefined(typeof(OrderKind),command.order))return "Invalid LAN command enum";
            if(!Finite(command.point.x)||!Finite(command.point.y)||Math.Abs(command.point.x)>MaxCoordinateMagnitude||Math.Abs(command.point.y)>MaxCoordinateMagnitude)return "Invalid LAN command coordinates";
            if(command.actors!=null&&command.actors.Length>LanProtocol.MaxActors)return "LAN actor limit exceeded";
            if(command.actors!=null)foreach(int actor in command.actors)if(actor<1)return "Invalid LAN actor identifier";
            if(command.target<0||command.index<0||command.index>MaxCommandIndex||command.count<1||command.count>MaxCommandCount)return "Invalid LAN command target, index or count";
            if(!ValidText(command.item)||!ValidText(command.choice))return "Invalid LAN command string";
            return null;
        }
        string OwnershipError(GameCommand command,int player)
        {
            if(command?.actors==null)return null;
            foreach(int actor in command.actors)
            {
                var entity=world.Get(actor);
                // Dead owned actors still reach World.Issue for its ordinary
                // game rejection. Allies and AI remain independently controlled.
                if(entity==null||entity.owner!=player)return "LAN command contains an actor not owned by the bound slot";
            }
            return null;
        }
        static string QueueReferenceError(LanOperation operation,GameCommand command,long expectedQueueId)
        {
            bool cancellation=operation==LanOperation.Command&&command!=null&&command.kind==CommandKind.CancelQueue;
            if(cancellation&&expectedQueueId<=0)return "LAN cancellation requires a stable queue identifier";
            if(!cancellation&&expectedQueueId!=0)return "Unexpected LAN queue identifier";
            return null;
        }
        static string PayloadDigest(LanOperation operation,GameCommand command,long expectedQueueId)
        {
            using(var bytes=new MemoryStream())
            {
                using(var writer=new BinaryWriter(bytes,StrictUtf8,true))
                {
                    writer.Write((int)operation);writer.Write(expectedQueueId);writer.Write(command!=null);
                    if(command!=null)
                    {
                        writer.Write(command.player);writer.Write((int)command.kind);writer.Write((int)command.order);
                        writer.Write(command.point.x);writer.Write(command.point.y);writer.Write(command.target);
                        writer.Write(command.index);writer.Write(command.count);writer.Write(command.append);
                        WriteString(writer,command.item);WriteString(writer,command.choice);
                        writer.Write(command.actors?.Length??-1);if(command.actors!=null)foreach(int actor in command.actors)writer.Write(actor);
                    }
                }
                if(bytes.Length>LanProtocol.MaxCommandBytes)throw new ArgumentException("LAN command byte limit exceeded");
                using(var sha=SHA256.Create())return Convert.ToBase64String(sha.ComputeHash(bytes.ToArray()));
            }
        }
        static void WriteString(BinaryWriter writer,string value)
        {writer.Write(value!=null);if(value!=null)writer.Write(value);}
        static GameCommand CopyCommand(GameCommand command)=>command==null?null:new GameCommand
        {
            player=command.player,kind=command.kind,actors=command.actors==null?null:(int[])command.actors.Clone(),
            order=command.order,point=command.point,target=command.target,index=command.index,count=command.count,
            item=command.item,choice=command.choice,append=command.append
        };
        LanCommandAck NewAck(string peerId,long sequence,int generation,long hostSequence)=>new LanCommandAck
        {matchId=matchId,epoch=epoch,peerId=peerId,sequence=sequence,generation=generation,hostSequence=hostSequence,
            appliedStep=-1,ordinal=-1,stateRevision=lastRevision};
        LanCommandAck Reject(LanCommandRequest request,string reason)=>new LanCommandAck
        {matchId=matchId,epoch=epoch,peerId=SafeIdentifier(request?.peerId),sequence=request?.sequence??0,generation=request?.generation??0,
            terminal=true,protocolRejected=true,reason=reason,appliedStep=-1,ordinal=-1,stateRevision=lastRevision};
        LanCommandAck CompleteRejected(Entry entry,string reason,bool protocolRejected)
        {
            entry.Ack.terminal=true;entry.Ack.reason=reason;entry.Ack.protocolRejected=protocolRejected;Remember(entry);
            return CopyAck(entry.Ack,entry.Peer?.Generation??0);
        }
        static LanCommandAck Conflict(Entry entry,int generation)
        {
            var conflict=CopyAck(entry.Ack,generation);conflict.terminal=true;conflict.accepted=false;
            conflict.protocolRejected=true;conflict.reason="LAN sequence was reused with a different payload";return conflict;
        }
        void Remember(Entry entry)
        {
            var peer=entry.Peer;if(peer==null)return;peer.Results.Enqueue(entry);
            while(peer.Results.Count>LanProtocol.ResultWindow)
            {
                var expired=peer.Results.Dequeue();peer.Entries.Remove(expired.Ack.sequence);
                peer.RetiredThrough=Math.Max(peer.RetiredThrough,expired.Ack.sequence);
            }
        }
        static LanCommandAck CopyAck(LanCommandAck ack,int generation)=>new LanCommandAck
        {matchId=ack.matchId,epoch=ack.epoch,peerId=ack.peerId,reason=ack.reason,sequence=ack.sequence,hostSequence=ack.hostSequence,
            stateRevision=ack.stateRevision,generation=generation,appliedStep=ack.appliedStep,ordinal=ack.ordinal,
            terminal=ack.terminal,accepted=ack.accepted,protocolRejected=ack.protocolRejected};
        long NextHostSequence()
        {if(hostSequence==long.MaxValue)throw new InvalidOperationException("LAN host sequence exhausted");return ++hostSequence;}
        void RequireThread()
        {if(Thread.CurrentThread.ManagedThreadId!=threadId)throw new InvalidOperationException("LAN authority belongs to the simulation thread");}
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
        static bool ValidText(string value)
        {
            if(value==null)return true;if(value.Length>LanProtocol.MaxString)return false;
            try{StrictUtf8.GetByteCount(value);return true;}catch(EncoderFallbackException){return false;}
        }
        static string SafeIdentifier(string value)=>ValidText(value)?value??"":"";
        static void RequireIdentifier(string value,string name)
        {if(string.IsNullOrWhiteSpace(value)||!ValidText(value))throw new ArgumentException("A bounded LAN identifier is required",name);}
        static string BoundReason(string value)
        {
            if(string.IsNullOrEmpty(value))return "";
            int length=Math.Min(value.Length,LanProtocol.MaxString);
            if(length<value.Length&&char.IsHighSurrogate(value[length-1]))length--;
            string bounded=value.Substring(0,length);return ValidText(bounded)?bounded:"LAN command returned an invalid diagnostic";
        }
    }
}
