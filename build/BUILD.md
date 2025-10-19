# Centralized Build and Package Management

This solution has been configured with centralized package management and build traversal for efficient building and dependency management.

## Overview

The solution uses:
- **global.json**: Defines the .NET SDK version and MSBuild SDK versions
- **Directory.Packages.props**: Centralized NuGet package version management
- **Directory.Build.props**: Common build properties for all projects
- **dirs.proj**: Build traversal projects for organized building

## File Structure

```
├── global.json                    # SDK version management
├── Directory.Packages.props       # Centralized package versions
├── Directory.Build.props          # Common build properties
├── dirs.proj                      # Main build traversal
├── tests.proj                     # Test projects traversal
├── build.ps1                      # Main build script
├── build.cmd                      # Windows build wrapper
└── [folders]/
    └── dirs.proj                  # Folder-specific build traversal
```

## Building the Solution

### Using PowerShell (Recommended)

```powershell
# Basic build
./build.ps1

# Build with tests
./build.ps1 -Test

# Build with packages
./build.ps1 -Pack

# Full build (clean, restore, build, test, pack)
./build.ps1 -Clean -Restore -Test -Pack

# Debug build with tests
./build.ps1 -Configuration Debug -Test
```

### Using Command Line

```cmd
# Basic build
build.cmd

# Build with parameters
build.cmd -Test -Pack
```

### Using dotnet CLI directly

```bash
# Build all projects
dotnet build dirs.proj

# Run all tests
dotnet test tests.proj

# Create packages
dotnet pack dirs.proj --output ../NuGetPackages
```

## Centralized Package Management

All package versions are managed in `Directory.Packages.props`. To add a new package:

1. Add the package version to `Directory.Packages.props`:
   ```xml
   <PackageVersion Include="MyPackage" Version="1.0.0" />
   ```

2. Reference it in your project without version:
   ```xml
   <PackageReference Include="MyPackage" />
   ```

### Version Conflict Resolution

The centralized package management automatically resolves version conflicts by using the highest version specified in `Directory.Packages.props`.

## Build Traversal

The `dirs.proj` files use wildcards to automatically discover and build projects:

```xml
<ProjectReference Include="**\*.csproj" Exclude="**\*.Tests\*.csproj;**\bin\**;**\obj\**" />
```

This means:
- ✅ Automatically finds all `.csproj` files in subdirectories
- ✅ Excludes test projects (built separately)
- ✅ Excludes bin/obj folders
- ✅ No need to manually maintain project lists

## Project Structure Benefits

### For Developers
- **Single command builds**: `./build.ps1` builds everything
- **Automatic project discovery**: No need to manually add projects to solution
- **Consistent dependencies**: All projects use the same package versions
- **Fast builds**: Only builds what's needed

### For CI/CD
- **Reliable builds**: Centralized versions prevent conflicts
- **Easy scripting**: Simple build commands
- **Test separation**: Tests can be run independently
- **Package creation**: Automatic NuGet package generation

## Common Tasks

### Adding a New Project
1. Create your `.csproj` file
2. It will automatically be discovered by the build system
3. No need to modify solution files or build scripts

### Adding Dependencies
1. Add package version to `Directory.Packages.props`
2. Reference without version in your project
3. The build system handles the rest

### Running Tests
```powershell
# Run all tests
./build.ps1 -Test

# Run tests for specific configuration
./build.ps1 -Configuration Debug -Test
```

### Creating Packages
```powershell
# Create packages for all projects
./build.ps1 -Pack

# Packages will be created in ../NuGetPackages/
```

## Troubleshooting

### Build Errors
1. Ensure you have the correct .NET SDK version (specified in `global.json`)
2. Run with `-Clean -Restore` to reset state
3. Check `Directory.Packages.props` for version conflicts

### Missing Projects
The build system automatically discovers projects. If a project isn't building:
1. Ensure it has a `.csproj` extension
2. Check it's not in `bin/` or `obj/` folders
3. Verify the `dirs.proj` patterns match your structure

### Package Conflicts
All package versions are centralized. If you need a different version:
1. Update `Directory.Packages.props`
2. The new version will be used across all projects
3. Test thoroughly to ensure compatibility
