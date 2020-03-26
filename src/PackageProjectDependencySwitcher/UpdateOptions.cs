using CommandLine;

namespace PackageProjectDependencySwitcher
{
    [Verb("update")]
    public class UpdateOptions
    {
        public UpdateOptions(string path)
        {
            Path = path;
        }

        [Value(0)]
        public string Path { get; }
    }
}