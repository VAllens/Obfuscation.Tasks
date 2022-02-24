using System.Collections.Concurrent;
using System.Text;

namespace Obfuscation.Tasks
{
    internal static class StringBuilderPool
    {
        private const int maxPooledStringBuilders = 64;
        private static readonly ConcurrentQueue<StringBuilder> freeStringBuilders = new();

        public static StringBuilder Get()
        {
            if (freeStringBuilders.TryDequeue(out StringBuilder? sb))
            {
                return sb;
            }

            return new StringBuilder();
        }

        public static void Return(StringBuilder sb)
        {
            //System.Diagnostics.Trace.Assert(sb != null, $"'{nameof(sb)}' MUST NOT be NULL.");

            if (freeStringBuilders.Count <= maxPooledStringBuilders)
            {
                // There is a race condition here so the count could be off a little bit (but insignificantly)
                sb.Clear();
                freeStringBuilders.Enqueue(sb);
            }
        }
    }
}
