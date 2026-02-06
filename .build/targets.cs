#!/usr/bin/dotnet run

#:package McMaster.Extensions.CommandLineUtils@4.1.1
#:package Bullseye@6.0.0
#:package SimpleExec@12.0.0

using System;
using Bullseye;
using McMaster.Extensions.CommandLineUtils;
using static Bullseye.Targets;
using static SimpleExec.Command;

using var app = new CommandLineApplication { UsePagerForHelpText = false };
app.HelpOption();

var ridOption = app.Option<string>(
    "--rid <rid>",
    "The runtime identifier (RID) to use for publishing.",
    CommandOptionType.SingleValue
);
var versionOption = app.Option<string>(
    "--version <version>",
    "The release version.",
    CommandOptionType.SingleValue
);

app.Argument(
    "targets",
    "A list of targets to run or list. If not specified, the \"default\" target will be run, or all targets will be listed.",
    true
);
foreach (var (aliases, description) in Options.Definitions)
{
    _ = app.Option(string.Join("|", aliases), description, CommandOptionType.NoValue);
}

app.OnExecuteAsync(async ct =>
{
    const string configuration = "Release";
    const string solution = "Fusion++.slnx";
    const string publishProject = "Fusion++/Fusion++.csproj";

    var root = (
        await ReadAsync("git", "rev-parse --show-toplevel", cancellationToken: ct)
    ).StandardOutput.Trim();

    var targets = app.Arguments[0].Values.OfType<string>();
    var options = new Options(
        Options.Definitions.Select(d =>
            (
                d.Aliases[0],
                app.Options.Single(o => d.Aliases.Contains($"--{o.LongName}")).HasValue()
            )
        )
    );

    Target(
        "clean",
        async () => await RunAsync("dotnet", $"clean {solution} --configuration {configuration}")
    );

    Target(
        "restore",
        async () =>
        {
            var rid = ridOption.Value();
            var runtimeArg = !string.IsNullOrEmpty(rid) ? $"--runtime {rid}" : string.Empty;
            await RunAsync("dotnet", $"restore {solution} {runtimeArg}");
        }
    );

    Target(
        "build",
        ["restore"],
        async () =>
            await RunAsync(
                "dotnet",
                $"build {solution} --configuration {configuration} --no-restore"
            )
    );

    Target(
        "test",
        ["build"],
        async () =>
        {
            var testResultFolder = "TestResults";
            var coverageFileName = "coverage.xml";
            var testResultPath = Directory.CreateDirectory(Path.Combine(root, testResultFolder));
            await RunAsync(
                "dotnet",
                $"test --solution {solution} --configuration {configuration} --coverage --coverage-output {Path.Combine(testResultPath.FullName, coverageFileName)} --coverage-output-format xml --ignore-exit-code 8"
            );
        }
    );

    Target(
        "coverage",
        ["test"],
        async () =>
        {
            var testResultFolder = "TestResults";
            var coverageFileName = "coverage.xml";
            var coverageInputPath = Path.Combine(root, testResultFolder, coverageFileName);
            var reportOutputDir = Path.Combine(root, testResultFolder, "report");

            if (!File.Exists(coverageInputPath))
            {
                throw new InvalidOperationException(
                    $"Coverage file not found at {coverageInputPath}. Run the 'test' target first."
                );
            }

            var reportTypes = "Html;MarkdownSummaryGithub";

            await RunAsync(
                "dotnet",
                $"""
tool run reportgenerator -reports:"{coverageInputPath}" -targetdir:"{reportOutputDir}" -reporttypes:"{reportTypes}" -title:"Kuddle.Net Coverage Report"
"""
            );

            Console.WriteLine($"Coverage report generated at: {reportOutputDir}/summary.md");
        }
    );

    Target("default", ["build"], () => Console.WriteLine("Default target ran."));

    Target(
        "publish",
        async () =>
        {
            var rid = ridOption.Value();
            ArgumentException.ThrowIfNullOrWhiteSpace(rid, nameof(rid));
            var runtimeArg = $"--runtime {rid}";

            var publishDir = Path.Combine(root, "dist", "publish", rid);
            if (Directory.Exists(publishDir))
                Directory.Delete(publishDir, true);

            await RunAsync(
                "dotnet",
                $"publish {publishProject} -c {configuration} -o {publishDir} {runtimeArg} --self-contained"
            );
        }
    );

    Target(
        "release",
        ["publish"],
        async () =>
        {
            const string velopackId = "FusionPlusPlus";
            string iconPath = Path.Combine(root, "_res/app.ico");
            string splashImage = Path.Combine(root, "_res/app256.png");
            var version = versionOption.Value();
            ArgumentException.ThrowIfNullOrWhiteSpace(version, nameof(version));
            var rid = ridOption.Value();
            ArgumentException.ThrowIfNullOrWhiteSpace(rid, nameof(rid));

            var publishDir = Path.Combine(root, "dist", "publish", rid);
            var outputDir = Path.Combine(root, "dist", "release", rid);

            if (Directory.Exists(outputDir))
                Directory.Delete(outputDir, true);
            string directive =
                rid.StartsWith("linux", StringComparison.OrdinalIgnoreCase) ? "[linux]"
                : rid.StartsWith("osx", StringComparison.OrdinalIgnoreCase) ? "[osx]"
                : "[win]";
            await RunAsync(
                "dotnet",
                $"vpk {directive} pack --packId {velopackId} --packVersion {version} --icon \"{iconPath}\" --splashImage \"{splashImage}\" --packDir \"{publishDir}\" --mainExe \"Fusion++.exe\" --outputDir \"{outputDir}\" --yes"
            );
        }
    );

    await RunTargetsAndExitAsync(targets, options);
});

return await app.ExecuteAsync(args);
