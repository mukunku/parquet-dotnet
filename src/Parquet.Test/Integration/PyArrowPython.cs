using System;

namespace Parquet.Test.Integration;

/// <summary>
/// Finds a Python interpreter that has pyarrow installed, for integration tests that check pyarrow can read files
/// written by Parquet.Net. Resolved once per test run.
/// </summary>
static class PyArrowPython {

    // python3 first: on Linux and macOS "python" may be missing or Python 2. On Windows python.org installers only
    // create python.exe, and python3.exe is often just the Microsoft Store placeholder, which HasPyArrow rules out.
    private static readonly string[] Candidates = ["python3", "python"];

    private static readonly Lazy<string?> _executable = new(Resolve);

    /// <summary>
    /// The interpreter to use, or null when none is found.
    /// </summary>
    public static string? Executable => _executable.Value;

    private static string? Resolve() {
        foreach(string candidate in Candidates) {
            if(HasPyArrow(candidate))
                return candidate;
        }
        return null;
    }

    /// <summary>
    /// True when the executable starts and can import pyarrow. This also rules out the Windows "App execution alias"
    /// placeholders, which exist on PATH but only print "Python was not found" and exit with 9009.
    /// </summary>
    private static bool HasPyArrow(string executable) {
        try {
            IntegrationBase.ExecAndGetOutput(executable, "-c \"import pyarrow\"");
            return true;
        } catch {
            return false;
        }
    }
}
