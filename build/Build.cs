using System.Collections.Generic;
using System.Linq;
using Nuke.Common;
using Nuke.Common.CI;
using Nuke.Common.CI.GitHubActions;
using Nuke.Common.Execution;
using Nuke.Common.Git;
using Nuke.Common.IO;
using Nuke.Common.ProjectModel;
using Nuke.Common.Tooling;
using Nuke.Common.Tools.DotNet;
using Nuke.Common.Utilities.Collections;
using static Nuke.Common.IO.FileSystemTasks;
using static Nuke.Common.Tools.DotNet.DotNetTasks;

[CheckBuildProjectConfigurations]
[UnsetVisualStudioEnvironmentVariables]
[GitHubActions("dotnetcore",
	GitHubActionsImage.Ubuntu1804,
	ImportSecrets = new[]{ "NUGET_API_KEY" },
	AutoGenerate = true,
	On = new [] { GitHubActionsTrigger.Push, GitHubActionsTrigger.PullRequest },
	InvokedTargets = new [] {"Test", "Push"}
	)]
class Build : NukeBuild
{
    /// Support plugins are available for:
    ///   - JetBrains ReSharper        https://nuke.build/resharper
    ///   - JetBrains Rider            https://nuke.build/rider
    ///   - Microsoft VisualStudio     https://nuke.build/visualstudio
    ///   - Microsoft VSCode           https://nuke.build/vscode

    public static int Main() => Execute<Build>(x => x.Test);

    [Parameter("Configuration to build - Default is 'Debug' (local) or 'Release' (server)")]
    readonly Configuration Configuration = IsLocalBuild ? Configuration.Debug : Configuration.Release;
    [Parameter("NuGet server URL.")]
	readonly string NugetSource = "https://api.nuget.org/v3/index.json";
    [Parameter("API Key for the NuGet server.")]
	readonly string NugetApiKey;
	[Parameter("Version to use for package.")]
	readonly string Version;

    [Solution]
	readonly Solution Solution;
    [GitRepository]
	readonly GitRepository GitRepository;
    //[GitVersion]
	//readonly GitVersion GitVersion;
	
    AbsolutePath SourceDirectory => RootDirectory / "src";
    AbsolutePath ArtifactsDirectory => RootDirectory / "artifacts";

    Project PackageProject => Solution.GetProject("PackageProjectDependencySwitcher");
    
    IEnumerable<Project> TestProjects => Solution.GetProjects("*.Tests");
    
    Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            SourceDirectory.GlobDirectories("**/bin", "**/obj").ForEach(DeleteDirectory);
            EnsureCleanDirectory(ArtifactsDirectory);
        });

    Target Restore => _ => _
        .Executes(() =>
        {
            DotNetRestore(s => s
                .SetProjectFile(Solution)
			);
        });

    Target Compile => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {   
            DotNetBuild(s => s
                .EnableNoRestore()
                .SetProjectFile(Solution)
                .SetConfiguration(Configuration)
                .SetAssemblyVersion(Version + ".0")
                .SetFileVersion(Version)
                .SetInformationalVersion(Version)
			);

            DotNetPublish(s => s
				.EnableNoRestore()
				.EnableNoBuild()
				.SetConfiguration(Configuration)
				.SetAssemblyVersion(Version + ".0")
				.SetFileVersion(Version)
				.SetInformationalVersion(Version)
				.CombineWith(
					from project in new[] { PackageProject }
					from framework in project.GetTargetFrameworks()
                    select new { project, framework }, (cs, v) => cs
						.SetProject(v.project)
						.SetFramework(v.framework)
				)
			);
        });

    Target Test => _ => _
        .DependsOn(Compile)
        .Executes(() =>
        {
            DotNetTest(s => s
	            .SetConfiguration(Configuration)
	            .EnableNoRestore()
                .EnableNoBuild()
	            .CombineWith(
		            TestProjects, (cs, v) => cs
			            .SetProjectFile(v))
            );
        });

    Target Pack => _ => _
        .DependsOn(Clean, Test)
		.Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            DotNetPack(s => s
                .EnableNoRestore()
                .EnableNoBuild()
				.SetProject(Solution)
                .SetConfiguration(Configuration)
                .SetOutputDirectory(ArtifactsDirectory)
                .SetVersion(Version)
				.SetIncludeSymbols(true)
				.SetSymbolPackageFormat(DotNetSymbolPackageFormat.snupkg)
            );
        });

    Target Push => _ => _
        .DependsOn(Pack)
        .Consumes(Pack)
        .Requires(() => Configuration == Configuration.Release)
        .Executes(() =>
        {
            DotNetNuGetPush(s => s
				.SetSource(NugetSource)
				.SetApiKey(NugetApiKey)
				.SetSkipDuplicate(true)
				.CombineWith(ArtifactsDirectory.GlobFiles("*.nupkg"), (s, v) => s
					.SetTargetPath(v)
				)
            );
        });
}
