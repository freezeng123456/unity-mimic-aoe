using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Frontier
{
    // Wire I/O owns only detached bytes. No callback here may touch Unity,
    // JsonUtility, a World, a Recorder or a controller.
    public static class LanTransport
    {
        public static Task<LanConnection> ConnectAsync(string host,int port,int timeoutMs=5000,CancellationToken cancellationToken=default)
            =>LanConnection.ConnectAsync(host,port,timeoutMs,cancellationToken);
    }

    public sealed class LanConnection : IDisposable
    {
        public const int MaxFrameBytes=8*1024*1024,MaxQueuedFrames=128,MaxQueuedBytes=16*1024*1024;
        static long nextConnectionId;
        readonly TcpClient client;readonly NetworkStream stream;
        readonly object incomingGate=new object(),outgoingGate=new object();
        readonly Queue<byte[]> incoming=new Queue<byte[]>(),outgoing=new Queue<byte[]>();
        readonly Thread reader,writer;
        int stopped,stopCompleted,readerCompleted,writerCompleted,incomingBytes,outgoingBytes,closing,flushWorkerCompleted=1;
        string disconnectReason="";
        string flushReason="";CancellationTokenSource flushCancellation;
        internal event Action<LanConnection> Closed;
        public long ConnectionId {get;}
        // Disconnecting prevents further work immediately. It does not imply
        // that the asynchronous socket/queue cleanup has finished yet.
        public bool Connected=>Volatile.Read(ref stopped)==0;
        public string DisconnectReason=>Volatile.Read(ref disconnectReason);
        public bool WorkersTerminated=>Volatile.Read(ref stopCompleted)!=0&&Volatile.Read(ref readerCompleted)!=0&&Volatile.Read(ref writerCompleted)!=0&&Volatile.Read(ref flushWorkerCompleted)!=0;
        public int QueuedIncomingFrames {get{lock(incomingGate)return incoming.Count;}}
        public int QueuedOutgoingFrames {get{lock(outgoingGate)return outgoing.Count;}}
        public int QueuedIncomingBytes {get{lock(incomingGate)return incomingBytes;}}
        public int QueuedOutgoingBytes {get{lock(outgoingGate)return outgoingBytes;}}

        internal LanConnection(TcpClient client)
        {
            this.client=client??throw new ArgumentNullException(nameof(client));
            client.NoDelay=true;client.SendTimeout=10000;stream=client.GetStream();
            ConnectionId=Interlocked.Increment(ref nextConnectionId);
            reader=new Thread(ReadLoop){IsBackground=true,Name="Frontier LAN reader "+ConnectionId};
            writer=new Thread(WriteLoop){IsBackground=true,Name="Frontier LAN writer "+ConnectionId};
            bool readerStarted=false,writerStarted=false;
            try{reader.Start();readerStarted=true;writer.Start();writerStarted=true;}
            catch
            {
                if(!readerStarted)Volatile.Write(ref readerCompleted,1);
                if(!writerStarted)Volatile.Write(ref writerCompleted,1);
                Stop("Could not start LAN I/O workers");throw;
            }
        }

        public static Task<LanConnection> ConnectAsync(string host,int port,int timeoutMs=5000,CancellationToken cancellationToken=default)
        {
            if(string.IsNullOrWhiteSpace(host)||host.Length>253)throw new ArgumentException("A LAN host name or IPv4 address is required",nameof(host));
            if(port<1||port>65535)throw new ArgumentOutOfRangeException(nameof(port));
            if(timeoutMs<1||timeoutMs>120000)throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            // Mono's resolver can do synchronous work before yielding its Task.
            // Even that phase therefore starts outside Unity's main thread.
            return Task.Run(()=>ConnectWorker(host.Trim(),port,timeoutMs,cancellationToken),cancellationToken);
        }
        static async Task<LanConnection> ConnectWorker(string host,int port,int timeoutMs,CancellationToken token)
        {
            var socket=new TcpClient(AddressFamily.InterNetwork);Task connecting=null;
            using(var deadline=CancellationTokenSource.CreateLinkedTokenSource(token))
            using(token.Register(()=>CloseSocket(socket)))
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    var timer=Task.Delay(timeoutMs,deadline.Token);
                    // Start the deadline before resolving the host. Some Mono
                    // resolver implementations block inside ConnectAsync before
                    // returning its Task; that cannot delay our caller's timeout.
                    connecting=Task.Run(()=>socket.ConnectAsync(host,port));
                    if(await Task.WhenAny(connecting,timer).ConfigureAwait(false)!=connecting)
                    {token.ThrowIfCancellationRequested();throw new TimeoutException("LAN connection timed out");}
                    await connecting.ConfigureAwait(false);token.ThrowIfCancellationRequested();
                    deadline.Cancel();var connection=new LanConnection(socket);
                    if(token.IsCancellationRequested){connection.Dispose();token.ThrowIfCancellationRequested();}
                    return connection;
                }
                catch
                {
                    CloseSocket(socket);
                    // A platform DNS lookup may finish after caller cancellation.
                    // Observe its fault and never publish a late socket/session.
                    if(connecting!=null)_=connecting.ContinueWith(t=>{var ignored=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
                    throw;
                }
                finally{deadline.Cancel();}
            }
        }
        internal static void CloseSocket(TcpClient socket)
        {try{socket.Close();}catch(SocketException){}catch(ObjectDisposedException){}}

        // Send never performs a socket write or waits for the peer. Each queued
        // payload is copied; subsequent caller mutation cannot alter the wire.
        public bool Send(byte[] payload)
        {
            if(payload==null)throw new ArgumentNullException(nameof(payload));
            bool overflow=false,oversize=false;
            lock(outgoingGate)
            {
                if(!Connected||closing!=0)return false;
                if(payload.Length>MaxFrameBytes)oversize=true;
                else if(outgoing.Count>=MaxQueuedFrames||payload.Length>MaxQueuedBytes-outgoingBytes)overflow=true;
                else
                {
                    var copy=(byte[])payload.Clone();outgoing.Enqueue(copy);outgoingBytes+=copy.Length;
                    Monitor.Pulse(outgoingGate);return true;
                }
            }
            if(oversize)Stop("Outgoing LAN frame exceeds 8 MiB");else if(overflow)Stop("Outgoing LAN queue limit exceeded");return false;
        }
        public bool TryReceive(out byte[] payload)
        {
            lock(incomingGate)
            {
                if(incoming.Count==0){payload=null;return false;}
                payload=incoming.Dequeue();incomingBytes-=payload.Length;return true;
            }
        }
        // An explicit Leave/error can enqueue its final frame then close without
        // waiting on Unity's thread. The worker drains it; an unread peer cannot
        // keep the socket alive beyond the bounded deadline.
        public void CloseAfterFlush(string reason,int timeoutMs=1000)
        {
            if(timeoutMs<1||timeoutMs>10000)throw new ArgumentOutOfRangeException(nameof(timeoutMs));
            CancellationTokenSource deadline;
            lock(outgoingGate)
            {
                if(!Connected||closing!=0)return;
                closing=1;flushReason=SafeCloseReason(reason);deadline=new CancellationTokenSource();
                flushCancellation=deadline;Volatile.Write(ref flushWorkerCompleted,0);
                Monitor.PulseAll(outgoingGate);
            }
            // All task outcomes are consumed here. Stop cancels this delay on
            // successful flush/disposal, and the task owns its CTS lifetime.
            var flushTask=Task.Run(async()=>
            {
                try{await Task.Delay(timeoutMs,deadline.Token).ConfigureAwait(false);Stop("LAN flush close deadline exceeded");}
                catch(OperationCanceledException){}
                catch(Exception){Stop("LAN flush close failed");}
                finally
                {
                    Interlocked.CompareExchange(ref flushCancellation,null,deadline);
                    deadline.Dispose();Volatile.Write(ref flushWorkerCompleted,1);
                }
            });
            _=flushTask.ContinueWith(t=>{var ignored=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);
        }
        static string SafeCloseReason(string reason)
        {
            if(string.IsNullOrWhiteSpace(reason))return "LAN connection closed after flush";
            // This is a trusted caller's diagnostic, never a received payload,
            // host/credential/resume-token echo. Keep it single-line and bounded.
            reason=reason.Replace('\r',' ').Replace('\n',' ');return reason.Length<=256?reason:reason.Substring(0,256);
        }
        bool ReadExact(byte[] bytes,int length,string phase)
        {
            int read=0;
            while(read<length&&Connected)
            {
                int count=stream.Read(bytes,read,length-read);
                if(count==0){Stop(read==0&&phase=="header"?"Remote closed LAN connection":"Remote closed during LAN frame "+phase,true);return false;}
                read+=count;
            }
            return Connected&&read==length;
        }
        void ReadLoop()
        {
            try
            {
                var header=new byte[4];
                while(Connected)
                {
                    if(!ReadExact(header,4,"header"))break;
                    uint length=((uint)header[0]<<24)|((uint)header[1]<<16)|((uint)header[2]<<8)|header[3];
                    if(length>MaxFrameBytes){Stop("Incoming LAN frame exceeds 8 MiB");break;}
                    bool noCapacity;
                    lock(incomingGate)noCapacity=incoming.Count>=MaxQueuedFrames||length>MaxQueuedBytes-incomingBytes;
                    if(noCapacity){Stop("Incoming LAN queue limit exceeded");break;}
                    var payload=new byte[(int)length];if(!ReadExact(payload,payload.Length,"payload"))break;
                    bool overflow;
                    lock(incomingGate)
                    {
                        if(!Connected)break;
                        overflow=incoming.Count>=MaxQueuedFrames||payload.Length>MaxQueuedBytes-incomingBytes;
                        if(!overflow){incoming.Enqueue(payload);incomingBytes+=payload.Length;}
                    }
                    if(overflow){Stop("Incoming LAN queue limit exceeded");break;}
                }
            }
            catch(Exception error)when(error is System.IO.IOException||error is SocketException||error is ObjectDisposedException)
            {Stop("LAN read failed: "+error.Message);}
            finally{Volatile.Write(ref readerCompleted,1);}
        }
        void WriteLoop()
        {
            try
            {
                while(Connected)
                {
                    byte[] payload;string finishReason=null;
                    lock(outgoingGate)
                    {
                        while(Connected&&outgoing.Count==0&&closing==0)Monitor.Wait(outgoingGate);
                        if(!Connected)break;
                        if(outgoing.Count==0){payload=null;finishReason=flushReason;}
                        else{payload=outgoing.Dequeue();outgoingBytes-=payload.Length;}
                    }
                    if(payload==null){Stop(finishReason,true);break;}
                    int length=payload.Length;
                    var header=new[]{(byte)(length>>24),(byte)(length>>16),(byte)(length>>8),(byte)length};
                    stream.Write(header,0,4);if(length!=0)stream.Write(payload,0,length);
                }
            }
            catch(Exception error)when(error is System.IO.IOException||error is SocketException||error is ObjectDisposedException)
            {Stop("LAN write failed: "+error.Message);}
            finally{Volatile.Write(ref writerCompleted,1);}
        }
        void Stop(string reason,bool preserveReceived=false)
        {
            if(Interlocked.CompareExchange(ref stopped,1,0)!=0)return;
            try
            {
                Volatile.Write(ref disconnectReason,reason);
                CloseSocket(client);
                var deadline=Volatile.Read(ref flushCancellation);
                if(deadline!=null)try{deadline.Cancel();}catch(ObjectDisposedException){}
                // A complete ACK/Leave preceding an orderly EOF still belongs to
                // the receiver. Malformed/overflow frames and explicit disposal
                // clear the bounded inbox; an incomplete frame is never enqueued.
                if(!preserveReceived)lock(incomingGate){incoming.Clear();incomingBytes=0;}
                lock(outgoingGate){outgoing.Clear();outgoingBytes=0;Monitor.PulseAll(outgoingGate);}
                Closed?.Invoke(this);
            }
            finally{Volatile.Write(ref stopCompleted,1);}
        }
        // Closing the socket interrupts blocked read/write; waking the queue
        // interrupts an idle writer. Dispose deliberately does not join on the
        // Unity main thread. WorkersTerminated exposes eventual cleanup.
        public void Dispose()
        {
            Stop("LAN connection disposed");
            lock(incomingGate){incoming.Clear();incomingBytes=0;}
        }
    }

    public sealed class LanTcpServer : IDisposable
    {
        public const int DefaultMaxConnections=4;
        readonly TcpListener listener;readonly Thread accepting;readonly object gate=new object();
        readonly Queue<LanConnection> pending=new Queue<LanConnection>();
        readonly HashSet<LanConnection> connections=new HashSet<LanConnection>();
        readonly int maxConnections;int stopped,acceptCompleted;string lastError="";
        public int Port {get;}
        public string LastError=>Volatile.Read(ref lastError);
        public bool WorkersTerminated=>Volatile.Read(ref acceptCompleted)!=0;
        public int ActiveConnectionCount {get{lock(gate)return connections.Count;}}
        public int PendingAcceptCount {get{lock(gate)return pending.Count;}}
        public LanTcpServer(int port,int maxConnections=DefaultMaxConnections)
        {
            if(port<0||port>65535)throw new ArgumentOutOfRangeException(nameof(port));
            if(maxConnections<1||maxConnections>16)throw new ArgumentOutOfRangeException(nameof(maxConnections));
            this.maxConnections=maxConnections;listener=new TcpListener(IPAddress.Any,port);
            listener.Start(maxConnections*2);Port=((IPEndPoint)listener.LocalEndpoint).Port;
            accepting=new Thread(AcceptLoop){IsBackground=true,Name="Frontier LAN accept "+Port};
            try{accepting.Start();}catch{listener.Stop();Volatile.Write(ref acceptCompleted,1);throw;}
        }
        public bool TryAccept(out LanConnection connection)
        {
            lock(gate)
            {
                if(Volatile.Read(ref stopped)!=0||pending.Count==0){connection=null;return false;}
                connection=pending.Dequeue();return true;
            }
        }
        void PrunePending()
        {
            int count=pending.Count;
            while(count-->0){var connection=pending.Dequeue();if(connection.Connected)pending.Enqueue(connection);}
        }
        void ConnectionClosed(LanConnection connection)
        {lock(gate){connections.Remove(connection);PrunePending();}}
        void AcceptLoop()
        {
            try
            {
                while(Volatile.Read(ref stopped)==0)
                {
                    var client=listener.AcceptTcpClient();
                    lock(gate)
                    {
                        PrunePending();
                        if(Volatile.Read(ref stopped)!=0){LanConnection.CloseSocket(client);break;}
                        if(connections.Count>=maxConnections||pending.Count>=maxConnections)
                        {Volatile.Write(ref lastError,"LAN socket limit reached");LanConnection.CloseSocket(client);continue;}
                        LanConnection connection;
                        try{connection=new LanConnection(client);}
                        catch{LanConnection.CloseSocket(client);throw;}
                        connection.Closed+=ConnectionClosed;
                        if(connection.Connected){connections.Add(connection);pending.Enqueue(connection);}
                        else connection.Closed-=ConnectionClosed;
                    }
                }
            }
            catch(Exception error)when(error is SocketException||error is ObjectDisposedException||error is System.IO.IOException)
            {if(Volatile.Read(ref stopped)==0){Volatile.Write(ref lastError,"LAN accept failed: "+error.Message);Dispose();}}
            finally{Volatile.Write(ref acceptCompleted,1);}
        }
        public void Dispose()
        {
            if(Interlocked.Exchange(ref stopped,1)!=0)return;
            listener.Stop();LanConnection[] owned;
            lock(gate)
            {
                owned=new LanConnection[connections.Count];connections.CopyTo(owned);connections.Clear();pending.Clear();
                foreach(var connection in owned)connection.Closed-=ConnectionClosed;
            }
            foreach(var connection in owned)connection.Dispose();
        }
    }
}
