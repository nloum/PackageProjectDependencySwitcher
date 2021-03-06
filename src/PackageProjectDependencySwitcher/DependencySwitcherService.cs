﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text.RegularExpressions;
using IoFluently;

namespace PackageProjectDependencySwitcher
{
    public class DependencySwitcherService : IDependencySwitcherService
    {
        private static Regex _projectReference = new Regex(@"<ProjectReference Include=""(?<projectFolderPath>.+)(?<projectFileName>[^<^\^/]+)\.(?<projectFileExtension>.+)""");
        private static Regex _packageVersion = new Regex(@"<PackageVersion>(?<packageVersion>[^<]+)</PackageVersion>");

        public void ConvertToProjectReferences(AbsolutePath solutionFolder, Func<AbsolutePath, string, bool> referencePredicate, bool backup)
        {
            var files = solutionFolder.GetDescendants("*.csproj").ToImmutableList();

            var filesToPackages = new Dictionary<AbsolutePath, Func<string, AbsolutePath, string>>();
            
            foreach (var file in files)
            {
                var text = file.ReadAllText();
                var packageVersion = _packageVersion.Match(text);

                if (packageVersion.Success)
                {
                    var regex = new Regex($"<PackageReference Include=\"{Path.GetFileNameWithoutExtension(file.Name)}\".+/>");

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
                    text = package.Value(text, file.Parent());
                    if (!text.Equals(prevText))
                    {
                        numChanges++;
                        Console.WriteLine($"Changing {Path.GetFileNameWithoutExtension(file.ToString())}'s dependency on {Path.GetFileNameWithoutExtension(package.Key.ToString())} from a package reference to a project reference");
                    }
                }

                if (numChanges > 0)
                {
                    file.WriteAllText(text);
                }
            }
        }
        
        public void ConvertToPackageReferences(AbsolutePath solutionFolder, Func<AbsolutePath, string, bool> referencePredicate, bool backup)
        {
            var files = solutionFolder.GetDescendants("*.csproj").ToImmutableList();

            var filesToPackages = new Dictionary<AbsolutePath, Func<string, string>>();
            
            foreach (var file in files)
            {
                var text = file.ReadAllText();
                var packageVersion = _packageVersion.Match(text);

                if (packageVersion.Success)
                {
                    var regex = new Regex($"<ProjectReference Include=\".+{file.Name}\" */>");

                    var packageVersionNumber = packageVersion.Groups["packageVersion"].Value;
                    filesToPackages[file] = str => regex.Replace(str, $"<PackageReference Include=\"{file.WithoutExtension().Name}\" Version=\"{packageVersionNumber}\" />");
                }
            }

            foreach (var file in files)
            {
                var text = file.ReadAllText();

                if (file.WithoutExtension().HasExtension(".Test"))
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
                        Console.WriteLine($"Changing {file.WithoutExtension().Name}'s dependency on {package.Key.WithoutExtension().Name} from a project reference to a package reference");
                    }
                }
                
                file.WriteAllText(text);
            }
        }
    }
}