using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using AScheduler.ConfigWizard.Wpf.Models;

namespace AScheduler.ConfigWizard.Wpf.Services;

/// <summary>
/// Applies wizard values to backend/frontend configuration files and environment variables.
/// </summary>
public sealed class ConfigurationApplyService
{
    public void ApplyConfiguration(WizardState state)
    {
        var backupTargets = new List<string>();

        string? appsettingsBasePath = null;
        string? appsettingsProductionPath = null;
        if (state.ConfigureBackend)
        {
            if (string.IsNullOrWhiteSpace(state.BackendFolderPath) || !Directory.Exists(state.BackendFolderPath))
            {
                throw new DirectoryNotFoundException("La carpeta backend seleccionada no existe.");
            }

            appsettingsBasePath = Path.Combine(state.BackendFolderPath, "appsettings.json");
            appsettingsProductionPath = Path.Combine(state.BackendFolderPath, "appsettings.Production.json");

            if (!File.Exists(appsettingsBasePath))
            {
                throw new FileNotFoundException("No se encontro appsettings.json en la carpeta backend seleccionada.", appsettingsBasePath);
            }

            if (File.Exists(appsettingsProductionPath))
            {
                backupTargets.Add(appsettingsProductionPath);
            }
        }

        string? frontendConfigPath = null;
        if (state.ConfigureFrontend)
        {
            if (string.IsNullOrWhiteSpace(state.FrontendFolderPath) || !Directory.Exists(state.FrontendFolderPath))
            {
                throw new DirectoryNotFoundException("La carpeta frontend seleccionada no existe.");
            }

            frontendConfigPath = ResolveFrontendConfigPath(state.FrontendFolderPath);
            if (File.Exists(frontendConfigPath))
            {
                backupTargets.Add(frontendConfigPath);
            }
        }

        if (!state.ConfigureBackend && !state.ConfigureFrontend)
        {
            return;
        }

        var effectiveJwtSecret = state.GenerateJwtSecret
            ? SecretGenerator.GenerateBase64Secret()
            : state.JwtSecret.Trim();

        if (state.ConfigureBackend && string.IsNullOrWhiteSpace(effectiveJwtSecret))
        {
            throw new InvalidOperationException("JWT secret es obligatorio.");
        }

        var backups = CreateBackups(backupTargets);

        try
        {
            if (state.ConfigureBackend)
            {
                var appSettingsJson = JsonNode.Parse(File.ReadAllText(appsettingsBasePath!))?.AsObject()
                                      ?? throw new InvalidOperationException("No se pudo parsear appsettings.json");

                var connectionStringsNode = GetOrCreateObject(appSettingsJson, "ConnectionStrings");
                var jwtNode = GetOrCreateObject(appSettingsJson, "Jwt");
                connectionStringsNode["Default"] = state.ConnectionString;
                jwtNode["Secret"] = "__FROM_ENV__";

                SaveJson(appsettingsProductionPath!, appSettingsJson);
            }

            if (state.ConfigureFrontend)
            {
                JsonObject frontendConfig;
                if (File.Exists(frontendConfigPath!))
                {
                    frontendConfig = JsonNode.Parse(File.ReadAllText(frontendConfigPath!))?.AsObject()
                                     ?? throw new InvalidOperationException("No se pudo parsear config.json del frontend.");
                }
                else
                {
                    frontendConfig = new JsonObject();
                }

                frontendConfig["port"] = state.FrontendPort;
                frontendConfig["backendUrl"] = state.BackendUrl;
                SaveJson(frontendConfigPath!, frontendConfig);
            }

            if (state.ConfigureBackend)
            {
                var scope = state.UseMachineScope
                    ? EnvironmentVariableTarget.Machine
                    : EnvironmentVariableTarget.User;

                try
                {
                    SetEnvironmentVariableIfAllowed("ASCHEDULER_JWT_SECRET", effectiveJwtSecret, scope, state.OverwriteJwtSecret);
                    SetEnvironmentVariableIfAllowed("DOTNET_ENVIRONMENT", "Production", scope, state.OverwriteDotNetEnvironment);
                    SetEnvironmentVariableIfAllowed("ASPNETCORE_URLS", state.AspNetCoreUrls, scope, state.OverwriteAspNetCoreUrls);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new InvalidOperationException(
                        "No hay permisos para escribir variables de entorno en scope de maquina. Ejecuta como administrador o cambia a scope User.",
                        ex);
                }
            }
        }
        catch
        {
            RestoreBackups(backups);
            throw;
        }
    }

    private static Dictionary<string, string> CreateBackups(IEnumerable<string> filePaths)
    {
        var backups = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var path in filePaths)
        {
            var backupPath = $"{path}.bak.{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}-{Guid.NewGuid():N}";
            File.Copy(path, backupPath, false);
            backups[path] = backupPath;
        }

        return backups;
    }

    private static void RestoreBackups(IReadOnlyDictionary<string, string> backups)
    {
        var errors = new List<string>();

        foreach (var pair in backups)
        {
            try
            {
                File.Copy(pair.Value, pair.Key, true);
            }
            catch (Exception ex)
            {
                errors.Add($"{pair.Key}: {ex.Message}");
            }
        }

        if (errors.Count > 0)
        {
            throw new InvalidOperationException(
                "Fallo parcial durante la restauracion de backups: " + string.Join(" | ", errors));
        }
    }

    private static void SaveJson(string filePath, JsonNode jsonNode)
    {
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrWhiteSpace(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = jsonNode.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(filePath, json + Environment.NewLine);
    }

    private static string ResolveFrontendConfigPath(string frontendFolderPath)
    {
        var rootConfigPath = Path.Combine(frontendFolderPath, "config.json");
        if (File.Exists(rootConfigPath))
        {
            return rootConfigPath;
        }

        var publicConfigPath = Path.Combine(frontendFolderPath, "public", "config.json");
        if (File.Exists(publicConfigPath))
        {
            return publicConfigPath;
        }

        // If no file exists yet, prefer root-level config for release layouts.
        return rootConfigPath;
    }

    private static JsonObject GetOrCreateObject(JsonObject root, string propertyName)
    {
        if (root[propertyName] is null)
        {
            var created = new JsonObject();
            root[propertyName] = created;
            return created;
        }

        if (root[propertyName] is JsonObject existing)
        {
            return existing;
        }

        throw new InvalidOperationException(
            $"La propiedad '{propertyName}' existe pero no es un objeto JSON. Revisa appsettings.json antes de aplicar.");
    }

    private static void SetEnvironmentVariableIfAllowed(
        string variableName,
        string value,
        EnvironmentVariableTarget scope,
        bool allowOverwrite)
    {
        var existingValue = Environment.GetEnvironmentVariable(variableName, scope);
        if (!allowOverwrite && !string.IsNullOrWhiteSpace(existingValue))
        {
            return;
        }

        Environment.SetEnvironmentVariable(variableName, value, scope);
    }
}
