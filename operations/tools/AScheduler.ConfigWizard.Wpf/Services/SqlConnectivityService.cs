using Microsoft.Data.SqlClient;

namespace AScheduler.ConfigWizard.Wpf.Services;

/// <summary>
/// Validates SQL Server connectivity with the exact connection string selected by the user.
/// </summary>
public sealed class SqlConnectivityService
{
    public async Task<(bool IsSuccess, string Message)> ValidateConnectionAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return (false, "Connection string vacia.");
        }

        try
        {
            _ = new SqlConnectionStringBuilder(connectionString);
        }
        catch (Exception ex)
        {
            return (false, $"Formato de connection string invalido: {ex.Message}");
        }

        try
        {
            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync().ConfigureAwait(false);
            return (true, $"Conexion SQL validada correctamente con {connection.DataSource}.");
        }
        catch (Exception ex)
        {
            return (false, $"No se pudo conectar a SQL con la connection string seleccionada: {ex.Message}");
        }
    }
}
