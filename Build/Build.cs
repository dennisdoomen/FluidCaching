using System;
using System.Diagnostics;
using System.Linq;
using Fallout.Common;
using Fallout.Common.CI.GitHubActions;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.Coverlet;
using Fallout.Common.Tools.DotNet;
using Fallout.Common.Tools.GitVersion;
using Fallout.Common.Tools.ReportGenerator;
using Fallout.Common.Utilities;
using Fallout.Common.Utilities.Collections;
using Fallout.Solutions;
using static Fallout.Common.Tools.DotNet.DotNetTasks;
using static Fallout.Common.Tools.ReportGenerator.ReportGeneratorTasks;
using static Serilog.Log;

class Build : FalloutBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode
    public static int Main() => Execute<Build>(x => x.Default);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;

    GitHubActions GitHubActions => GitHubActions.Instance;

    string BranchSpec => GitHubActions?.Ref;

    string BuildNumber => GitHubActions?.RunNumber.ToString();

    [Parameter(
        "Set the URI specifying the location of the NuGet artifacts API, which is used to publish packages generated during the build process.")]
    readonly string NugetArtifactsApiUri = "https://api.nuget.org/v3/index.json";

    [Parameter("The API key used to authenticate and authorize access to the NuGet artifacts API.")]
    [Secret]
    readonly string NugetArtifactsApiKey;

    [Parameter("The key to use for scanning packages on GitHub")]
    [Secret]
    readonly string GitHubApiKey;

    [Solution(GenerateProjects = true)]
    readonly Solution Solution;

    [GitVersion(Framework = "net10.0", NoFetch = true, NoCache = true)]
    readonly GitVersion GitVersion;

    AbsolutePath ArtifactsDirectory => RootDirectory / "Artifacts";

    AbsolutePath TestResultsDirectory => RootDirectory / "TestResults";

    [NuGetPackage("PackageGuard", "PackageGuard.dll")]
    Tool PackageGuard;

    [NuGetPackage("JetBrains.ReSharper.GlobalTools", "inspectcode.exe")]
    Tool InspectCode;

    string SemVer;

    Target CalculateNugetVersion => _ => _
        .Executes(() =>
        {
            SemVer = GitVersion.SemVer;
            if (IsPullRequest)
            {
                Information(
                    "Branch spec {branchspec} is a pull request. Adding build number {buildnumber}",
                    BranchSpec, BuildNumber);

                SemVer = string.Join('.', GitVersion.SemVer.Split('.').Take(3).Union(new[]
                {
                    BuildNumber
                }));
            }

            Information("SemVer = {semver}", SemVer);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution)
                .EnableNoCache());
        });

    Target Compile => _ => _
        .DependsOn(CalculateNugetVersion)
        .DependsOn(Restore)
        .Executes(() =>
        {
            ReportSummary(s => s
                .WhenNotNull(SemVer, (summary, semVer) => summary
                    .AddPair("Version", semVer)));

            DotNetBuild(s => s
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .EnableNoLogo()
                .EnableNoRestore()
				.SetVersion(SemVer)
                .SetAssemblyVersion(GitVersion.AssemblySemVer)
                .SetFileVersion(GitVersion.AssemblySemFileVer)
                .SetInformationalVersion(GitVersion.InformationalVersion) );
        });

    Target RunInspectCode => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            InspectCode($"FluidCaching.sln -o={ArtifactsDirectory / "CodeIssues.sarif"} --no-build");
        });

    Target RunTests => _ => _
        .DependsOn(Compile, RunInspectCode)
        .Executes(() =>
        {
            TestResultsDirectory.CreateOrCleanDirectory();
            var project = Solution.GetProject("FluidCaching.Specs");

            DotNetTest(s => s
                // We run tests in debug mode so that Fluent Assertions can show the names of variables
                .SetConfiguration(Configuration.Debug)
                // To prevent the machine language to affect tests sensitive to the current thread's culture
                .SetProcessEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE", "en-US")
                .SetDataCollector("XPlat Code Coverage")
                .SetCollectCoverage(true)
                .SetCoverletOutputFormat(CoverletOutputFormat.cobertura)
                .SetResultsDirectory(TestResultsDirectory)
                .SetProjectFile(project)
                .CombineWith(project.GetTargetFrameworks(),
                    (ss, framework) => ss
                        .SetFramework(framework)
                        .AddLoggers($"trx;LogFileName={framework}.trx")
                ));
        });

    Target ApiChecks => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            var project = Solution.GetProject("FluidCaching.ApiVerificationTests");

            // xunit.v3 ships its own Microsoft.Testing.Platform-based test host, and running that through
            // `dotnet test`'s legacy VSTest bridge is no longer supported on .NET 10 SDK and later. Invoking the
            // built executable directly runs it natively via Microsoft.Testing.Platform instead.
            // FluidCaching.ApiVerificationTests only targets net10.0, so that framework moniker is hardcoded below.
            string extension = OperatingSystem.IsWindows() ? ".exe" : "";
            AbsolutePath executable = project!.Directory / "bin" / Configuration / "net10.0" / $"{project.Name}{extension}";

            var startInfo = new ProcessStartInfo(executable)
            {
                Arguments = $"-result-trx \"{TestResultsDirectory / $"{project.Name}.trx"}\"",
                WorkingDirectory = project.Directory,
                UseShellExecute = false
            };
            startInfo.EnvironmentVariables["DOTNET_CLI_UI_LANGUAGE"] = "en-US";

            using var process = Process.Start(startInfo);
            process!.WaitForExit();

            if (process.ExitCode != 0)
            {
                throw new Exception($"Process '{executable}' exited with code {process.ExitCode}.");
            }
        });

    Target ScanPackages => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            Environment.SetEnvironmentVariable("GITHUB_API_KEY", GitHubApiKey);
            PackageGuard($"--config-path={RootDirectory / ".packageguard" / "config.json"} --use-caching {RootDirectory}");
        });

    Target GenerateCodeCoverageReport => _ => _
        .DependsOn(RunTests)
        .Executes(() =>
        {
            ReportGenerator(s => s
				.AddReports(TestResultsDirectory / "**/coverage.cobertura.xml")
                .AddReportTypes(ReportTypes.lcov, ReportTypes.Html)
                .SetTargetDirectory(TestResultsDirectory / "reports")
                .AddFileFilters("-*.g.cs"));

            string link = TestResultsDirectory / "reports" / "index.html";
            Information($"Code coverage report: \x1b]8;;file://{link.Replace('\\', '/')}\x1b\\{link}\x1b]8;;\x1b\\");
        });

    Target PreparePackageReadme => _ => _
        .Executes(() =>
        {
            var content = (RootDirectory / "README.md").ReadAllText();
            var sections = content.Split(["\n## "], StringSplitOptions.RemoveEmptyEntries);

            string[] headersToInclude =
            [
                "About",
                "How do I use it",
                "Where do I get it",
                "Versioning",
                "Credits"
            ];

            var readmeContent = "## " + string.Join("\n## ", sections
                .Where(section => headersToInclude.Any(header => section.StartsWith(header, StringComparison.OrdinalIgnoreCase))));

            (ArtifactsDirectory / "Readme.md").WriteAllText(readmeContent);
        });

    Target Pack => _ => _
        .DependsOn(ScanPackages)
        .DependsOn(PreparePackageReadme)
        .DependsOn(CalculateNugetVersion)
        .DependsOn(ApiChecks)
        .DependsOn(GenerateCodeCoverageReport)
        .Executes(() =>
        {
            ReportSummary(s => s
                .WhenNotNull(SemVer, (c, semVer) => c
                    .AddPair("Packed version", semVer)));

            // Pack the binary FluidCaching library
            DotNetPack(s => s
                .SetProject(Solution.GetProject("FluidCaching"))
                .SetOutputDirectory(ArtifactsDirectory)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoLogo()
                .EnableNoRestore()
                .EnableContinuousIntegrationBuild() // Necessary for deterministic builds
                .SetVersion(SemVer));

            // Pack the source-only package
            DotNetPack(s => s
                .SetProject(Solution.GetProject("FluidCaching.Sources"))
                .SetOutputDirectory(ArtifactsDirectory)
                .SetConfiguration(Configuration)
                .EnableNoBuild()
                .EnableNoLogo()
                .EnableNoRestore()
                .EnableContinuousIntegrationBuild()
                .SetVersion(SemVer));
        });


    Target Push => _ => _
        .DependsOn(Pack)
        .OnlyWhenDynamic(() => IsTag && !string.IsNullOrWhiteSpace(NugetArtifactsApiKey))
        .ProceedAfterFailure()
        .Executes(() =>
        {
            var packages = ArtifactsDirectory.GlobFiles("FluidCaching*.nupkg");

            Assert.NotEmpty(packages);

            DotNetNuGetPush(s => s
                .SetApiKey(NugetArtifactsApiKey)
                .EnableSkipDuplicate()
                .SetSource(NugetArtifactsApiUri)
                .EnableNoSymbols()
                .CombineWith(packages,
                    (v, path) => v.SetTargetPath(path)));
        });

    Target Default => _ => _
		.DependsOn(Pack)
        .DependsOn(Push);

    bool IsPullRequest => GitHubActions?.IsPullRequest ?? false;

    bool IsTag => BranchSpec != null && BranchSpec.Contains("refs/tags", StringComparison.OrdinalIgnoreCase);
}
