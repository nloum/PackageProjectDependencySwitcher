using System;
using IoFluently;

namespace PackageProjectDependencySwitcher
{
    public interface IDependencySwitcherService
    {
        void ConvertProjectReferencesToPackageReferences(AbsolutePath solutionFolder, Func<AbsolutePath, string, bool> referencePredicate, bool backup);
        void ConvertToPackageReferences(AbsolutePath solutionFolder, Func<AbsolutePath, string, bool> referencePredicate, bool backup);
    }
}