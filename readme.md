# Package-Project Dependency Switcher

This is a command line program that operates on a directory that contains C# project files (*.csproj). Here's what it does:

1. Given a directory, this tool finds projects that generate packages (based on the fact they have a `<PackageVersion>` XML element in them)
2. The tool then finds projects that depend on those projects
3. This tool then changes those project dependencies to be package dependencies.

Eventually the tool will also operate in reverse.

Here's how you change project dependencies to package dependencies:

    PackageProjectDependencySwitcher.exe package path/to/folder/containing/csproj/files

*Note that this is pre-alpha software and is subject to change and is probably quite buggy.*

When you use this tool, do a git commit before using it and carefully inspect all the changes it makes.
