// Lightweight diagnostic logging for the sign-in / Key Vault discovery flow.
//
// Every message is prefixed with a stable tag so it can be filtered from
// logcat (Android) or the Debug output:  adb logcat -s KNOXAUTH:* ... or
// simply grep for "KNOXAUTH". Writes to both Debug and Console so the lines
// show up regardless of how the platform routes diagnostic output.
using System;
using System.Diagnostics;

namespace Knox
{
    internal static class AuthLog
    {
        public const string Tag = "KNOXAUTH";

        public static void Write(string message)
        {
            var line = $"{Tag}: {message}";
            Debug.WriteLine(line);
            Console.WriteLine(line);
        }

        public static void Error(string stage, Exception ex)
        {
            Write($"{stage} FAILED: {ex.GetType().Name}: {ex.Message}");
        }
    }
}
