using System;
using System.Diagnostics;
using System.Text;

namespace Xtension
{
    internal static class ScanSafety
    {
        // Includes worker time AND time waiting for the UI message loop to deliver the result.
        public static bool IsFresh(long startedMs, long nowMs, long limitMs)
        {
            return startedMs != Clock.Never && nowMs >= startedMs && nowMs - startedMs < limitMs;
        }

        /// <summary>Allowlisted metadata only. Provider messages, Data, file paths and inner exceptions may contain browsing data.</summary>
        public static string DescribeError(string operation, Exception error)
        {
            StringBuilder text = new StringBuilder();
            text.Append(operation).Append(": ").Append(error.GetType().Name)
                .Append(" (HRESULT 0x").Append(error.HResult.ToString("X8")).Append(")");
            StackFrame[] frames = new StackTrace(error, false).GetFrames();
            int count = 0;
            if (frames != null)
            {
                foreach (StackFrame frame in frames)
                {
                    System.Reflection.MethodBase method = frame.GetMethod();
                    if (method == null || method.DeclaringType == null || method.DeclaringType.Namespace != "Xtension") continue;
                    text.Append(count == 0 ? "; app stack: " : " <- ")
                        .Append(method.DeclaringType.Name).Append('.').Append(method.Name);
                    if (++count == 4) break;
                }
            }
            return text.ToString();
        }
    }
}
