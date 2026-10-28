using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using NUnit.Framework;

namespace Frontier.Tests
{
    public sealed class LanTransport068Tests
    {
        static void Until(Func<bool> predicate,string message,int timeoutMs=5000)
        {
            var clock=Stopwatch.StartNew();bool satisfied=predicate();
            while(!satisfied&&clock.ElapsedMilliseconds<timeoutMs){Thread.Sleep(1);satisfied=predicate();}
            Assert.IsTrue(satisfied,message);
        }
        static LanConnection Accept(LanTcpServer server)
        {
            LanConnection result=null;Until(()=>server.TryAccept(out result),"Loopback server must accept the actual TCP connection");return result;
        }
        static byte[] Receive(LanConnection connection)
        {
            byte[] result=null;Until(()=>connection.TryReceive(out result),"A complete framed payload must arrive");return result;
        }
        static byte[] Header(int length)=>new[]{(byte)(length>>24),(byte)(length>>16),(byte)(length>>8),(byte)length};
        static void RawFrame(NetworkStream stream,byte[] payload)
        {var header=Header(payload.Length);stream.Write(header,0,4);if(payload.Length>0)stream.Write(payload,0,payload.Length);}
        static void Clean(LanConnection connection)
        {if(connection==null)return;connection.Dispose();Until(()=>connection.WorkersTerminated,"Disposal must terminate both LAN I/O workers and any flush deadline");}
        static void Clean(LanTcpServer server)
        {server.Dispose();Until(()=>server.WorkersTerminated,"Disposal must unblock and terminate the accept worker");}

        [Test,Timeout(15000)] public void ActualTcpFramesSurviveSplitHeadersBodiesEmptyFramesAndMultipleMessages()
        {
            var server=new LanTcpServer(0);LanConnection accepted=null;
            try
            {
                using(var raw=new TcpClient(AddressFamily.InterNetwork))
                {
                    raw.Connect(IPAddress.Loopback,server.Port);accepted=Accept(server);var stream=raw.GetStream();
                    var payload=Encoding.UTF8.GetBytes("分帧 loopback payload");var header=Header(payload.Length);
                    for(int i=0;i<4;i++){stream.Write(header,i,1);Thread.Sleep(1);}
                    stream.Write(payload,0,2);stream.Write(payload,2,payload.Length-2);RawFrame(stream,Array.Empty<byte>());RawFrame(stream,new byte[]{7,8,9});
                    CollectionAssert.AreEqual(payload,Receive(accepted));Assert.IsEmpty(Receive(accepted));CollectionAssert.AreEqual(new byte[]{7,8,9},Receive(accepted));
                }
                Until(()=>accepted.WorkersTerminated,"Remote EOF must finish the receiver's disconnect cleanup");Assert.IsFalse(accepted.Connected);StringAssert.Contains("Remote closed",accepted.DisconnectReason);
            }
            finally{Clean(accepted);Clean(server);}
        }

        [Test,Timeout(15000)] public void BidirectionalSendOwnsTheBytesAndReconnectCreatesAnIndependentConnection()
        {
            var server=new LanTcpServer(0);LanConnection client=null,accepted=null,next=null,nextAccepted=null;
            try
            {
                client=LanTransport.ConnectAsync("127.0.0.1",server.Port).GetAwaiter().GetResult();accepted=Accept(server);
                var payload=new byte[]{1,2,3,4};Assert.IsTrue(client.Send(payload));payload[0]=99;
                CollectionAssert.AreEqual(new byte[]{1,2,3,4},Receive(accepted));Assert.IsTrue(accepted.Send(new byte[]{5,6}));CollectionAssert.AreEqual(new byte[]{5,6},Receive(client));
                long previous=client.ConnectionId,remote=accepted.ConnectionId;Clean(client);Until(()=>!accepted.Connected,"Closing one endpoint must reach the other");
                Until(()=>server.ActiveConnectionCount==0,"Disconnected sockets must release the server's connection bound");
                next=LanConnection.ConnectAsync("localhost",server.Port).GetAwaiter().GetResult();nextAccepted=Accept(server);
                Assert.Greater(next.ConnectionId,previous);Assert.Greater(nextAccepted.ConnectionId,remote);Assert.IsFalse(client.Send(new byte[]{0}));
                Assert.IsTrue(next.Send(new byte[]{42}));CollectionAssert.AreEqual(new byte[]{42},Receive(nextAccepted));
            }
            finally{Clean(client);Clean(accepted);Clean(next);Clean(nextAccepted);Clean(server);}
        }

