using CommandLine;

namespace PackageProjectDependencySwitcher
{
    [Verb("package")]
    public class PackageOptions
    {
        public PackageOptions(string path)
        {
            Path = path;
        }

        [Value(0)]
        public string Path { get; }
    }
}