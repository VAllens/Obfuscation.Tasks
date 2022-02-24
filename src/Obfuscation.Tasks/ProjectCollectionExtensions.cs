using System.Collections.Generic;
using System.Linq;
using Microsoft.Build.Framework;

namespace Obfuscation.Tasks
{
#if NET472
    /// <summary>
    /// ProjectCollection 扩展类
    /// <para>See: https://stackoverflow.com/questions/54434663/microsoft-build-evaluation-projectcollection-loadproject-method-throws-microsoft</para>
    /// </summary>
    internal static class ProjectCollectionExtensions
    {
        public static string? GetPropertyValue(this IBuildEngine buildEngine, string name)
        {
            return buildEngine.GetProjectProperties().Where(x => x.Key == name).Select(x => x.Value).FirstOrDefault();
        }

        public static IEnumerable<string> GetPropertyValues(this IBuildEngine buildEngine, string name)
        {
            return buildEngine.GetProjectProperties().Where(x => x.Key == name).Select(x => x.Value);
        }

        public static IEnumerable<KeyValuePair<string, string>> GetProjectProperties(this IBuildEngine buildEngine)
        {
            string projectFile = buildEngine.ProjectFileOfTaskNode;
            //var projectRootElement = ProjectRootElement.Open(projectFile);
            var projectCollection = new Microsoft.Build.Evaluation.ProjectCollection();
            var project = projectCollection.LoadProject(projectFile);
            if (project == null)
            {
                return Enumerable.Empty<KeyValuePair<string, string>>();
            }

            return project.AllEvaluatedProperties.Select(x => new KeyValuePair<string, string>(x.Name, x.EvaluatedValue));
        }
    }
#endif
}