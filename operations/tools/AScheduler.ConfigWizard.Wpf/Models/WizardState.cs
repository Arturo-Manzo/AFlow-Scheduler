using System.Text;

namespace AScheduler.ConfigWizard.Wpf.Models;

/// <summary>
/// Stores user selections across wizard steps.
/// </summary>
public sealed class WizardState
{
    public bool ConfigureFrontend { get; set; } = true;

    public bool ConfigureBackend { get; set; } = true;

    public bool ConfigureDatabase { get; set; } = true;

    public string BackendFolderPath { get; set; } = string.Empty;

    public string FrontendFolderPath { get; set; } = string.Empty;

    public string SqlServer { get; set; } = "(localdb)\\MSSQLLocalDB";

    public int FrontendPort { get; set; } = 4000;

    public int BackendPort { get; set; } = 5000;

    public bool GenerateJwtSecret { get; set; } = true;

    public string JwtSecret { get; set; } = string.Empty;

    public string ConnectionString { get; set; } = string.Empty;

    public string BackendUrl { get; set; } = "http://localhost:5000/api";

    public string AspNetCoreUrls { get; set; } = "http://localhost:5000";

    public bool UseMachineScope { get; set; }

    public bool DatabaseValidationPassed { get; set; }

    public string DatabaseValidationStatus { get; set; } = "Pendiente";

    public string EnvironmentValidationStatus { get; set; } = "Pendiente";

    public int ExistingEnvironmentVariablesCount { get; set; }

    public string EnvironmentVariablePlan { get; set; } = "Sin evaluar";

    public bool OverwriteJwtSecret { get; set; }

    public bool OverwriteDotNetEnvironment { get; set; }

    public bool OverwriteAspNetCoreUrls { get; set; }

    public List<string> InitialWarnings { get; } = [];

    public string BuildPreviewSummary()
    {
        var scope = UseMachineScope ? "Machine" : "User";
        var builder = new StringBuilder();
        builder.AppendLine("Modulos seleccionados:");
        builder.AppendLine($"- Frontend: {(ConfigureFrontend ? "Si" : "No")}");
        builder.AppendLine($"- Backend: {(ConfigureBackend ? "Si" : "No")}");
        builder.AppendLine();
        builder.AppendLine("Se aplicaran los siguientes cambios:");
        builder.AppendLine($"- SQL Server: {SqlServer}");
        if (ConfigureBackend)
        {
            builder.AppendLine($"- Carpeta backend: {BackendFolderPath}");
            builder.AppendLine($"- Backend port: {BackendPort}");
            builder.AppendLine($"- Connection string: {ConnectionString}");
            builder.AppendLine($"- JWT: {(GenerateJwtSecret ? "Auto-generado" : "Manual (oculto)")}");
            builder.AppendLine($"- Scope variables de entorno: {scope}");
            builder.AppendLine($"- ASPNETCORE_URLS deseado: {AspNetCoreUrls}");
            builder.AppendLine(
                $"- Sobrescritura por variable: ASCHEDULER_JWT_SECRET={(OverwriteJwtSecret ? "Si" : "No")}, " +
                $"DOTNET_ENVIRONMENT={(OverwriteDotNetEnvironment ? "Si" : "No")}, " +
                $"ASPNETCORE_URLS={(OverwriteAspNetCoreUrls ? "Si" : "No")}");
            builder.AppendLine($"- Estado variables de entorno actuales: {EnvironmentValidationStatus}");
            builder.AppendLine($"- Plan variables de entorno: {EnvironmentVariablePlan}");
            builder.AppendLine("- Variables de entorno objetivo: ASCHEDULER_JWT_SECRET, DOTNET_ENVIRONMENT, ASPNETCORE_URLS");
        }

        if (ConfigureFrontend)
        {
            builder.AppendLine($"- Carpeta frontend: {FrontendFolderPath}");
            builder.AppendLine($"- Frontend port: {FrontendPort}");
            builder.AppendLine($"- Backend URL: {BackendUrl}");
        }

        builder.AppendLine($"- Validacion SQL/Runtime: {DatabaseValidationStatus}");
        builder.AppendLine();
        builder.AppendLine("Archivos objetivo:");
        if (ConfigureBackend)
        {
            builder.AppendLine("- <backend>/appsettings.Production.json");
        }

        if (ConfigureFrontend)
        {
            builder.AppendLine("- <frontend>/config.json o <frontend>/public/config.json");
        }

        if (ConfigureDatabase)
        {
            builder.AppendLine("- Validacion de conectividad SQL en preflight (sin cambios de esquema en esta version)");
        }

        if (InitialWarnings.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine("Advertencias detectadas en paso inicial:");
            foreach (var warning in InitialWarnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }

        return builder.ToString();
    }
}
