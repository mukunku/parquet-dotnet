using System;
using Parquet.Test.Integration;

namespace Parquet.Test.Xunit;

/// <summary>
/// A test that needs Python with pyarrow. Skipped on macOS (like <see cref="NonMacOSFactAttribute"/>) and, outside of
/// CI, when no suitable Python is found. On CI (CI=true) it always runs, so a broken setup fails instead of hiding.
/// </summary>
public class PyArrowFactAttribute : NonMacOSFactAttribute {
    public PyArrowFactAttribute() {
        bool isCi = string.Equals(Environment.GetEnvironmentVariable("CI"), "true", StringComparison.OrdinalIgnoreCase);
        if((Skip == null) && !isCi && (PyArrowPython.Executable == null)) {
            Skip = "Python with pyarrow not found (tried python3 and python on PATH).";
        }
    }
}
