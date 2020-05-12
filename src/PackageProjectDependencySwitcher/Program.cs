﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using CommandLine;
using IoFluently;
using ReactiveProcesses;

namespace PackageProjectDependencySwitcher
{
    class Program
    {
        private static Regex _projectReference = new Regex(@"<ProjectReference Include=""(?<projectFolderPath>.+)(?<projectFileName>[^<^\^/]+)\.(?<projectFileExtension>.+)""");
        private static Regex _packageVersion = new Regex(@"<PackageVersion>(?<packageVersion>[^<]+)</PackageVersion>");
        
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<PackageOptions, ProjectOptions, UpdateOptions>(args)
                .WithParsed<PackageOptions>(ConvertProjectReferencesToPackageReferences)
                .WithParsed<UpdateOptions>(UpdateReferencesToProjectPackages)
                .WithParsed<ProjectOptions>(ConvertPackageReferencesToProjectReferences);
        }

        private static void UpdateReferencesToProjectPackages(UpdateOptions options)
        {
            var ioService = new IoService(new ReactiveProcessFactory());
            
            var files = Directory.GetFiles(options.Path, "*.csproj", SearchOption.AllDirectories).Select(x => ioService.ToAbsolutePath(x)).ToImmutableList();

            var filesToPackages = new Dictionary<AbsolutePath, Func<string, AbsolutePath, string>>();
            
            foreach (var file in files)
            {
                var text = file.ReadAllText();
                var packageVersion = _packageVersion.Match(text);

                if (packageVersion.Success)
                {
                    var regex = new Regex($"<PackageReference Include=\"{Path.GetFileNameWithoutExtension(file.ToString())}\"[^/]+/>");

                    filesToPackages[file] = (csprojFileText, relativeTo) =>
                    {
                        var path = file.RelativeTo(relativeTo);

                        var result = regex.Replace(csprojFileText,
                            $"<PackageReference Include=\"{Path.GetFileNameWithoutExtension(file.ToString())}\" Version=\"{packageVersion.Groups["packageVersion"].Value}\" />");
                        return result;
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
            
            var files = Directory.GetFiles(options.Path, "*.csproj", SearchOption.AllDirectories).Select(x => ioService.ToAbsolutePath(x)).ToImmutableList();

            var filesToPackages = new Dictionary<AbsolutePath, Func<string, AbsolutePath, string>>();
            
            foreach (var file in files)
            {
                var text = file.ReadAllText();
                var packageVersion = _packageVersion.Match(text);

                if (packageVersion.Success)
                {
                    var regex = new Regex($"<PackageReference Include=\"{Path.GetFileNameWithoutExtension(file.LastPathComponent())}\".+/>");

                    filesToPackages[file] = (csprojFileText, relativeTo) =>
                    {
                        var path = file.RelativeTo(relativeTo);

                        var result = regex.Replace(csprojFileText,
                                $"<ProjectReference Include=\"{path}\" />");
                        return result;
                    };
                }
            }
            
            foreach (var file in files)
            {
                var text = file.ReadAllText();

                var numChanges = 0;
                foreach (var package in filesToPackages)
                {
                    if (package.Key.Equals(file))
                    {
                        continue;
                    }
                    
                    var prevText = text;
                    text = package.Value(text, file.Parent().Value);
                    if (!text.Equals(prevText))
                    {
                        numChanges++;
                        Console.WriteLine($"Changing {Path.GetFileNameWithoutExtension(file.ToString())}'s dependency on {Path.GetFileNameWithoutExtension(package.Key.ToString())} from a project reference to a package reference");
                    }
                }

                if (numChanges > 0)
                {
                    file.WriteAllText(text);
                }
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