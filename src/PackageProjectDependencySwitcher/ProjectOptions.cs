using CommandLine;

namespace PackageProjectDependencySwitcher
{
    [Verb("project")]
    public class ProjectOptions
    {
        public ProjectOptions(string path)
        {
            Path = path;
        }

        [Value(0)]
        public string Path { get; }
    }
}