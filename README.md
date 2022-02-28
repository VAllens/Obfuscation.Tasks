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
dotnet add package Obfuscation.Tasks --version 1.0.1
```

or PowerShell:

```powershell
Install-Package Obfuscation.Tasks -Version 1.0.1
```

or Edit project items:

```xml
<ItemGroup>
  <PackageReference Include="Obfuscation.Tasks" Version="1.0.1">
    <PrivateAssets>all</PrivateAssets>
    <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
  </PackageReference>
</ItemGroup>
```

### Configure `ObfuscationTask`

Triggered after `PostBuildEvent`:

```xml
<Target Name="Obfuscation" AfterTargets="PostBuildEvent">
  <ObfuscationTask ToolDir="\\192.168.1.155\dll" InputFilePath="$(TargetPath)" DependencyFiles="" OutputFilePath="" ObfuscateFileNameSuffix="" TimeoutMillisecond="2000" Importance="high">
    <Output TaskParameter="ObfuscationedFilePath" PropertyName="SecuredFilePath" />
  </ObfuscationTask>
  <Message Text="SecuredFilePath: $(SecuredFilePath)" Importance="high" />
</Target>
```

Or trigger after publication.
Here's a detail, when the `OutputType` is `Library`, it needs to depend on the `GenerateNuspec` target.
When the `OutputType` is `Exe`, it needs to depend on the `Publish` target.

```xml
<Target Name="Obfuscation" AfterTargets="GenerateNuspec">
  <ObfuscationTask ToolDir="\\192.168.1.155\dll" InputFilePath="$(TargetPath)" Importance="low">
    <Output TaskParameter="ObfuscationedFilePath" PropertyName="SecuredFilePath" />
  </ObfuscationTask>
  <Message Text="SecuredFilePath: $(SecuredFilePath)" Importance="high" />
</Target>
```

- Input Parameters:
    - `ToolDir`: Required. Example: `\\192.168.1.155\dll`
    - `InputFilePath`: Required. Example: `D:\sources\ObfuscationSamples\ObfuscationSamples\bin\Release\ObfuscationSamples.dll`
    - `DependencyFiles`: Optional. Example: `$(OutputPath)Serilog.dll;$(OutputPath)Serilog.Sinks.Console.dll`
    - `OutputFilePath`: Optional. Default value: `$(InputFilePath)_Secure.dll`. Example: `D:\sources\ObfuscationSamples\ObfuscationSamples\bin\Release\ObfuscationSamples_Secure.dll`
    - `TimeoutMillisecond`: Optional. Default value: `30000`
    - `Importance`: Optional. Default value: Normal. Options: `High`, `Normal`, `Low`. Reference `Message` task
    - `ObfuscateFileNameSuffix`: Optional. Default value: _Secure
      
- Output Parameters:
    - `ObfuscationedFilePath`: string. Default value: `OutputFilePath`

## Support

Support [MSBuild](https://github.com/dotnet/msbuild) v15.0 or higher.

## Samples

- [ObfuscationSamples](https://github.com/VAllens/Obfuscation.Tasks/tree/main/samples)

## Authors

- [Allen Cai](https://github.com/VAllens)

## License

- [MIT](LICENSE)
