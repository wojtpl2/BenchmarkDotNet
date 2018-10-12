using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using BenchmarkDotNet.Characteristics;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.Results;
using JetBrains.Annotations;

namespace BenchmarkDotNet.Toolchains.DotNetCli 
{
    public class DotNetCliCommand
    {
        private const string MandatoryUseSharedCompilationFalse = " /p:UseSharedCompilation=false";
        
        [PublicAPI] public string CliPath { get; }
            
        [PublicAPI] public string Arguments { get; }

        [PublicAPI] public GenerateResult GenerateResult { get; }

        [PublicAPI] public ILogger Logger { get; }

        [PublicAPI] public BuildPartition BuildPartition { get; }

        [PublicAPI] public IReadOnlyList<EnvironmentVariable> EnvironmentVariables { get; }
            
        public DotNetCliCommand(string cliPath, string arguments, GenerateResult generateResult, ILogger logger, 
            BuildPartition buildPartition, IReadOnlyList<EnvironmentVariable> environmentVariables)
        {
            CliPath = cliPath;
            Arguments = arguments;
            GenerateResult = generateResult;
            Logger = logger;
            BuildPartition = buildPartition;
            EnvironmentVariables = environmentVariables;
        }
            
        public DotNetCliCommand ExtendArguments(string arguments)
            => new DotNetCliCommand(CliPath, arguments + Arguments, GenerateResult, Logger, BuildPartition, EnvironmentVariables);

        [PublicAPI]
        public BuildResult RestoreThenBuild()
        {
            var restoreResult = Restore();

            if (!restoreResult.IsSuccess)
                return BuildResult.Failure(GenerateResult, new Exception(restoreResult.ProblemDescription));

            return Build().ToBuildResult(GenerateResult);
        }

        [PublicAPI]
        public BuildResult RestoreThenBuildThenPublish()
        {
            var restoreResult = Restore();

            if (!restoreResult.IsSuccess)
                return BuildResult.Failure(GenerateResult, new Exception(restoreResult.ProblemDescription));

            var buildResult = Build();

            if (!buildResult.IsSuccess)
                return BuildResult.Failure(GenerateResult, new Exception(buildResult.ProblemDescription));

            return Publish().ToBuildResult(GenerateResult);
        }

        public DotNetCliCommandResult Restore()
            => DotNetCliCommandExecutor.Execute(
                ExtendArguments(
                    GetRestoreCommand(GenerateResult.ArtifactsPaths, BuildPartition, Arguments)));
        
        public DotNetCliCommandResult Build()
            => DotNetCliCommandExecutor.Execute(
                ExtendArguments(
                    GetBuildCommand(BuildPartition, Arguments)));
        
        public DotNetCliCommandResult Publish()
            => DotNetCliCommandExecutor.Execute(
                ExtendArguments(
                    GetPublishCommand(BuildPartition, Arguments)));
        
        internal static string GetRestoreCommand(ArtifactsPaths artifactsPaths, BuildPartition buildPartition, string extraArguments = null) 
            => new StringBuilder(100)
                .Append("restore ")
                .Append(string.IsNullOrEmpty(artifactsPaths.PackagesDirectoryName) ? string.Empty : $"--packages \"{artifactsPaths.PackagesDirectoryName}\" ")
                .Append(GetCustomMsBuildArguments(buildPartition.RepresentativeBenchmarkCase, buildPartition.Resolver))
                .Append(extraArguments)
                .Append(MandatoryUseSharedCompilationFalse)
                .ToString();
        
        internal static string GetBuildCommand(BuildPartition buildPartition, string extraArguments = null) 
            => new StringBuilder(100)
                .Append($"build -c {buildPartition.BuildConfiguration} ") // we don't need to specify TFM, our auto-generated project contains always single one
                .Append(GetCustomMsBuildArguments(buildPartition.RepresentativeBenchmarkCase, buildPartition.Resolver))
                .Append(extraArguments)
                .Append(MandatoryUseSharedCompilationFalse)
                .ToString();
        
        internal static string GetPublishCommand(BuildPartition buildPartition, string extraArguments = null) 
            => new StringBuilder(100)
                .Append($"publish -c {buildPartition.BuildConfiguration} ") // we don't need to specify TFM, our auto-generated project contains always single one
                .Append(GetCustomMsBuildArguments(buildPartition.RepresentativeBenchmarkCase, buildPartition.Resolver))
                .Append(extraArguments)
                .Append(MandatoryUseSharedCompilationFalse)
                .ToString();

        private static string GetCustomMsBuildArguments(BenchmarkCase benchmarkCase, IResolver resolver)
        {
            if (!benchmarkCase.Job.HasValue(InfrastructureMode.ArgumentsCharacteristic))
                return null;

            var msBuildArguments = benchmarkCase.Job.ResolveValue(InfrastructureMode.ArgumentsCharacteristic, resolver).OfType<MsBuildArgument>();

            return string.Join(" ", msBuildArguments.Select(arg => arg.TextRepresentation));
        }
    }
}