using System.IO;
using System.Xml.Serialization;
using IoFluently;
using Schemas;

namespace PackageProjectDependencySwitcher
{
    public static class Extensions
    {
        public static IFileWithKnownFormatSync<Project> AsMsbuildFile(this AbsolutePath path)
        {
            var serializer = new XmlSerializer(typeof(Project));
            return path.AsFile(absPath =>
            {
                using (var stream = absPath.Open(FileMode.Open, FileAccess.Read))
                {
                    var result = (Project) serializer.Deserialize(stream);
                    return result;
                }
            }, (absPath, project) =>
            {
                using (var stream = absPath.Open(FileMode.Create, FileAccess.Write))
                {
                    serializer.Serialize(stream, project);
                }
            });
        }
    }
}