        [TestCase(false)][TestCase(true)] [Timeout(15000)] public void TruncatedHeaderOrBodyIsRejectedWhileEarlierCompleteFramesAreRetained(bool payloadTruncated)
        {
            var server=new LanTcpServer(0);LanConnection accepted=null;
            try
            {
                using(var raw=new TcpClient(AddressFamily.InterNetwork))
                {
                    raw.Connect(IPAddress.Loopback,server.Port);accepted=Accept(server);var stream=raw.GetStream();RawFrame(stream,new byte[]{11,12});
                    if(payloadTruncated){var header=Header(8);stream.Write(header,0,4);stream.Write(new byte[]{1,2},0,2);}
                    else stream.Write(new byte[]{0,0},0,2);
                }
                Until(()=>accepted.WorkersTerminated,"A partial frame must end in a completed disconnection");Assert.IsFalse(accepted.Connected);StringAssert.Contains(payloadTruncated?"payload":"header",accepted.DisconnectReason);
                CollectionAssert.AreEqual(new byte[]{11,12},Receive(accepted));Assert.IsFalse(accepted.TryReceive(out _),"An incomplete frame is never delivered");
            }
            finally{Clean(accepted);Clean(server);}
        }

        [Test,Timeout(15000)] public void OversizeHeaderIsRejectedBeforeReadingOrAllocatingTheAnnouncedBody()
        {
            var server=new LanTcpServer(0);LanConnection accepted=null;
            try
            {
                using(var raw=new TcpClient(AddressFamily.InterNetwork))
                {
                    raw.Connect(IPAddress.Loopback,server.Port);accepted=Accept(server);var header=Header(LanConnection.MaxFrameBytes+1);raw.GetStream().Write(header,0,4);
                    Until(()=>accepted.WorkersTerminated,"Only a malicious header must be enough to reject and close an oversize frame");Assert.IsFalse(accepted.Connected);StringAssert.Contains("exceeds 8 MiB",accepted.DisconnectReason);
                    Assert.AreEqual(0,accepted.QueuedIncomingFrames);Assert.AreEqual(0,accepted.QueuedIncomingBytes);
                }
            }
            finally{Clean(accepted);Clean(server);}
        }

        [Test,Timeout(15000)] public void EmptyFramesStillConsumeThe128FrameInboxBound()
        {
            var server=new LanTcpServer(0);LanConnection accepted=null;
            try
            {
                using(var raw=new TcpClient(AddressFamily.InterNetwork))
                {
                    raw.Connect(IPAddress.Loopback,server.Port);accepted=Accept(server);var stream=raw.GetStream();
                    var headers=new byte[LanConnection.MaxQueuedFrames*4];stream.Write(headers,0,headers.Length);
                    Until(()=>accepted.QueuedIncomingFrames==LanConnection.MaxQueuedFrames,"Exactly 128 empty messages should fit");Assert.IsTrue(accepted.Connected);Assert.AreEqual(0,accepted.QueuedIncomingBytes);
                    stream.Write(new byte[4],0,4);Until(()=>accepted.WorkersTerminated,"A 129th unread empty message must close and finish cleanup instead of growing the queue");Assert.IsFalse(accepted.Connected);
                    StringAssert.Contains("Incoming LAN queue limit",accepted.DisconnectReason);Assert.AreEqual(0,accepted.QueuedIncomingFrames);
                }
            }
            finally{Clean(accepted);Clean(server);}
        }

        [Test,Timeout(20000)] public void InboxHasASeparate16MiBByteBoundAndRejectsTheNextBodyAtItsHeader()
        {
            var server=new LanTcpServer(0);LanConnection accepted=null;
            try
            {
                using(var raw=new TcpClient(AddressFamily.InterNetwork))
                {
                    raw.SendTimeout=5000;raw.Connect(IPAddress.Loopback,server.Port);accepted=Accept(server);var stream=raw.GetStream();var payload=new byte[LanConnection.MaxFrameBytes];
                    RawFrame(stream,payload);RawFrame(stream,payload);Until(()=>accepted.QueuedIncomingBytes==LanConnection.MaxQueuedBytes,"Two maximum frames should fit exactly");
                    Assert.AreEqual(2,accepted.QueuedIncomingFrames);Assert.IsTrue(accepted.Connected);
                    var header=Header(1);stream.Write(header,0,4);Until(()=>!accepted.Connected,"The byte bound must be enforced without waiting for the overflowing body");
                    // Connected goes false as soon as Stop wins its CAS. Keep
                    // the raw peer open and supply no body: the read worker must
                    // finish its own cleanup before asserting exact queue state.
                    Until(()=>accepted.WorkersTerminated,"Header-only overflow must terminate I/O and finish clearing the inbox while the raw peer remains open");
                    StringAssert.Contains("Incoming LAN queue limit",accepted.DisconnectReason);Assert.AreEqual(0,accepted.QueuedIncomingBytes);
                    Assert.AreEqual(0,accepted.QueuedIncomingFrames);Assert.IsFalse(accepted.TryReceive(out _));
                }
            }
            finally{Clean(accepted);Clean(server);}
        }

