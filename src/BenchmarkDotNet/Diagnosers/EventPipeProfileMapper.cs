﻿using System.Collections.Generic;
using System.Diagnostics.Tracing;
using Microsoft.Diagnostics.NETCore.Client;
using Microsoft.Diagnostics.Tracing.Parsers;

namespace BenchmarkDotNet.Diagnosers
{
    internal sealed class EventPipeProfileMapper
    {
        internal static IReadOnlyDictionary<EventPipeProfile, EventPipeProvider[]> DotNetRuntimeProfiles { get; } =
            new Dictionary<EventPipeProfile, EventPipeProvider[]>
            {
                {
                    EventPipeProfile.CpuSampling,
                    new[]
                    {
                        new EventPipeProvider("Microsoft-DotNETCore-SampleProfiler", EventLevel.Informational),
                        new EventPipeProvider("Microsoft-Windows-DotNETRuntime", EventLevel.Informational, (long) ClrTraceEventParser.Keywords.Default)
                    }
                },
                {
                    EventPipeProfile.GcVerbose,
                    new[]
                    {
                        new EventPipeProvider(
                            name: "Microsoft-Windows-DotNETRuntime",
                            eventLevel: EventLevel.Verbose,
                            keywords: (long) ClrTraceEventParser.Keywords.GC |
                                      (long) ClrTraceEventParser.Keywords.GCHandle |
                                      (long) ClrTraceEventParser.Keywords.Exception
                        ),
                    }
                },
                {
                    EventPipeProfile.GcCollect,
                    new[]
                    {
                        new EventPipeProvider(
                            name: "Microsoft-Windows-DotNETRuntime",
                            eventLevel: EventLevel.Informational,
                            keywords: (long) ClrTraceEventParser.Keywords.GC |
                                      (long) ClrTraceEventParser.Keywords.Exception
                        )
                    }
                },
            };
    }
}
