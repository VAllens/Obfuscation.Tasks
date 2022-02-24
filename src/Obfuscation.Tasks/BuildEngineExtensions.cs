using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Build.Framework;

namespace Obfuscation.Tasks
{
#if NET472
    /// <summary>
    /// BuildEngine 扩展类 (黑魔法、慎用)
    /// <para>See: https://stackoverflow.com/questions/3043531/when-implementing-a-microsoft-build-utilities-task-how-to-i-get-access-to-the-va</para>
    /// </summary>
    [Obsolete($"Please use {nameof(ProjectCollectionExtensions)}")]
    internal static class BuildEngineExtensions
    {
        private const BindingFlags bindingFlags = BindingFlags.NonPublic | BindingFlags.FlattenHierarchy | BindingFlags.Instance | BindingFlags.Public;

        /// <summary>
        /// 尝试获取项目编译上下文的一个环境变量
        /// </summary>
        /// <param name="buildEngine">编译引擎</param>
        /// <param name="key">变量名</param>
        /// <param name="value">变量值</param>
        /// <returns>true，表示存在该变量，并且获取成功。</returns>
        public static bool TryGetEnvironmentVariable(this IBuildEngine buildEngine, string key, out string? value)
        {
            try
            {
                value = buildEngine.GetEnvironmentVariable(key, true);
                return true;
            }
            catch
            {
                value = string.Empty;
                return false;
            }
        }

        /// <summary>
        /// 获取项目编译上下文的一个环境变量
        /// </summary>
        /// <param name="buildEngine">编译引擎</param>
        /// <param name="key">变量名</param>
        /// <param name="throwIfNotFound">是否抛出 <see cref="KeyNotFoundException"/> 异常</param>
        /// <returns>返回环境变量值</returns>
        /// <exception cref="KeyNotFoundException"></exception>
        public static string? GetEnvironmentVariable(this IBuildEngine buildEngine, string key, bool throwIfNotFound)
        {
            var dicts = buildEngine.GetEnvironmentVariables();

            KeyValuePair<string, string>? keyValuePair = dicts.Where(x => string.Equals(x.Key, key, StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            if (keyValuePair.HasValue)
            {
                return keyValuePair.Value.Value;
            }

            if (throwIfNotFound)
            {
                throw new KeyNotFoundException(string.Format("Could not extract from '{0}' environmental variables.", key));
            }

            return null;
        }

        /// <summary>
        /// 获取项目编译上下文的环境变量列表
        /// </summary>
        /// <param name="buildEngine">编译引擎</param>
        /// <returns>返回环境变量列表</returns>
        public static IEnumerable<KeyValuePair<string, string>> GetEnvironmentVariables(this IBuildEngine buildEngine)
        {
            var projectInstance = GetProjectInstance(buildEngine);

            var items = projectInstance.Items;
            if (items.Count > 0)
            {
                return items.Select(x => new KeyValuePair<string, string>(x.ItemType, x.EvaluatedInclude));
            }

            var properties = projectInstance.Properties;
            if (properties.Count > 0)
            {
                return properties.Select(x => new KeyValuePair<string, string>(x.Name, x.EvaluatedValue));
            }

            return Enumerable.Empty<KeyValuePair<string, string>>();
        }

        private static Microsoft.Build.Execution.ProjectInstance GetProjectInstance(IBuildEngine buildEngine)
        {
            var buildEngineType = buildEngine.GetType();
            var targetBuilderCallbackField = buildEngineType.GetFields(bindingFlags)
                .Where(x => x.Name.Equals("_targetBuilderCallback", StringComparison.InvariantCultureIgnoreCase) || x.Name.Equals("targetBuilderCallback", StringComparison.InvariantCultureIgnoreCase))
                .FirstOrDefault();
            if (targetBuilderCallbackField == null)
            {
                throw new ArgumentNullException("Could not extract targetBuilderCallback from " + buildEngineType.FullName);
            }

            var targetBuilderCallback = targetBuilderCallbackField.GetValue(buildEngine);
            var targetCallbackType = targetBuilderCallback.GetType();
            var projectInstanceField = targetCallbackType.GetFields(bindingFlags)
                .Where(x => x.Name.Equals("_projectInstance", StringComparison.InvariantCultureIgnoreCase) || x.Name.Equals("projectInstance", StringComparison.InvariantCultureIgnoreCase))
                .FirstOrDefault();
            if (projectInstanceField == null)
            {
                throw new ArgumentNullException("Could not extract projectInstance from " + targetCallbackType.FullName);
            }

            return (Microsoft.Build.Execution.ProjectInstance)projectInstanceField.GetValue(targetBuilderCallback);
        }
    }
#endif
}