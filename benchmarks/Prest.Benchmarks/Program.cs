using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Running;

var config = DefaultConfig.Instance.WithArtifactsPath(ResolveArtifactsPath());
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);

static string ResolveArtifactsPath()
{
    // Walk up from the assembly location until we find the solution file, then
    // anchor artifacts at benchmarks/artifacts so results land in the same place
    // regardless of whether we're run from the repo root, the project dir, or CI.
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "Prest.slnx")))
    {
        dir = dir.Parent;
    }

    var root = dir?.FullName ?? Directory.GetCurrentDirectory();
    return Path.Combine(root, "benchmarks", "artifacts");
}
