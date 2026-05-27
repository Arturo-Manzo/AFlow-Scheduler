using CHRONIQ.Execution;

namespace CHRONIQ.Tests;

public class NetworkPathWarmupTests
{
    [Theory]
    [InlineData("\"\\\\server\\share\\folder\\job.bat\" --flag", "\\\\server\\share\\folder\\job.bat")]
    [InlineData("\"C:\\Program Files\\Tools\\run.exe\" /q", "C:\\Program Files\\Tools\\run.exe")]
    [InlineData("Z:\\jobs\\nightly.bat arg1", "Z:\\jobs\\nightly.bat")]
    [InlineData("python script.py", "python")]
    public void ExtractPathCandidate_ReturnsLaunchTarget(string command, string expected)
    {
        var candidate = NetworkPathWarmup.ExtractPathCandidate(command);

        Assert.Equal(expected, candidate);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\"unterminated")]
    public void ExtractPathCandidate_ReturnsNullForInvalidInput(string command)
    {
        var candidate = NetworkPathWarmup.ExtractPathCandidate(command);

        Assert.Null(candidate);
    }
}
