using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Obfuscation.Tasks
{
    /// <summary>
    /// 混淆任务
    /// <para>About how implementent custom MSBuild task, see to: </para>
    /// <para>https://natemcmaster.com/blog/2017/07/05/msbuild-task-in-nuget/</para>
    /// <para>https://blog.walterlv.com/post/create-a-cross-platform-msbuild-task-based-nuget-tool.html</para>
    /// </summary>
    public sealed class ObfuscationTask : Task
    {
        /// <summary>
        /// 等待混淆输出程序集文件的超时时间(毫秒)，默认值：30000 (30秒)。
        /// </summary>
        private const int TIMEOUT_MILLISECOND = 30_000;

        /// <summary>
        /// 混淆文件名后缀。默认值: _Secure
        /// </summary>
        private const string OUTPUT_FILENAME_SUFFIX = "_Secure";

        /// <summary>
        /// 要混淆的程序集文件列表
        /// </summary>
        private IReadOnlyCollection<string> _inputFilePaths = Array.Empty<string>();

        /// <summary>
        /// 依赖的文件列表
        /// </summary>
        private IReadOnlyCollection<string> _dependencyFiles = Array.Empty<string>();

        /// <summary>
        /// 消息重要性级别。默认值：<see cref="MessageImportance.Normal"/>。
        /// </summary>
        private MessageImportance _messageImportance = MessageImportance.Normal;

        /// <summary>
        /// 混淆工具目录
        /// </summary>
        //[Required] //为了能够提示更明确和友好的错误信息，注释掉
        public string? ToolDir { get; set; }

        /// <summary>
        /// 要混淆的程序集文件列表
        /// </summary>
        //[Required]
        public string? InputFilePaths { get; set; }

        /// <summary>
        /// 依赖的文件
        /// </summary>
        public string? DependencyFiles { get; set; }

        /// <summary>
        /// 等待混淆输出程序集文件的超时时间(毫秒)，默认值：30000 (30秒)。
        /// </summary>
        public int TimeoutMillisecond { get; set; } = TIMEOUT_MILLISECOND;

        /// <summary>
        /// 消息重要性级别，可选。默认值：<see cref="MessageImportance.Normal"/>。枚举值详见：<see cref="MessageImportance"/>。
        /// </summary>
        public string Importance { get; set; } = nameof(MessageImportance.Normal);

        /// <summary>
        /// 混淆文件名后缀。默认值: _Secure
        /// </summary>
        public string OutputFileNameSuffix { get; set; } = OUTPUT_FILENAME_SUFFIX;

        /// <summary>
        /// 返回混淆后的程序集文件路径列表
        /// </summary>
        [Output]
        public string? OutputFilePaths { get; set; }

        /// <summary>
        /// 一个任务的执行方法实现
        /// </summary>
        /// <returns>true, 表示本任务执行成功</returns>
        public override bool Execute()
        {
            //System.Diagnostics.Debugger.Launch();

            string dotNetVersion;
#if NETSTANDARD2_0
            dotNetVersion = ".NET Standard 2.0";
#else
            dotNetVersion = ".NET Framework 4.7.2";
#endif
            LogMessageFromText("I'm a .NET Obfuscation Tasks. Runtime Verion: " + dotNetVersion);
            //LogMessageFromText("Current project file: " + BuildEngine.ProjectFileOfTaskNode);

#if DEBUG
            //输出全局属性列表
            PrintGlobalProperties();

#if NET472
            //输出项目属性列表
            PrintProjectProperties();

            //输出编译上下文的环境变量列表
            PrintEnvironmentVariables();
#endif
#endif

            //检查任务参数
            if (!CheckTaskParameters())
            {
                return false;
            }

            //执行混淆
            return ExecuteCore();
        }

        /// <summary>
        /// 检查任务参数的合法性
        /// </summary>
        /// <returns>true，表示输入的任务参数合法。</returns>
        private bool CheckTaskParameters()
        {
            //ToolDir
            if (string.IsNullOrWhiteSpace(ToolDir))
            {
                Log.LogError($"Please configure {nameof(ToolDir)}. Example: \\\\192.168.1.155\\dll");
                return false;
            }
            ToolDir = ToolDir!.Trim();

            if (Directory.Exists(ToolDir) == false)
            {
                Log.LogError($"The directory '{ToolDir}' not found. Input parameter: {nameof(ToolDir)}");
                return false;
            }

            //InputFilePaths
            if (string.IsNullOrWhiteSpace(InputFilePaths))
            {
#if NET472
                string? targetPath = BuildEngine.GetPropertyValue("TargetPath");
#else
                string? targetPath = null;
#endif
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    Log.LogError($"Please configure {nameof(InputFilePaths)}. Example: D:\\sources\\ObfuscationSamples\\ObfuscationSamples\\bin\\Release\\ObfuscationSamples.dll");
                    return false;
                }

                InputFilePaths = targetPath;
                _inputFilePaths = new List<string>() { targetPath! };

                Log.LogWarning($"{nameof(InputFilePaths)} is not configured, the default value will be used: {InputFilePaths}.");
            }
            else
            {
                InputFilePaths = InputFilePaths!.Trim();
                _inputFilePaths = InputFilePaths!.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList().AsReadOnly();
            }

            foreach (string inputFilePath in _inputFilePaths)
            {
                if (File.Exists(inputFilePath) == false)
                {
                    Log.LogError($"The File '{inputFilePath}' not found. Input parameter: {nameof(InputFilePaths)}");
                    return false;
                }
            }

            //DependencyFiles
            if (!string.IsNullOrWhiteSpace(DependencyFiles))
            {
                DependencyFiles = DependencyFiles!.Trim();
                List<string> dependencyFiles = DependencyFiles.Split(';').Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
                if (dependencyFiles.Count > 0)
                {
                    foreach (string dependencyFile in dependencyFiles)
                    {
                        if (!File.Exists(dependencyFile))
                        {
                            Log.LogError($"The File '{dependencyFile}' not found. Input parameter: {nameof(dependencyFile)}");
                            return false;
                        }
                    }

                    _dependencyFiles = dependencyFiles.AsReadOnly();
                }
            }

            //TimeoutMillisecond
            if (TimeoutMillisecond <= 0)
            {
                TimeoutMillisecond = TIMEOUT_MILLISECOND;
                Log.LogWarning($"{nameof(TimeoutMillisecond)} is not configured, the default value will be used: {TimeoutMillisecond}.");
            }

            //Importance
            if (!string.IsNullOrWhiteSpace(Importance) && Enum.TryParse(Importance!.Trim(), true, out MessageImportance importance))
            {
                _messageImportance = importance;
            }

            //ObfuscateFileNameSuffix
            if (string.IsNullOrWhiteSpace(OutputFileNameSuffix))
            {
                OutputFileNameSuffix = OUTPUT_FILENAME_SUFFIX;
            }

            return true;
        }

        /// <summary>
        /// 执行混淆
        /// </summary>
        /// <returns>true, 表示本任务执行成功</returns>
        private bool ExecuteCore()
        {
            try
            {
                //Copy dependency files to obfuscation tool dir
                List<string> obfuscationDependencyFilePaths = new();
                if (_dependencyFiles.Any())
                {
                    foreach (string dependencyFile in _dependencyFiles)
                    {
                        string obfuscationDependencyFilePath = Path.Combine(ToolDir, Path.GetFileName(dependencyFile));
                        LogMessageFromText($"Copying dependency file: {dependencyFile} => {obfuscationDependencyFilePath}");
                        File.Copy(dependencyFile, obfuscationDependencyFilePath, overwrite: true);
                        obfuscationDependencyFilePaths.Add(obfuscationDependencyFilePath);
                    }
                }

                //Delete obfuscationed dlls in out dir (Always assume that these files may exist)
                List<KeyValuePair<string, string>> obfuscationOutputFilePaths = new();
                foreach (string inputFilePath in _inputFilePaths)
                {
                    string newFileName = $"{Path.GetFileNameWithoutExtension(inputFilePath)}{OutputFileNameSuffix}{Path.GetExtension(inputFilePath)}";
                    string obfuscationOutputFilePath = Path.Combine(ToolDir, newFileName);
                    LogMessageFromText($"Deleting obfuscationed file in {nameof(ToolDir)}: {obfuscationOutputFilePath}");
                    File.Delete(obfuscationOutputFilePath);
                    obfuscationOutputFilePaths.Add(new KeyValuePair<string, string>(obfuscationOutputFilePath, inputFilePath));
                }

                //Copy input files to obfuscation tool dir
                List<string> obfuscationInputFilePaths = new();
                foreach (string inputFilePath in _inputFilePaths)
                {
                    string obfuscationInputFilePath = Path.Combine(ToolDir, Path.GetFileName(inputFilePath));
                    LogMessageFromText($"Copying input file: {inputFilePath} => {obfuscationInputFilePath}");
                    File.Copy(InputFilePaths, obfuscationInputFilePath, overwrite: true);
                    obfuscationInputFilePaths.Add(obfuscationInputFilePath);
                }

                //Wait obfuscation file generated
                LogMessageFromText($"Waiting generate obfuscation files");
                string[] obfuscationFilePaths = obfuscationOutputFilePaths.Select(x => x.Key).ToArray();
                bool result = WaitGenerateObfuscationedFile(obfuscationFilePaths, TimeoutMillisecond);
                if (!result)
                {
                    Log.LogError($"Obfuscation task waiting for obfuscationed file output timeout. {nameof(TimeoutMillisecond)}: {TimeoutMillisecond}. Can't find the obfuscationed file paths: {string.Join(";", obfuscationFilePaths)}.");
                    return false;
                }

                //Delete input files in obfuscation tool dir
                foreach (string obfuscationInputFilePath in obfuscationInputFilePaths)
                {
                    LogMessageFromText($"Deleting input file in {nameof(ToolDir)}: {obfuscationInputFilePath}.");
                    File.Delete(obfuscationInputFilePath);
                }

                //Delete dependency files in obfuscation tool dir
                if (obfuscationDependencyFilePaths.Any())
                {
                    foreach (string dependencyFile in obfuscationDependencyFilePaths)
                    {
                        LogMessageFromText($"Deleting dependency file in {nameof(ToolDir)}: {dependencyFile}.");
                        File.Delete(dependencyFile);

                        //Always assume this files may exist
                        string newDependencyFileName = $"{Path.GetFileNameWithoutExtension(dependencyFile)}{OutputFileNameSuffix}{Path.GetExtension(dependencyFile)}";
                        string obfuscationDependencyFilePath = Path.Combine(ToolDir, newDependencyFileName);
                        LogMessageFromText($"Deleting obfuscationed‘s dependency file in {nameof(ToolDir)}: {obfuscationDependencyFilePath}.");
                        File.Delete(obfuscationDependencyFilePath);
                    }
                }

                //Move obfuscationed files to output path
                List<string> outputFilePaths = new();
                foreach (KeyValuePair<string, string> keyValuePair in obfuscationOutputFilePaths)
                {
                    string obfuscationOutputFilePath = keyValuePair.Key;
                    string inputFilePath = keyValuePair.Value;

                    string dir = Path.GetDirectoryName(inputFilePath);
                    string fileName = Path.GetFileName(obfuscationOutputFilePath);
                    string outputFilePath = Path.Combine(dir, fileName);
                    LogMessageFromText($"Moving obfuscationed file path: {obfuscationOutputFilePath} => {outputFilePath}");
                    File.Delete(outputFilePath);
                    File.Move(obfuscationOutputFilePath, outputFilePath);
                    outputFilePaths.Add(outputFilePath);
                }

                //Output parameter
                OutputFilePaths = outputFilePaths.Count > 0 ? string.Join(";", outputFilePaths) : string.Empty;
                LogMessageFromText("Obfuscation completed");
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }

            return true;
        }

        /// <summary>
        /// 阻塞等待生成受保护文件。如果始终没有输出，则超时。
        /// </summary>
        /// <param name="outputFilePaths">输出文件路径</param>
        /// <param name="timeout">超时时间(毫秒)</param>
        /// <returns></returns>
        private bool WaitGenerateObfuscationedFile(IReadOnlyCollection<string> outputFilePaths, int timeout)
        {
            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
            var task = System.Threading.Tasks.Task.Run(async () =>
            {
                Dictionary<string, bool> generatedFiles = outputFilePaths.ToDictionary(x => x, x => false);
                const int delay = 500;
                int checkCount = 0;
                int delayMillisecondsTotal = 0;
                while (!cts.IsCancellationRequested)
                {
                    LogMessageFromText("Checking if the obfuscation file has been generated...");

                    string[] tempGeneratedFiles = generatedFiles.Where(x => !x.Value).Select(x => x.Key).ToArray();
                    foreach (string filePath in tempGeneratedFiles)
                    {
                        if (File.Exists(filePath))
                        {
                            generatedFiles[filePath] = true;
                            LogMessageFromText($"The obfuscation file '{filePath}' has been generated");
                        }
                    }

                    if (generatedFiles.All(x => x.Value))
                    {
                        LogMessageFromText("All obfuscation files has been generated");
                        return;
                    }

                    await System.Threading.Tasks.Task.Delay(delay);
                    checkCount++;
                    delayMillisecondsTotal += delay;
                    LogMessageFromText($"Checked count: {checkCount}, and waited {delayMillisecondsTotal} milliseconds.");
                }
            }, cts.Token);

            task.Wait();

            if (cts.IsCancellationRequested)
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// 使用用户指定的消息重要性级别输出消息
        /// </summary>
        /// <param name="message">消息</param>
        private void LogMessageFromText(string message)
        {
            Log.LogMessageFromText(message, _messageImportance);
        }

        /// <summary>
        /// 输出全局属性列表
        /// </summary>
        private void PrintGlobalProperties()
        {
            /*
            * Global properties: 
            * SolutionFileName: Obfuscation.Tasks.sln
            * LangName: en-US
            * CurrentSolutionConfigurationContents: <SolutionConfiguration>
            * <ProjectConfiguration Project="{07b4b880-5ac9-43a9-9a75-fa3f6ded09ec}" AbsolutePath="D:\repo\Obfuscation.Tasks\src\Obfuscation.Tasks\Obfuscation.Tasks.csproj">Debug|AnyCPU</ProjectConfiguration>
            * <ProjectConfiguration Project="{f051efee-47be-4eac-9277-ac14c07b6e6f}" AbsolutePath="D:\repo\Obfuscation.Tasks\src\Obfuscation.Tasks\Obfuscation.Tasks.csproj">Debug|AnyCPU</ProjectConfiguration>
            * <ProjectConfiguration Project="{004f8b03-e695-4023-849e-a49b5e3b4f91}" AbsolutePath="D:\repo\Obfuscation.Tasks\samples\ObfuscationSamples\ObfuscationSamples.csproj">Debug|AnyCPU</ProjectConfiguration>
            * </SolutionConfiguration>
            * Configuration: Debug
            * LangID: 1033
            * SolutionDir: D:\repo\Obfuscation.Tasks\
            * SolutionExt: .sln
            * BuildingInsideVisualStudio: true
            * UseHostCompilerIfAvailable: false
            * DefineExplicitDefaults: true
            * Platform: AnyCPU
            * SolutionPath: D:\repo\Obfuscation.Tasks\Obfuscation.Tasks.sln
            * SolutionName: Obfuscation.Tasks
            * VSIDEResolvedNonMSBuildProjectOutputs: <VSIDEResolvedNonMSBuildProjectOutputs />
            * DevEnvDir: C:\Program Files\Microsoft Visual Studio\2022\Enterprise\Common7\IDE\
            */
            var props = BuildEngine6.GetGlobalProperties();
            StringBuilder sb = StringBuilderPool.Get();
            sb.AppendLine("Global properties: ");
            foreach (var prop in props)
            {
                sb.AppendLine($"{prop.Key}: {prop.Value}");
            }
            Log.LogWarning(sb.ToString());
            StringBuilderPool.Return(sb);
        }

#if NET472
        /// <summary>
        /// 输出项目属性列表
        /// </summary>
        private void PrintProjectProperties()
        {
            var properties = BuildEngine.GetProjectProperties();

            StringBuilder sb = StringBuilderPool.Get();
            sb.AppendLine("Evaluated properties: ");
            foreach (var item in properties)
            {
                sb.AppendLine($"{item.Key}: {item.Value}");
            }
            Log.LogWarning(sb.ToString());
            StringBuilderPool.Return(sb);

            string? targetPath = properties.Where(x => x.Key == "TargetPath").Select(x => x.Value).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(targetPath))
            {
                Log.LogWarning("TargetPath: " + targetPath);
            }
        }

        /// <summary>
        /// 输出编译上下文的环境变量列表
        /// </summary>
        private void PrintEnvironmentVariables()
        {
            var props = BuildEngine.GetEnvironmentVariables();
            StringBuilder sb = StringBuilderPool.Get();
            sb.AppendLine("Environment variables: ");
            foreach (var prop in props)
            {
                sb.AppendLine($"{prop.Key}: {prop.Value}");
            }
            Log.LogWarning(sb.ToString());
            StringBuilderPool.Return(sb);
        }
#endif
    }
}
