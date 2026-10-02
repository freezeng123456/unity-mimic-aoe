using NUnit.Framework;

namespace Frontier.Tests
{
    public sealed class PerformanceCaptureTests
    {
        [Test] public void PausesFocusLossAndInvalidIntervalsRemainRawButAreExcludedFromStatistics()
        {
            var raw = new[]
            {
                new PerformanceFrame { frameMs = 10, focused = true, eligibleInterval = true },
                new PerformanceFrame { frameMs = 20, focused = true, eligibleInterval = true },
                new PerformanceFrame { frameMs = 900, focused = true, paused = true, eligibleInterval = true },
                new PerformanceFrame { frameMs = 800, focused = false, eligibleInterval = true },
                new PerformanceFrame { frameMs = float.NaN, focused = true, eligibleInterval = true },
                new PerformanceFrame { frameMs = float.PositiveInfinity, focused = true, eligibleInterval = true },
                new PerformanceFrame { frameMs = 0, focused = true, eligibleInterval = true },
                new PerformanceFrame { frameMs = 700, focused = true, pauseChangedInInterval = true },
                new PerformanceFrame { frameMs = 600, focused = true, focusLostInInterval = true }
            };
            var report = new PerformanceCaptureReport { frames = raw, totalFrames = raw.Length };
            NativePerformanceCapture.Summarize(report);
            Assert.That(report.includedFrames, Is.EqualTo(2));
            Assert.That(report.excludedFrames, Is.EqualTo(7));
            Assert.That(report.meanFrameMs, Is.EqualTo(15));
            Assert.That(report.allFramesFocused, Is.False);
            Assert.That(report.frames, Is.SameAs(raw));
            Assert.That(raw[2].frameMs, Is.EqualTo(900));
        }

        [Test] public void EmptyEligibleSubsetHasFiniteZeroMetrics()
        {
            var report = new PerformanceCaptureReport
            { frames = new[] { new PerformanceFrame { frameMs = 1000, focused = true, paused = true } }, totalFrames = 1 };
            NativePerformanceCapture.Summarize(report);
            Assert.That(report.includedFrames, Is.Zero);
            Assert.That(report.excludedFrames, Is.EqualTo(1));
            Assert.That(report.meanFrameMs, Is.Zero);
            Assert.That(report.p95FrameMs, Is.Zero);
            Assert.That(report.p99FrameMs, Is.Zero);
        }
    }
}
