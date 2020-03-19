using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using CommandLine;
using MoreIO;
using ReactiveProcesses;

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
    
    class Program
    {
        private static Regex _projectReference = new Regex(@"<ProjectReference Include=""(?<projectFolderPath>.+)(?<projectFileName>[^<^\^/]+)\.(?<projectFileExtension>.+)""");
        private static Regex _packageVersion = new Regex(@"<PackageVersion>(?<packageVersion>[^<]+)</PackageVersion>");
        
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<PackageOptions, ProjectOptions>(args)
                .WithParsed<PackageOptions>(ConvertProjectReferencesToPackageReferences)
                .WithParsed<UpdateOptions>(UpdateReferencesToProjectPackages)
                .WithParsed<ProjectOptions>(ConvertPackageReferencesToProjectReferences);
        }

        private static void UpdateReferencesToProjectPackages(UpdateOptions options)
        {
            var ioService = new IoService(new ReactiveProcessFactory());
            
            var files = Directory.GetFiles(options.Path, "*.csproj", SearchOption.AllDirectories).Select(x => ioService.ToPath(x).Value).ToImmutableList();

            var filesToPackages = new Dictionary<PathSpec, Func<string, PathSpec, string>>();
            
            foreach (var file in files)
            {
                var text = file.ReadAllText();
                var packageVersion = _packageVersion.Match(text);

                if (packageVersion.Success)
                {
                    var regex = new Regex($"<PackageReference Include=\"{Path.GetFileNameWithoutExtension(file.ToString())}\".+/>");

                    filesToPackages[file] = (csprojFileText, relativeTo) =>
                    {
                        var path = file.RelativeTo(relativeTo);

                        return regex.Replace(text,
                            $"<PackageReference Include=\"{Path.GetFileNameWithoutExtension(file.ToString())}\" Version=\"{packageVersion.Groups["packageVersion"].Value}\" />");
                    };
                }
            }
            
            foreach (var file in files)
            {
                var text = file.ReadAllText();

                var numChanges = 0;
                foreach (var package in filesToPackages)
                {
                    var prevText = text;
                    text = package.Value(text, file.Parent().Value);
                    if (!text.Equals(prevText))
                    {
                        numChanges++;
                        Console.WriteLine($"Updating {Path.GetFileNameWithoutExtension(file.ToString())}'s dependency on {Path.GetFileNameWithoutExtension(package.Key.ToString())}");
                    }
                }
                
                file.WriteAllText(text);
            }
        }

        private static void ConvertPackageReferencesToProjectReferences(ProjectOptions options)
        {
            var ioService = new IoService(new ReactiveProcessFactory());
            
            var files = Directory.GetFiles(options.Path, "*.csproj", SearchOption.AllDirectories).Select(x => ioService.ToPath(x).Value).ToImmutableList();

            var filesToPackages = new Dictionary<PathSpec, Func<string, PathSpec, string>>();
            
            foreach (var file in files)
            {
                var text = file.ReadAllText();
                var packageVersion = _packageVersion.Match(text);

                if (packageVersion.Success)
                {
                    var regex = new Regex($"<PackageReference Include=\"{file.LastPathComponent()}\".+/>");

                    filesToPackages[file] = (csprojFileText, relativeTo) =>
                    {
                        var path = file.RelativeTo(relativeTo);

                        return regex.Replace(text,
                                $"<ProjectReference Include=\"{path}\" />");
                    };
                }
            }
            
            foreach (var file in files)
            {
                var text = file.ReadAllText();

                var numChanges = 0;
                foreach (var package in filesToPackages)
                {
                    var prevText = text;
                    text = package.Value(text, file.Parent().Value);
                    if (!text.Equals(prevText))
                    {
                        numChanges++;
                        Console.WriteLine($"Changing {Path.GetFileNameWithoutExtension(file.ToString())}'s dependency on {Path.GetFileNameWithoutExtension(package.Key.ToString())} from a project reference to a package reference");
                    }
                }
                
                file.WriteAllText(text);
            }
        }
        
        private static void ConvertProjectReferencesToPackageReferences(PackageOptions options)
        {
            var files = Directory.GetFiles(options.Path, "*.csproj", SearchOption.AllDirectories).ToImmutableList();

            var filesToPackages = new Dictionary<string, Func<string, string>>();
            
            foreach (var file in files)
            {
                var text = File.ReadAllText(file);
                var packageVersion = _packageVersion.Match(text);

                if (packageVersion.Success)
                {
                    var regex = new Regex($"<ProjectReference Include=\".+{Path.GetFileName(file)}\" */>");

                    var packageVersionNumber = packageVersion.Groups["packageVersion"].Value;
                    filesToPackages[file] = str => regex.Replace(str, $"<PackageReference Include=\"{Path.GetFileNameWithoutExtension(file)}\" Version=\"{packageVersionNumber}\" />");
                }
            }

            foreach (var file in files)
            {
                var text = File.ReadAllText(file);

                if (Path.GetFileNameWithoutExtension(file).EndsWith(".Test") || Path.GetFileNameWithoutExtension(file).EndsWith(".Tests"))
                {
                    // TODO - make this behavior configurable.
                    continue;
                }
                
                var numChanges = 0;
                foreach (var package in filesToPackages)
                {
                    var prevText = text;
                    text = package.Value(text);
                    if (!text.Equals(prevText))
                    {
                        numChanges++;
                        Console.WriteLine($"Changing {Path.GetFileNameWithoutExtension(file)}'s dependency on {Path.GetFileNameWithoutExtension(package.Key)} from a project reference to a package reference");
                    }
                }
                
                File.WriteAllText(file, text);
            }
        }
    }
}