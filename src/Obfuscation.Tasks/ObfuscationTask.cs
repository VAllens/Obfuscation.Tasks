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
        /// 混淆工具目录
        /// </summary>
        //[Required]
        public string? ToolDir { get; set; }

        /// <summary>
        /// 要混淆的程序集文件
        /// </summary>
        //[Required]
        public string? InputFilePath { get; set; }

        /// <summary>
        /// 依赖的文件
        /// </summary>
        public string? DependencyFiles { get; set; }

        /// <summary>
        /// 依赖的文件列表
        /// </summary>
        private IEnumerable<string> _dependencyFiles = Enumerable.Empty<string>();

        /// <summary>
        /// 被混淆后输出的程序集文件路径，可选。默认值：<see cref="InputFilePath"/> 的文件名+<see cref="ObfuscateFileNameSuffix"/>。例如：D:\InputFilePath\ObfuscationSamples_Secure.dll
        /// </summary>
        public string? OutputFilePath { get; set; }

        /// <summary>
        /// 等待混淆输出程序集文件的超时时间(毫秒)，默认值：30000 (30秒)。
        /// </summary>
        public int TimeoutMillisecond { get; set; }

        /// <summary>
        /// 消息重要性级别，可选。默认值：<see cref="MessageImportance.Normal"/>。枚举值详见：<see cref="MessageImportance"/>。
        /// </summary>
        public string Importance { get; set; } = nameof(MessageImportance.Normal);

        /// <summary>
        /// 消息重要性级别。默认值：<see cref="MessageImportance.Normal"/>。
        /// </summary>
        private MessageImportance _messageImportance = MessageImportance.Normal;

        private const string OBFUSCATE_FILENAME_SUFFIX = "_Secure";

        /// <summary>
        /// 混淆文件名后缀。默认值: _Secure
        /// </summary>
        public string ObfuscateFileNameSuffix { get; set; } = OBFUSCATE_FILENAME_SUFFIX;

        /// <summary>
        /// 返回混淆后的程序集文件路径。
        /// 等同 <see cref="OutputFilePath"/>，不同之处在于该属性将被 <see cref="Task"/> 返回给被引用的项目上下文
        /// </summary>
        [Output]
        public string? ObfuscationedFilePath { get; set; }

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
                Log.LogError($"{nameof(ToolDir)} does not exist. {nameof(ToolDir)}: {ToolDir}");
                return false;
            }

            //InputFilePath
            if (string.IsNullOrWhiteSpace(InputFilePath))
            {
#if NET472
                string? targetPath = BuildEngine.GetPropertyValue("TargetPath");
#else
                string? targetPath = null;
#endif
                if (string.IsNullOrWhiteSpace(targetPath))
                {
                    Log.LogError($"Please configure {nameof(InputFilePath)}. Example: D:\\sources\\ObfuscationSamples\\ObfuscationSamples\\bin\\Release\\ObfuscationSamples.dll");
                    return false;
                }

                Log.LogWarning($"{nameof(InputFilePath)} is not configured, the default value will be used: {InputFilePath}.");
            }
            InputFilePath = InputFilePath!.Trim();

            if (File.Exists(InputFilePath) == false)
            {
                Log.LogError($"{nameof(InputFilePath)} does not exist. {nameof(InputFilePath)}: {InputFilePath}");
                return false;
            }

            //DependencyFiles
            if (!string.IsNullOrWhiteSpace(DependencyFiles))
            {
                DependencyFiles = DependencyFiles!.Trim();
                string[] dependencyFiles = DependencyFiles.Split(';');
                if (dependencyFiles.Length > 0)
                {
                    foreach (string dependencyFile in dependencyFiles)
                    {
                        if (!File.Exists(dependencyFile))
                        {
                            Log.LogError($"{nameof(DependencyFiles)} does not exist. {nameof(dependencyFile)}: {dependencyFile}");
                            return false;
                        }
                    }

                    _dependencyFiles = dependencyFiles;
                }
            }

            //OutputFilePath
            if (string.IsNullOrWhiteSpace(OutputFilePath))
            {
                string dir = Path.GetDirectoryName(InputFilePath);
                string ext = Path.GetExtension(InputFilePath);
                string fileName = Path.GetFileNameWithoutExtension(InputFilePath);
                string newFileName = $"{fileName}{ObfuscateFileNameSuffix}{ext}";
                OutputFilePath = Path.Combine(dir, newFileName);

                Log.LogWarning($"{nameof(OutputFilePath)} is not configured, the default value will be used: {OutputFilePath}.");
            }
            else
            {
                OutputFilePath = OutputFilePath!.Trim();
                string outDir = Path.GetDirectoryName(OutputFilePath);
                if (Directory.Exists(outDir) == false)
                {
                    LogMessageFromText($"Creating Output file directory: {outDir}");
                    Directory.CreateDirectory(outDir);
                }
            }

            //TimeoutMillisecond
            if (TimeoutMillisecond <= 0)
            {
                TimeoutMillisecond = 30 * 1000;
                Log.LogWarning($"{nameof(TimeoutMillisecond)} is not configured, the default value will be used: {TimeoutMillisecond}.");
            }

            //Importance
            if (!string.IsNullOrWhiteSpace(Importance) && Enum.TryParse(Importance!.Trim(), true, out MessageImportance importance))
            {
                _messageImportance = importance;
            }

            //ObfuscateFileNameSuffix
            if (string.IsNullOrWhiteSpace(ObfuscateFileNameSuffix))
            {
                ObfuscateFileNameSuffix = OBFUSCATE_FILENAME_SUFFIX;
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
                //Copy dependency file to obfuscation tool dir
                List<string> obfuscationDependencyFilePaths = new List<string>();
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

                //Delete obfuscationed dll in out dir
                string newFileName = $"{Path.GetFileNameWithoutExtension(InputFilePath)}{ObfuscateFileNameSuffix}{Path.GetExtension(InputFilePath)}";
                string obfuscationOutputFilePath = Path.Combine(ToolDir, newFileName);
                LogMessageFromText($"Deleting obfuscationed file in {nameof(ToolDir)}: {obfuscationOutputFilePath}");
                File.Delete(obfuscationOutputFilePath);

                //Copy input file to obfuscation tool dir
                string obfuscationInputFilePath = Path.Combine(ToolDir, Path.GetFileName(InputFilePath));
                LogMessageFromText($"Copying {nameof(InputFilePath)}: {InputFilePath} => {obfuscationInputFilePath}");
                File.Copy(InputFilePath, obfuscationInputFilePath, overwrite: true);

                //Wait obfuscation file generated
                LogMessageFromText($"Waiting generate obfuscation File");
                bool result = WaitGenerateObfuscationedFile(obfuscationOutputFilePath, TimeoutMillisecond);
                if (!result)
                {
                    Log.LogError($"Obfuscation task waiting for obfuscationed file output timeout. {nameof(TimeoutMillisecond)}: {TimeoutMillisecond}. Can't find the obfuscationed file path: {obfuscationOutputFilePath}.");
                    return false;
                }

                //Delete input file in obfuscation tool dir
                LogMessageFromText($"Deleting input file in {nameof(ToolDir)}: {obfuscationInputFilePath}.");
                File.Delete(obfuscationInputFilePath);

                //Delete dependency file in obfuscation tool dir
                if (obfuscationDependencyFilePaths.Any())
                {
                    foreach (string dependencyFile in obfuscationDependencyFilePaths)
                    {
                        LogMessageFromText($"Deleting dependency file in {nameof(ToolDir)}: {dependencyFile}.");
                        File.Delete(dependencyFile);

                        string newDependencyFileName = $"{Path.GetFileNameWithoutExtension(dependencyFile)}{ObfuscateFileNameSuffix}{Path.GetExtension(dependencyFile)}";
                        string obfuscationDependencyFilePath = Path.Combine(ToolDir, newDependencyFileName);
                        LogMessageFromText($"Deleting obfuscationed‘s dependency file in {nameof(ToolDir)}: {obfuscationDependencyFilePath}.");
                        File.Delete(obfuscationDependencyFilePath);
                    }
                }

                //Move out file to out path
                LogMessageFromText($"Moving Obfuscation output file path: {obfuscationOutputFilePath} => {OutputFilePath}");
                File.Delete(OutputFilePath);
                File.Move(obfuscationOutputFilePath, OutputFilePath);

                //Output parameter
                ObfuscationedFilePath = OutputFilePath;
                LogMessageFromText($"Obfuscation completed");
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
        /// <param name="outputFilePath">输出文件路径</param>
        /// <param name="timeout">超时时间(毫秒)</param>
        /// <returns></returns>
        private bool WaitGenerateObfuscationedFile(string outputFilePath, int timeout)
        {
            var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromMilliseconds(timeout));
            var task = System.Threading.Tasks.Task.Run(async () =>
            {
                const int delay = 500;
                int checkCount = 0;
                int delayMillisecondsTotal = 0;
                while (!cts.IsCancellationRequested)
                {
                    LogMessageFromText("Checking if the obfuscation file has been generated.");
                    if (File.Exists(outputFilePath))
                    {
                        LogMessageFromText("The obfuscation file has been generated");
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
