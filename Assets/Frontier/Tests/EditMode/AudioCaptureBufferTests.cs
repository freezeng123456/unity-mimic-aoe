using System;
using System.IO;
using System.Threading;
using NUnit.Framework;

namespace Frontier.Tests
{
    public sealed class AudioCaptureBufferTests
    {
        [Test] public void StereoFramesAreBoundedAndTheLiveInputIsUnmodified()
        {
            var input = new[] { .25f, -.25f, .5f, -.5f, .75f, -.75f, 1f, -1f, .125f, -.125f };
            var original = (float[])input.Clone();
            var buffer = new NativePcmCaptureBuffer(8000, 4);
            buffer.Append(input, 2);
            Assert.IsTrue(buffer.Complete);
            var captured = buffer.Stop();
            Assert.AreEqual(4, captured.Frames); Assert.AreEqual(2, captured.Channels); Assert.AreEqual(8, captured.Count);
            Assert.AreEqual(0, captured.DiscardedSamples); Assert.AreEqual(2, captured.SamplesExcludedAtDurationLimit);
            CollectionAssert.AreEqual(original, input, "Capturing the listener must never alter the playing mix");
            for (int i = 0; i < captured.Count; i++) Assert.AreEqual(original[i], captured.Samples[i]);
            buffer.Append(new float[] { -1, -1 }, 2);
            Assert.AreEqual(4, buffer.Stop().Frames, "Data outside the capture window cannot be appended");
        }

        [Test] public void FormatChangesAreRejectedWithoutMixingLayouts()
        {
            var buffer = new NativePcmCaptureBuffer(8000, 20);
            buffer.Append(new[] { .1f, .2f, .3f, .4f }, 2);
            buffer.Append(new[] { .7f, .8f, .9f }, 1);
            Assert.IsTrue(buffer.Complete);
            var captured = buffer.Stop();
            Assert.AreEqual(2, captured.Channels); Assert.AreEqual(2, captured.Frames);
            Assert.AreEqual(1, captured.FormatErrors); Assert.AreEqual(3, captured.DiscardedSamples);
            buffer.Append(new[] { .9f, .9f }, 2);
            Assert.AreEqual(2, buffer.Stop().Frames, "A rejected format must stop this session rather than silently resume");
        }

        [Test] public void ConcurrentStopProducesStableSamplesAndNoLaterWrites()
        {
            var buffer = new NativePcmCaptureBuffer(8000, 16000);
            var block = new float[128]; for (int i = 0; i < block.Length; i++) block[i] = i % 2 == 0 ? .25f : -.5f;
            using (var started = new ManualResetEvent(false))
            {
                var producer = new Thread(() => { buffer.Append(block, 2); started.Set(); for (int i = 0; i < 2000; i++) buffer.Append(block, 2); });
                producer.Start(); Assert.IsTrue(started.WaitOne(5000));
                var captured = buffer.Stop(); var retained = new float[captured.Count]; Array.Copy(captured.Samples, retained, retained.Length);
                Assert.IsTrue(producer.Join(5000));
                Assert.Greater(captured.Count, 0); Assert.LessOrEqual(captured.Frames, 16000);
                for (int i = 0; i < retained.Length; i++) Assert.AreEqual(retained[i], captured.Samples[i]);
                Assert.AreEqual(captured.Frames, buffer.Stop().Frames);
            }
        }

        [Test] public void WaveHeaderPreservesRateChannelsAndInterleavingWhileMetricsRetainClipping()
        {
            var buffer = new NativePcmCaptureBuffer(8000, 4);
            buffer.Append(new[] { .5f, -.5f, 1.5f, -1.5f, float.NaN, float.PositiveInfinity }, 2);
            var captured = buffer.Stop(); var metrics = new NativeAudioReceipt(); NativeAudioCapture.CalculateMetrics(captured, metrics);
            Assert.AreEqual(1.5f, metrics.peak); Assert.AreEqual(2, metrics.clippedSamples); Assert.AreEqual(2, metrics.nonfiniteSamples);
            Assert.That(metrics.rms, Is.EqualTo(Math.Sqrt(1.25)).Within(.0001));
            using (var bytes = new MemoryStream())
            {
                NativeAudioCapture.WriteWave(bytes, captured); Assert.AreEqual(44 + captured.Count * 2, bytes.Length);
                bytes.Position = 20;
                using (var read = new BinaryReader(bytes, System.Text.Encoding.ASCII, true))
                {
                    Assert.AreEqual(1, read.ReadInt16()); Assert.AreEqual(2, read.ReadInt16()); Assert.AreEqual(8000, read.ReadInt32());
                    Assert.AreEqual(32000, read.ReadInt32()); Assert.AreEqual(4, read.ReadInt16()); Assert.AreEqual(16, read.ReadInt16());
                    bytes.Position = 40; Assert.AreEqual(12, read.ReadInt32());
                    Assert.AreEqual(16384, read.ReadInt16()); Assert.AreEqual(-16384, read.ReadInt16());
                    Assert.AreEqual(32767, read.ReadInt16()); Assert.AreEqual(-32767, read.ReadInt16());
                    Assert.AreEqual(0, read.ReadInt16()); Assert.AreEqual(0, read.ReadInt16());
                }
            }
            Assert.AreEqual(1.5f, captured.Samples[2], "Encoding clamps its output, not the original listener samples");
            Assert.IsTrue(float.IsNaN(captured.Samples[4]));
        }

        [Test] public void EmptyAndMalformedBlocksCannotInventAudioFrames()
        {
            var buffer = new NativePcmCaptureBuffer(8000, 4);
            buffer.Append(new float[0], 2); buffer.Append(new[] { .5f, -.5f, .75f }, 2);
            var captured = buffer.Stop(); Assert.AreEqual(1, captured.Frames); Assert.AreEqual(1, captured.DiscardedSamples);
            var empty = new NativePcmCaptureBuffer(8000, 4).Stop();
            using (var bytes = new MemoryStream()) { NativeAudioCapture.WriteWave(bytes, empty); Assert.AreEqual(44, bytes.Length); }
            Assert.AreEqual(0, empty.Count);
        }

        [Test] public void ClippingNonfiniteFormatChangeAndEmptyDataCannotBeReportedAsUsableSignal()
        {
            foreach (float[] input in new[] { new[] { 1.1f, -.2f }, new[] { float.NaN, .1f }, new[] { float.NegativeInfinity, .1f } })
            {
                var buffer = new NativePcmCaptureBuffer(8000, 4); buffer.Append(input, 1);
                var receipt = new NativeAudioReceipt(); NativeAudioCapture.PopulatePcmReceipt(buffer.Stop(), receipt);
                Assert.IsTrue(receipt.hasDspData); Assert.IsTrue(receipt.validFormat);
                Assert.IsFalse(receipt.validSignal, "A legal channel layout is not proof of a finite unclipped mix");
            }
            var clean = new NativePcmCaptureBuffer(8000, 4); clean.Append(new[] { .1f, -.1f }, 1);
            var changed = new NativeAudioReceipt(); NativeAudioCapture.PopulatePcmReceipt(clean.Stop(), changed, false);
            Assert.IsFalse(changed.validFormat); Assert.IsFalse(changed.validSignal);
            var empty = new NativeAudioReceipt(); NativeAudioCapture.PopulatePcmReceipt(new NativePcmCaptureBuffer(8000, 4).Stop(), empty);
            Assert.IsFalse(empty.hasDspData); Assert.IsFalse(empty.validFormat); Assert.IsFalse(empty.validSignal);
        }
    }
}
