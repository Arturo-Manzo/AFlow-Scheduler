namespace AScheduler.ConfigWizard.Wpf.Models;

/// <summary>
/// Holds preflight diagnostics and whether the wizard can continue.
/// </summary>
public sealed class PreflightResult
{
    public required string DotnetStatus { get; init; }

    public required string SqlStatus { get; init; }

    public required string FrontendPortStatus { get; init; }

    public required string BackendPortStatus { get; init; }

    public bool DotnetChecked { get; init; }

    public bool SqlChecked { get; init; }

    public bool FrontendPortChecked { get; init; }

    public bool BackendPortChecked { get; init; }

    public bool CanContinue { get; init; }

    public string ToDisplayText()
    {
        return $".NET: {DotnetStatus}\n" +
               $"SQL: {SqlStatus}\n" +
               $"Puerto frontend: {FrontendPortStatus}\n" +
               $"Puerto backend: {BackendPortStatus}";
    }
}
