using System;
using System.Xml;
using IoFluently;

namespace PackageProjectDependencySwitcher
{
    public interface IDependencySwitcherService
    {
        void ConvertToProjectReferences(AbsolutePath solutionFolder, Func<AbsolutePath, string, bool> referencePredicate, bool backup);
        void ConvertToPackageReferences(AbsolutePath solutionFolder, Func<AbsolutePath, string, bool> referencePredicate, bool backup);
    }
}