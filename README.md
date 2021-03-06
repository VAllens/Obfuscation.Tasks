# ![Obfuscation.Tasks](logo.png) Obfuscation.Tasks

[![.NET Build](https://github.com/VAllens/Obfuscation.Tasks/actions/workflows/build.yml/badge.svg?branch=develop)](https://github.com/VAllens/Obfuscation.Tasks/actions/workflows/build.yml)
[![NuGet Publish](https://github.com/VAllens/Obfuscation.Tasks/actions/workflows/publish.yml/badge.svg?branch=develop&event=pull_request)](https://github.com/VAllens/Obfuscation.Tasks/actions/workflows/publish.yml)
[![CodeQL](https://github.com/VAllens/Obfuscation.Tasks/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/VAllens/Obfuscation.Tasks/actions/workflows/codeql-analysis.yml)

## Summary

This is an [MSBuild](https://github.com/dotnet/msbuild) custom task that can be triggered at any event after compilation to automatically obfuscate assembly files.

## Usage

### Install package

.NET CLI:

```cmd
dotnet add package Obfuscation.Tasks --version 1.0.2
```

or PowerShell:

```powershell
Install-Package Obfuscation.Tasks -Version 1.0.2
```

or Edit project items:

```xml
<ItemGroup>
  <PackageReference Include="Obfuscation.Tasks" Version="1.0.2">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

### Configure `ObfuscationTask`

Triggered after `PostBuildEvent`:

```xml
<Target Name="Obfuscation" AfterTargets="PostBuildEvent">
  <ObfuscationTask ToolDir="\\192.168.1.155\dll" InputFilePaths="$(TargetPath)" DependencyFiles="" OutputFileNameSuffix="" TimeoutMillisecond="2000" Importance="high">
    <Output TaskParameter="OutputFilePaths" PropertyName="SecuredFilePaths" />
  </ObfuscationTask>
  <Message Text="SecuredFilePaths: $(SecuredFilePaths)" Importance="high" />
</Target>
```

Or trigger after publication.
Here's a detail, when the `OutputType` is `Library`, it needs to depend on the `GenerateNuspec` target.
When the `OutputType` is `Exe`, it needs to depend on the `Publish` target.

```xml
<Target Name="Obfuscation" AfterTargets="GenerateNuspec">
  <ObfuscationTask ToolDir="\\192.168.1.155\dll" InputFilePaths="$(TargetPath)" Importance="low">
    <Output TaskParameter="OutputFilePaths" PropertyName="SecuredFilePaths" />
  </ObfuscationTask>
  <Message Text="SecuredFilePaths: $(SecuredFilePaths)" Importance="high" />
</Target>
```

- Input Parameters:
    - `ToolDir`: Required. Example: `\\192.168.1.155\dll`
    - `InputFilePaths`: Required. Single example: `D:\sources\ObfuscationSamples\ObfuscationSamples\bin\Release\ObfuscationSamples.dll`; Multiple examples: `$(TargetPath);D:\ref\Samples1.dll;D:\ref\Samples2.dll`
    - `DependencyFiles`: Optional. Example: `$(OutputPath)Serilog.dll;$(OutputPath)Serilog.Sinks.Console.dll`
    - `TimeoutMillisecond`: Optional. Default value: `30000`
    - `Importance`: Optional. Default value: Normal. Options: `High`, `Normal`, `Low`. Reference `Message` task
    - `OutputFileNameSuffix`: Optional. Default value: `_Secure`
      
- Output Parameters:
    - `OutputFilePaths`: string. Default value: `$(InputFilePaths)$(OutputFileNameSuffix).dll`. Example: `D:\sources\ObfuscationSamples\ObfuscationSamples\bin\Release\ObfuscationSamples_Secure.dll`

## Support

Support [MSBuild](https://github.com/dotnet/msbuild) v15.0 or higher.

## Samples

- [ObfuscationSamples](https://github.com/VAllens/Obfuscation.Tasks/tree/main/samples)

## Authors

- [Allen Cai](https://github.com/VAllens)

## License

- [MIT](LICENSE)