        [Test,Timeout(15000)] public void UnreadPeerCannotBlockSendAndOutboxOverflowHasAnExplicitReason()
        {
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();LanConnection sender=null;TcpClient unread=null;
            try
            {
                sender=LanConnection.ConnectAsync("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port).GetAwaiter().GetResult();unread=listener.AcceptTcpClient();unread.ReceiveBufferSize=4096;
                var payload=new byte[1024*1024];var clock=Stopwatch.StartNew();bool overflow=false;
                for(int i=0;i<128;i++){if(!sender.Send(payload)){overflow=true;break;}}
                Assert.IsTrue(overflow,"A peer that never reads must eventually hit a bounded outgoing queue");Assert.Less(clock.ElapsedMilliseconds,5000,"Send only enqueues; it cannot wait for the 10-second socket write timeout");
                Until(()=>sender.WorkersTerminated,"Overflow closes a blocked socket write and read");
                StringAssert.Contains("Outgoing LAN queue limit",sender.DisconnectReason);Assert.AreEqual(0,sender.QueuedOutgoingBytes);Assert.AreEqual(0,sender.QueuedOutgoingFrames);
            }
            finally{unread?.Close();Clean(sender);listener.Stop();}
        }

        [Test,Timeout(15000)] public void CloseAfterFlushDeliversTheFinalLeaveBeforeEofAndTerminatesAllWorkers()
        {
            var server=new LanTcpServer(0);LanConnection sender=null,receiver=null;
            try
            {
                sender=LanConnection.ConnectAsync("127.0.0.1",server.Port).GetAwaiter().GetResult();receiver=Accept(server);var last=Encoding.UTF8.GetBytes("{\"kind\":\"leave\"}");
                Assert.IsTrue(sender.Send(new byte[]{10}));Assert.IsTrue(sender.Send(last));var clock=Stopwatch.StartNew();sender.CloseAfterFlush("Controlled Leave");
                Assert.Less(clock.ElapsedMilliseconds,1000,"Graceful flush schedules work and must not block the calling thread");Assert.IsFalse(sender.Send(new byte[]{99}));
                Until(()=>!receiver.Connected,"The flushed connection must reach EOF");CollectionAssert.AreEqual(new byte[]{10},Receive(receiver));CollectionAssert.AreEqual(last,Receive(receiver));
                Assert.IsFalse(receiver.TryReceive(out _));Until(()=>sender.WorkersTerminated&&receiver.WorkersTerminated,"Graceful close must finish workers and cancel its deadline");
                Assert.AreEqual("Controlled Leave",sender.DisconnectReason);
            }
            finally{Clean(sender);Clean(receiver);Clean(server);}
        }

        [Test,Timeout(15000)] public void GracefulCloseDeadlineClosesABlockedWriterWithoutBlockingTheCaller()
        {
            var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();LanConnection sender=null;TcpClient unread=null;
            try
            {
                sender=LanConnection.ConnectAsync("127.0.0.1",((IPEndPoint)listener.LocalEndpoint).Port).GetAwaiter().GetResult();unread=listener.AcceptTcpClient();unread.ReceiveBufferSize=4096;
                Assert.IsTrue(sender.Send(new byte[LanConnection.MaxFrameBytes]));var clock=Stopwatch.StartNew();sender.CloseAfterFlush("Controlled blocked flush",50);
                Assert.Less(clock.ElapsedMilliseconds,1000);Assert.IsFalse(sender.Send(new byte[]{1}));Until(()=>sender.WorkersTerminated,"Flush deadline must close unread sockets and end all workers");
                StringAssert.Contains("deadline",sender.DisconnectReason);Assert.IsFalse(sender.Connected);
            }
            finally{unread?.Close();Clean(sender);listener.Stop();}
        }

        [Test,Timeout(15000)] public void ServerSocketLimitRejectsExcessConnectionsAndDisposalAlsoClosesClaimedSockets()
        {
            var server=new LanTcpServer(0,1);LanConnection first=null,accepted=null,excess=null;
            try
            {
                first=LanConnection.ConnectAsync("127.0.0.1",server.Port).GetAwaiter().GetResult();accepted=Accept(server);excess=LanConnection.ConnectAsync("127.0.0.1",server.Port).GetAwaiter().GetResult();
                Until(()=>!excess.Connected,"An excess real TCP connection must be closed by the bounded server");Assert.AreEqual(1,server.ActiveConnectionCount);StringAssert.Contains("socket limit",server.LastError);
                server.Dispose();Until(()=>!first.Connected&&accepted.WorkersTerminated&&server.WorkersTerminated,"Server disposal owns accepted sockets too");Assert.AreEqual(0,server.ActiveConnectionCount);
            }
            finally{Clean(first);Clean(accepted);Clean(excess);Clean(server);}
        }

        [Test,Timeout(15000)] public void PreCancelledConnectNeverCreatesASessionAndInvalidArgumentsAreRejected()
        {
            var server=new LanTcpServer(0);
            try
            {
                using(var cancelled=new CancellationTokenSource())
                {cancelled.Cancel();Assert.Catch<OperationCanceledException>(()=>LanConnection.ConnectAsync("localhost",server.Port,1000,cancelled.Token).GetAwaiter().GetResult());}
                Assert.AreEqual(0,server.ActiveConnectionCount);Assert.Throws<ArgumentException>(()=>LanConnection.ConnectAsync("",server.Port));
                Assert.Throws<ArgumentOutOfRangeException>(()=>LanConnection.ConnectAsync("localhost",0));Assert.Throws<ArgumentOutOfRangeException>(()=>LanConnection.ConnectAsync("localhost",server.Port,0));
            }
            finally{Clean(server);}
        }
    }
}
