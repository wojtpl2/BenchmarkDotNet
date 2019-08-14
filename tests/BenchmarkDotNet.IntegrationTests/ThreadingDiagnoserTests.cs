﻿using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Engines;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Portability;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Tests.Loggers;
using BenchmarkDotNet.Toolchains;
using BenchmarkDotNet.Toolchains.DotNetCli;
using BenchmarkDotNet.Toolchains.InProcess.Emit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Xunit;
using Xunit.Abstractions;

namespace BenchmarkDotNet.IntegrationTests
{
    public class ThreadingDiagnoserTests
    {
        private readonly ITestOutputHelper output;

        public ThreadingDiagnoserTests(ITestOutputHelper outputHelper) => output = outputHelper;

        public static IEnumerable<object[]> GetToolchains()
            => !RuntimeInformation.IsNetCore || !NetCoreAppSettings.GetCurrentVersion().Is(TargetFrameworkMoniker.NetCoreApp30) // APIs added in .NET Core 3.0 https://github.com/dotnet/corefx/issues/35500
                ? Array.Empty<object[]>()
                : new[]
                {
                    new object[] { Job.Default.GetToolchain() },
                    new object[] { InProcessEmitToolchain.Instance },
                };

        [Theory, MemberData(nameof(GetToolchains))]
        public void CompletedWorkItemCounIsAccurate(IToolchain toolchain)
        {
            var config = CreateConfig(toolchain);

            var summary = BenchmarkRunner.Run<CompletedWorkItemCount>(config);

            AssertStats(summary, new Dictionary<string, Action<ThreadingStats>>
            {
                { nameof(CompletedWorkItemCount.DoNothing), stats => Assert.Equal(0, stats.CompletedWorkItemCount) },
                { nameof(CompletedWorkItemCount.CompleteOneWorkItem), stats => Assert.Equal(1, stats.CompletedWorkItemCount) }
            });
        }

        public class CompletedWorkItemCount
        {
            [Benchmark]
            public void CompleteOneWorkItem()
            {
                ManualResetEvent done = new ManualResetEvent(false);
                ThreadPool.QueueUserWorkItem(m => (m as ManualResetEvent).Set(), done);
                done.WaitOne();
            }

            [Benchmark]
            public void DoNothing() { }
        }

        [Theory, MemberData(nameof(GetToolchains))]
        public void LockContentionCountIsAccurate(IToolchain toolchain)
        {
            var config = CreateConfig(toolchain);

            var summary = BenchmarkRunner.Run<LockContentionCount>(config);

            AssertStats(summary, new Dictionary<string, Action<ThreadingStats>>
            {
                { nameof(LockContentionCount.DoNothing), stats => Assert.Equal(0, stats.LockContentionCount) },
                { nameof(LockContentionCount.RunIntoLockContention), stats => Assert.Equal(1, stats.LockContentionCount) }
            });
        }

        public class LockContentionCount
        {
            private readonly object guard = new object();

            private ManualResetEvent lockTaken;
            private ManualResetEvent failedToAcquire;

            [Benchmark]
            public void DoNothing() { }

            [Benchmark]
            public void RunIntoLockContention()
            {
                lockTaken = new ManualResetEvent(false);
                failedToAcquire = new ManualResetEvent(false);

                Thread first = new Thread(FirstThread);
                Thread second = new Thread(SecondThread);

                first.Start();
                second.Start();

                second.Join();
                first.Join();
            }

            void FirstThread()
            {
                Monitor.Enter(guard);
                lockTaken.Set();

                failedToAcquire.WaitOne();
                Monitor.Exit(guard);
            }

            void SecondThread()
            {
                lockTaken.WaitOne();

                bool taken = Monitor.TryEnter(guard, TimeSpan.FromMilliseconds(10));

                if (taken)
                {
                    throw new InvalidOperationException("Impossible!");
                }

                failedToAcquire.Set();
            }
        }

        private IConfig CreateConfig(IToolchain toolchain)
            => ManualConfig.CreateEmpty()
                .With(Job.ShortRun
                    .WithEvaluateOverhead(false) // no need to run idle for this test
                    .WithWarmupCount(0) // don't run warmup to save some time for our CI runs
                    .WithIterationCount(1) // single iteration is enough for us
                    .WithGcForce(false)
                    .With(toolchain))
                .With(DefaultColumnProviders.Instance)
                .With(ThreadingDiagnoser.Default)
                .With(toolchain.IsInProcess ? ConsoleLogger.Default : new OutputLogger(output)); // we can't use OutputLogger for the InProcess toolchains because it allocates memory on the same thread

        private void AssertStats(Summary summary, Dictionary<string, Action<ThreadingStats>> assertions)
        {
            foreach (var assertion in assertions)
            {
                var selectedReport = summary.Reports.Single(report => report.BenchmarkCase.DisplayInfo.Contains(assertion.Key));

                assertion.Value(selectedReport.ThreadingStats);
            }
        }
    }
}
