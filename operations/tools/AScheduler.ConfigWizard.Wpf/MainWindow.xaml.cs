using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using AScheduler.ConfigWizard.Wpf.Models;
using AScheduler.ConfigWizard.Wpf.Services;
using Microsoft.Data.SqlClient;
using Forms = System.Windows.Forms;

namespace AScheduler.ConfigWizard.Wpf;

/// <summary>
/// Interaction logic for MainWindow.xaml
/// </summary>
public partial class MainWindow : Window
{
    private readonly PreflightService _preflightService = new();
    private readonly SqlConnectivityService _sqlConnectivityService = new();
    private readonly ConfigurationApplyService _applyService = new();
    private readonly WizardState _state = new();

    private int _currentStepIndex;
    private bool _preflightSuccessful;
    private bool _isApplying;

    private readonly UIElement[] _steps;

    public MainWindow()
    {
        InitializeComponent();
        _steps =
        [
            WelcomeStepPanel,
            PreflightStepPanel,
            BackendStepPanel,
            FrontendStepPanel,
            ReviewStepPanel
        ];

        InitializeDefaults();
        RefreshStepUi();
    }

    private void InitializeDefaults()
    {
        var projectRoot = TryResolveProjectRoot();
        BackendFolderTextBox.Text = projectRoot;
        FrontendFolderTextBox.Text = Path.Combine(projectRoot, "frontend");

        SqlServerTextBox.Text = _state.SqlServer;
        FrontendPortTextBox.Text = _state.FrontendPort.ToString();
        BackendPortTextBox.Text = _state.BackendPort.ToString();
        ConnectionStringTextBox.Text = BuildDefaultConnectionString(_state.SqlServer);
        BackendUrlTextBox.Text = _state.BackendUrl;
        AspNetCoreUrlsTextBox.Text = _state.AspNetCoreUrls;
        EnvVarNamesTextBlock.Text = "ASCHEDULER_JWT_SECRET\nDOTNET_ENVIRONMENT\nASPNETCORE_URLS";
        UpdateApiBaseUrlPreview();
        UpdateEnvironmentVariableStatus();
    }

    private static string TryResolveProjectRoot()
    {
        var current = AppContext.BaseDirectory;
        var directory = new DirectoryInfo(current);

        while (directory is not null)
        {
            var projectFile = Path.Combine(directory.FullName, "AScheduler.csproj");
            if (File.Exists(projectFile))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return Directory.GetCurrentDirectory();
    }

    private static string BuildDefaultConnectionString(string sqlServer)
    {
        return $"Server={sqlServer};Database=ASchedulerDB;Trusted_Connection=True;TrustServerCertificate=True";
    }

    private void BackButton_Click(object sender, RoutedEventArgs e)
    {
        if (_currentStepIndex <= 0)
        {
            return;
        }

        _currentStepIndex = GetPreviousStepIndex(_currentStepIndex);
        RefreshStepUi();
    }

    private async void NextButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (!CaptureStepState())
            {
                return;
            }

            if (_currentStepIndex == _steps.Length - 1)
            {
                await ApplyConfigurationAsync().ConfigureAwait(true);
                return;
            }

            if (_currentStepIndex == 1 && RequiresRuntimeValidation() && !_preflightSuccessful)
            {
                StatusTextBlock.Text = "Valida SQL/runtime antes de continuar.";
                return;
            }

            _currentStepIndex = GetNextStepIndex(_currentStepIndex);
            RefreshStepUi();
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            StatusTextBlock.Text = "Se detecto un error. Revisa los datos e intenta nuevamente.";
        }
    }

    private bool CaptureStepState(bool enforceValidationGate = true)
    {
        if (_currentStepIndex == 0)
        {
            _state.ConfigureFrontend = ConfigureFrontendCheckBox.IsChecked == true;
            _state.ConfigureBackend = ConfigureBackendCheckBox.IsChecked == true;
            _state.ConfigureDatabase = _state.ConfigureBackend;

            if (!_state.ConfigureFrontend && !_state.ConfigureBackend)
            {
                StatusTextBlock.Text = "Selecciona al menos Frontend o Backend para continuar.";
                return false;
            }

            _state.BackendFolderPath = BackendFolderTextBox.Text.Trim();
            _state.FrontendFolderPath = FrontendFolderTextBox.Text.Trim();

            _state.InitialWarnings.Clear();
            if (_state.ConfigureBackend && !ValidateBackendFolder(_state.BackendFolderPath, out var backendMessage))
            {
                _state.InitialWarnings.Add(backendMessage);
            }

            if (_state.ConfigureFrontend && !ValidateFrontendFolder(_state.FrontendFolderPath, out var frontendMessage))
            {
                _state.InitialWarnings.Add(frontendMessage);
            }

            RenderInitialWarnings();

            return true;
        }

        if (_currentStepIndex == 1)
        {
            var frontendPort = _state.FrontendPort;
            if (_state.ConfigureFrontend && (!int.TryParse(FrontendPortTextBox.Text, out frontendPort) || frontendPort is < 1024 or > 65535))
            {
                StatusTextBlock.Text = "Puerto frontend invalido. Usa un valor entre 1024 y 65535.";
                return false;
            }

            var backendPort = _state.BackendPort;
            if (_state.ConfigureBackend && (!int.TryParse(BackendPortTextBox.Text, out backendPort) || backendPort is < 1024 or > 65535))
            {
                StatusTextBlock.Text = "Puerto backend invalido. Usa un valor entre 1024 y 65535.";
                return false;
            }

            _state.SqlServer = SqlServerTextBox.Text.Trim();
            _state.FrontendPort = frontendPort;
            _state.BackendPort = backendPort;
            _state.ConnectionString = ConnectionStringTextBox.Text.Trim();

            if (_state.ConfigureBackend && string.IsNullOrWhiteSpace(_state.SqlServer))
            {
                StatusTextBlock.Text = "Ingresa el servidor SQL.";
                return false;
            }

            if (_state.ConfigureBackend && string.IsNullOrWhiteSpace(_state.ConnectionString))
            {
                StatusTextBlock.Text = "La connection string es obligatoria para Backend/Database.";
                return false;
            }

            if (_state.ConfigureBackend &&
                !ValidateConnectionString(_state.ConnectionString, out var connectionStringValidationMessage))
            {
                StatusTextBlock.Text = connectionStringValidationMessage;
                return false;
            }

            if (enforceValidationGate && RequiresRuntimeValidation() && !_state.DatabaseValidationPassed)
            {
                StatusTextBlock.Text = "Debes ejecutar 'Validate Connection' con resultado exitoso.";
                return false;
            }

            return true;
        }

        if (_currentStepIndex == 2)
        {
            if (!_state.ConfigureBackend)
            {
                return true;
            }

            _state.GenerateJwtSecret = GenerateJwtSecretCheckBox.IsChecked == true;
            _state.UseMachineScope = UseMachineScopeRadioButton.IsChecked == true;
            _state.OverwriteJwtSecret = OverwriteJwtSecretCheckBox.IsChecked == true;
            _state.OverwriteDotNetEnvironment = OverwriteDotNetEnvironmentCheckBox.IsChecked == true;
            _state.OverwriteAspNetCoreUrls = OverwriteAspNetCoreUrlsCheckBox.IsChecked == true;
            _state.AspNetCoreUrls = AspNetCoreUrlsTextBox.Text.Trim();
            _state.JwtSecret = JwtSecretPasswordBox.Password;

            if (!_state.GenerateJwtSecret && !ValidateManualJwt(_state.JwtSecret, out var jwtValidationMessage))
            {
                StatusTextBlock.Text = jwtValidationMessage;
                return false;
            }

            if (!ValidateAspNetCoreUrls(_state.AspNetCoreUrls, out var aspNetCoreUrlsValidationMessage))
            {
                StatusTextBlock.Text = aspNetCoreUrlsValidationMessage;
                return false;
            }

            UpdateEnvironmentVariableStatus();

            return true;
        }

        if (_currentStepIndex == 3)
        {
            if (!_state.ConfigureFrontend)
            {
                return true;
            }

            _state.BackendUrl = BackendUrlTextBox.Text.Trim();
            if (!Uri.TryCreate(_state.BackendUrl, UriKind.Absolute, out _))
            {
                StatusTextBlock.Text = "La URL backend debe ser absoluta (http/https).";
                return false;
            }

            return true;
        }

        if (_currentStepIndex == 4)
        {
            if (ConfirmApplyCheckBox.IsChecked != true)
            {
                StatusTextBlock.Text = "Confirma la revision para aplicar cambios.";
                return false;
            }

            return true;
        }

        return true;
    }

    private void RefreshStepUi()
    {
        for (var index = 0; index < _steps.Length; index++)
        {
            _steps[index].Visibility = index == _currentStepIndex ? Visibility.Visible : Visibility.Collapsed;
        }

        BackButton.IsEnabled = _currentStepIndex > 0;
        NextButton.Content = _currentStepIndex == _steps.Length - 1 ? "Apply" : "Next";

        var visibleSteps = Enumerable.Range(0, _steps.Length).Where(ShouldDisplayStep).ToList();
        var visiblePosition = visibleSteps.IndexOf(_currentStepIndex) + 1;
        StepTitleText.Text = $"Step {visiblePosition} of {visibleSteps.Count}";
        UpdateSideNavState();

        switch (_currentStepIndex)
        {
            case 0:
                StatusTextBlock.Text = "Selecciona modulos y carpetas base.";
                RenderInitialWarnings();
                break;
            case 1:
                StatusTextBlock.Text = "Valida SQL/runtime con la connection string seleccionada.";
                break;
            case 2:
                StatusTextBlock.Text = "Define politica de autenticacion y variables de entorno.";
                UpdateEnvironmentVariableStatus();
                break;
            case 3:
                StatusTextBlock.Text = "Configura endpoint backend para frontend.";
                break;
            case 4:
                PreviewSummaryTextBox.Text = _state.BuildPreviewSummary();
                StatusTextBlock.Text = "Revisa todo y aplica.";
                break;
        }

        SetupStatusTextBlock.Text = StatusTextBlock.Text;

        BackendStepPanel.Visibility = ShouldDisplayStep(2) && _currentStepIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
        FrontendStepPanel.Visibility = ShouldDisplayStep(3) && _currentStepIndex == 3 ? Visibility.Visible : Visibility.Collapsed;
    }

    private async void RunPreflightButton_Click(object sender, RoutedEventArgs e)
    {
        if (!CaptureStepState(enforceValidationGate: false))
        {
            return;
        }

        RunPreflightButton.IsEnabled = false;
        StatusTextBlock.Text = "Ejecutando validaciones...";

        try
        {
            var runtimeResult = await _preflightService
                .RunAsync(
                    _state.SqlServer,
                    _state.FrontendPort,
                    _state.BackendPort,
                    _state.ConfigureFrontend,
                    _state.ConfigureBackend,
                    false)
                .ConfigureAwait(true);

            var sqlStatus = "No seleccionado";
            var sqlOk = true;
            if (_state.ConfigureBackend)
            {
                var sqlValidation = await _sqlConnectivityService.ValidateConnectionAsync(_state.ConnectionString).ConfigureAwait(true);
                sqlOk = sqlValidation.IsSuccess;
                sqlStatus = sqlValidation.Message;
            }

            _preflightSuccessful = runtimeResult.CanContinue && sqlOk;
            _state.DatabaseValidationPassed = _preflightSuccessful;
            _state.DatabaseValidationStatus = _preflightSuccessful ? "Exitosa" : "Fallida";

            PreflightResultTextBox.Text =
                $"SQL: {sqlStatus}\n" +
                $".NET: {runtimeResult.DotnetStatus}\n" +
                $"Puerto frontend: {runtimeResult.FrontendPortStatus}\n" +
                $"Puerto backend: {runtimeResult.BackendPortStatus}";

            PreflightStatusBorder.Background = _preflightSuccessful
                ? CreateBrush("#EAF7EE")
                : CreateBrush("#FCEEEE");
            PreflightStatusBorder.BorderBrush = _preflightSuccessful
                ? CreateBrush("#96D1A7")
                : CreateBrush("#E3A6A6");

            if (_preflightSuccessful)
            {
                StatusTextBlock.Text = "Validaciones SQL/runtime exitosas.";
                if (_state.ConfigureFrontend && string.IsNullOrWhiteSpace(BackendUrlTextBox.Text))
                {
                    BackendUrlTextBox.Text = $"http://localhost:{_state.BackendPort}/api";
                }

                UpdateEnvironmentVariableStatus();
            }
            else
            {
                StatusTextBlock.Text = "Hay validaciones pendientes por corregir.";
            }

            SetupStatusTextBlock.Text = StatusTextBlock.Text;
        }
        finally
        {
            RunPreflightButton.IsEnabled = true;
        }
    }

    private void GenerateJwtSecretCheckBox_OnChecked(object sender, RoutedEventArgs e)
    {
        if (JwtSecretPasswordBox is null)
        {
            return;
        }

        JwtSecretPasswordBox.IsEnabled = GenerateJwtSecretCheckBox.IsChecked != true;
        if (GenerateJwtSecretCheckBox.IsChecked == true)
        {
            JwtSecretPasswordBox.Password = string.Empty;
            JwtValidationTextBlock.Text = "Se generara un JWT robusto al aplicar configuracion.";
            JwtValidationTextBlock.Foreground = CreateBrush("#1E6F3D");
            return;
        }

        JwtValidationTextBlock.Text = "Ingresa un secreto manual con minimo 32 caracteres.";
        JwtValidationTextBlock.Foreground = CreateBrush("#8D4C00");
    }

    private void JwtSecretPasswordBox_OnPasswordChanged(object sender, RoutedEventArgs e)
    {
        if (GenerateJwtSecretCheckBox.IsChecked == true)
        {
            return;
        }

        var isValid = ValidateManualJwt(JwtSecretPasswordBox.Password, out var message);
        JwtValidationTextBlock.Text = message;
        JwtValidationTextBlock.Foreground = isValid
            ? CreateBrush("#1E6F3D")
            : CreateBrush("#8D4C00");
    }

    private void ValidateEnvironmentButton_Click(object sender, RoutedEventArgs e)
    {
        UpdateEnvironmentVariableStatus();
        StatusTextBlock.Text = "Estado de variables de entorno actualizado.";
        SetupStatusTextBlock.Text = StatusTextBlock.Text;
    }

    private void EnvironmentOverwriteOption_OnChecked(object sender, RoutedEventArgs e)
    {
        UpdateEnvironmentVariableStatus();
    }

    private void AspNetCoreUrlsTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateApiBaseUrlPreview();
        _state.DatabaseValidationPassed = false;
        _preflightSuccessful = false;
        _state.DatabaseValidationStatus = "Pendiente";
    }

    private void ConnectionStringTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        _state.DatabaseValidationPassed = false;
        _preflightSuccessful = false;
        _state.DatabaseValidationStatus = "Pendiente";

        try
        {
            var builder = new SqlConnectionStringBuilder(ConnectionStringTextBox.Text);
            if (!string.IsNullOrWhiteSpace(builder.DataSource))
            {
                SqlServerTextBox.Text = builder.DataSource;
            }
        }
        catch
        {
            // Ignore incomplete connection string edits while the user is typing.
        }
    }

    private void EnvironmentScope_OnChecked(object sender, RoutedEventArgs e)
    {
        UpdateEnvironmentVariableStatus();
    }

    private async Task ApplyConfigurationAsync()
    {
        if (_isApplying)
        {
            return;
        }

        _isApplying = true;
        SetBusyState(true, "Aplicando configuracion... por favor espera.");

        try
        {
            await Task.Run(() => _applyService.ApplyConfiguration(_state)).ConfigureAwait(true);

            System.Windows.MessageBox.Show(this,
                "Configuracion aplicada correctamente.\nVariables configuradas: ASCHEDULER_JWT_SECRET, DOTNET_ENVIRONMENT, ASPNETCORE_URLS.\nSiguiente paso sugerido: ejecutar preflight-host-check.ps1 y verificar /health.",
                "Configuracion completada",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            StatusTextBlock.Text = "Configuracion aplicada con exito.";
            SetupStatusTextBlock.Text = StatusTextBlock.Text;
        }
        finally
        {
            SetBusyState(false);
            _isApplying = false;
        }
    }

    private int GetNextStepIndex(int currentStep)
    {
        var next = currentStep + 1;
        while (next < _steps.Length && !ShouldDisplayStep(next))
        {
            next++;
        }

        return Math.Min(next, _steps.Length - 1);
    }

    private int GetPreviousStepIndex(int currentStep)
    {
        var previous = currentStep - 1;
        while (previous >= 0 && !ShouldDisplayStep(previous))
        {
            previous--;
        }

        return Math.Max(previous, 0);
    }

    private bool ShouldDisplayStep(int stepIndex)
    {
        return stepIndex switch
        {
            2 => _state.ConfigureBackend,
            3 => _state.ConfigureFrontend,
            _ => true
        };
    }

    private bool RequiresRuntimeValidation()
    {
        return _state.ConfigureBackend || _state.ConfigureFrontend;
    }

    private void SetBusyState(bool isBusy, string? statusMessage = null)
    {
        BackButton.IsEnabled = !isBusy && _currentStepIndex > 0;
        NextButton.IsEnabled = !isBusy;
        NextButton.Content = isBusy ? "Applying..." : _currentStepIndex == _steps.Length - 1 ? "Apply" : "Next";
        System.Windows.Input.Mouse.OverrideCursor = isBusy ? System.Windows.Input.Cursors.Wait : null;

        if (!string.IsNullOrWhiteSpace(statusMessage))
        {
            StatusTextBlock.Text = statusMessage;
            SetupStatusTextBlock.Text = statusMessage;
        }
    }

    private void UpdateSideNavState()
    {
        ApplyNavStyle(DirectoriesNavBorder, DirectoriesNavText, _currentStepIndex == 0, true);
        ApplyNavStyle(DatabaseNavBorder, DatabaseNavText, _currentStepIndex == 1, true);
        ApplyNavStyle(AuthenticationNavBorder, AuthenticationNavText, _currentStepIndex == 2, ShouldDisplayStep(2));
        ApplyNavStyle(FrontendNavBorder, FrontendNavText, _currentStepIndex == 3, ShouldDisplayStep(3));
        ApplyNavStyle(SummaryNavBorder, SummaryNavText, _currentStepIndex == 4, true);
    }

    private static void ApplyNavStyle(Border border, TextBlock textBlock, bool isActive, bool isVisible)
    {
        if (!isVisible)
        {
            border.Visibility = Visibility.Collapsed;
            return;
        }

        border.Visibility = Visibility.Visible;
        border.Background = isActive
            ? CreateBrush("#DDEBFF")
            : System.Windows.Media.Brushes.Transparent;
        textBlock.Foreground = isActive
            ? CreateBrush("#0D4ED1")
            : CreateBrush("#334A64");
    }

    private void RenderInitialWarnings()
    {
        if (_state.InitialWarnings.Count == 0)
        {
            InitialWarningsTextBlock.Text = "No se detectaron alertas iniciales en las rutas seleccionadas.";
            InitialWarningsTextBlock.Foreground = CreateBrush("#1E6F3D");
            return;
        }

        var builder = new StringBuilder();
        builder.AppendLine("Advertencias iniciales (no bloqueantes):");
        foreach (var warning in _state.InitialWarnings)
        {
            builder.AppendLine($"- {warning}");
        }

        InitialWarningsTextBlock.Text = builder.ToString().TrimEnd();
        InitialWarningsTextBlock.Foreground = CreateBrush("#8D4C00");
    }

    private void BrowseBackendFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder("Selecciona carpeta backend (release)", BackendFolderTextBox.Text);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        BackendFolderTextBox.Text = selectedPath;
    }

    private void BrowseFrontendFolderButton_Click(object sender, RoutedEventArgs e)
    {
        var selectedPath = SelectFolder("Selecciona carpeta frontend (release)", FrontendFolderTextBox.Text);
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return;
        }

        FrontendFolderTextBox.Text = selectedPath;
    }

    private string SelectFolder(string description, string initialPath)
    {
        using var folderDialog = new Forms.FolderBrowserDialog
        {
            Description = description,
            UseDescriptionForTitle = true,
            InitialDirectory = Directory.Exists(initialPath) ? initialPath : string.Empty,
            ShowNewFolderButton = false
        };

        var result = folderDialog.ShowDialog();
        return result == Forms.DialogResult.OK ? folderDialog.SelectedPath : string.Empty;
    }

    private static bool ValidateBackendFolder(string backendFolderPath, out string message)
    {
        if (!Directory.Exists(backendFolderPath))
        {
            message = "La carpeta backend no existe.";
            return false;
        }

        var appsettingsPath = Path.Combine(backendFolderPath, "appsettings.json");
        if (!File.Exists(appsettingsPath))
        {
            message = "Estructura backend invalida: falta appsettings.json en la carpeta seleccionada.";
            return false;
        }

        try
        {
            _ = JsonNode.Parse(File.ReadAllText(appsettingsPath))?.AsObject()
                ?? throw new InvalidOperationException("appsettings.json no es un objeto JSON valido.");
        }
        catch (Exception ex)
        {
            message = $"appsettings.json invalido: {ex.Message}";
            return false;
        }

        var productionPath = Path.Combine(backendFolderPath, "appsettings.Production.json");
        var testWritePath = File.Exists(productionPath)
            ? productionPath
            : Path.Combine(backendFolderPath, ".wizard-write-test.tmp");

        try
        {
            using var stream = File.Open(testWritePath, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read);
            stream.Flush();
        }
        catch (Exception)
        {
            message = "Sin permisos de escritura en carpeta backend para generar appsettings.Production.json.";
            return false;
        }
        finally
        {
            if (testWritePath.EndsWith(".wizard-write-test.tmp", StringComparison.OrdinalIgnoreCase) && File.Exists(testWritePath))
            {
                File.Delete(testWritePath);
            }
        }

        message = string.Empty;
        return true;
    }

    private static bool ValidateFrontendFolder(string frontendFolderPath, out string message)
    {
        if (!Directory.Exists(frontendFolderPath))
        {
            message = "La carpeta frontend no existe.";
            return false;
        }

        var rootConfigPath = Path.Combine(frontendFolderPath, "config.json");
        var publicConfigPath = Path.Combine(frontendFolderPath, "public", "config.json");

        if (!File.Exists(rootConfigPath) && !File.Exists(publicConfigPath))
        {
            message = "Estructura frontend invalida: falta config.json (raiz o public/config.json).";
            return false;
        }

        message = string.Empty;
        return true;
    }

    private void UpdateEnvironmentVariableStatus()
    {
        var scope = GetEnvironmentVariableScope();
        var scopeLabel = scope == EnvironmentVariableTarget.Machine ? "Machine" : "User";

        var names = new[] { "ASCHEDULER_JWT_SECRET", "DOTNET_ENVIRONMENT", "ASPNETCORE_URLS" };
        var lines = new List<string>();
        var existingCount = 0;
        var actionLines = new List<string>();

        foreach (var name in names)
        {
            var value = Environment.GetEnvironmentVariable(name, scope);
            var exists = !string.IsNullOrWhiteSpace(value);
            var overwriteEnabled = ShouldOverwriteVariable(name);
            if (exists)
            {
                existingCount++;
            }

            lines.Add($"{name}: {(exists ? "Existe" : "No existe")} ({scopeLabel})");
            if (!exists)
            {
                actionLines.Add($"{name}=Crear");
            }
            else if (overwriteEnabled)
            {
                actionLines.Add($"{name}=Actualizar (sobrescribir)");
            }
            else
            {
                actionLines.Add($"{name}=No tocar (sobrescritura deshabilitada)");
            }

            var desiredValue = GetDesiredEnvironmentVariableValue(name);
            if (!string.IsNullOrWhiteSpace(desiredValue))
            {
                actionLines.Add($"{name}=>{desiredValue}");
            }
        }

        _state.ExistingEnvironmentVariablesCount = existingCount;
        _state.EnvironmentValidationStatus = $"{existingCount}/{names.Length} existentes en {scopeLabel}";
        _state.EnvironmentVariablePlan = string.Join(", ", actionLines);

        // The radio button Checked event can fire while XAML is still creating controls.
        // Guard against null to avoid crashing during startup.
        if (EnvVarStatusTextBlock is not null)
        {
            EnvVarStatusTextBlock.Text = string.Join(Environment.NewLine, lines);
        }

        UpdateApiBaseUrlPreview();
    }

    private EnvironmentVariableTarget GetEnvironmentVariableScope()
    {
        return UseMachineScopeRadioButton?.IsChecked == true
            ? EnvironmentVariableTarget.Machine
            : EnvironmentVariableTarget.User;
    }

    private bool ShouldOverwriteVariable(string variableName)
    {
        return variableName switch
        {
            "ASCHEDULER_JWT_SECRET" => OverwriteJwtSecretCheckBox?.IsChecked == true,
            "DOTNET_ENVIRONMENT" => OverwriteDotNetEnvironmentCheckBox?.IsChecked == true,
            "ASPNETCORE_URLS" => OverwriteAspNetCoreUrlsCheckBox?.IsChecked == true,
            _ => false
        };
    }

    private string GetDesiredEnvironmentVariableValue(string variableName)
    {
        return variableName switch
        {
            "ASCHEDULER_JWT_SECRET" => _state.GenerateJwtSecret ? "<AUTO_GENERATED_AT_APPLY>" : "<MANUAL_VALUE>",
            "DOTNET_ENVIRONMENT" => "Production",
            "ASPNETCORE_URLS" => AspNetCoreUrlsTextBox?.Text.Trim() ?? string.Empty,
            _ => string.Empty
        };
    }

    private void UpdateApiBaseUrlPreview()
    {
        if (ApiBaseUrlPreviewTextBlock is null)
        {
            return;
        }

        var rawUrls = AspNetCoreUrlsTextBox?.Text.Trim();
        if (string.IsNullOrWhiteSpace(rawUrls))
        {
            ApiBaseUrlPreviewTextBlock.Text = "API base URL preview: N/A";
            return;
        }

        var primaryUrl = rawUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault();

        if (string.IsNullOrWhiteSpace(primaryUrl))
        {
            ApiBaseUrlPreviewTextBlock.Text = "API base URL preview: N/A";
            return;
        }

        var normalizedPrimaryUrl = primaryUrl.EndsWith("/")
            ? primaryUrl[..^1]
            : primaryUrl;
        ApiBaseUrlPreviewTextBlock.Text = $"API base URL preview: {normalizedPrimaryUrl}/api";
    }

    private static bool ValidateAspNetCoreUrls(string aspNetCoreUrls, out string message)
    {
        if (string.IsNullOrWhiteSpace(aspNetCoreUrls))
        {
            message = "ASPNETCORE_URLS es obligatorio.";
            return false;
        }

        var urls = aspNetCoreUrls.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (urls.Length == 0)
        {
            message = "ASPNETCORE_URLS debe incluir al menos una URL.";
            return false;
        }

        foreach (var url in urls)
        {
            if (!url.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !url.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                message = "ASPNETCORE_URLS solo permite URLs http/https separadas por ';'.";
                return false;
            }
        }

        message = string.Empty;
        return true;
    }

    private static bool ValidateManualJwt(string jwtSecret, out string message)
    {
        if (string.IsNullOrWhiteSpace(jwtSecret))
        {
            message = "Ingresa JWT secret manual o activa auto-generacion.";
            return false;
        }

        if (jwtSecret.Length < 32)
        {
            message = "JWT secret manual demasiado corto: minimo 32 caracteres.";
            return false;
        }

        if (jwtSecret.Length > 256)
        {
            message = "JWT secret manual demasiado largo: maximo 256 caracteres.";
            return false;
        }

        var hasUpper = jwtSecret.Any(char.IsUpper);
        var hasLower = jwtSecret.Any(char.IsLower);
        var hasDigit = jwtSecret.Any(char.IsDigit);
        var hasSpecial = jwtSecret.Any(ch => !char.IsLetterOrDigit(ch));

        if (!hasUpper || !hasLower || !hasDigit || !hasSpecial)
        {
            message = "JWT secret manual debe incluir mayusculas, minusculas, numeros y caracteres especiales.";
            return false;
        }

        var uniqueChars = jwtSecret.Distinct().Count();
        if (uniqueChars < 12)
        {
            message = "JWT secret manual con baja diversidad de caracteres. Usa al menos 12 caracteres unicos.";
            return false;
        }

        message = "JWT secret manual valido.";
        return true;
    }

    private static bool ValidateConnectionString(string connectionString, out string message)
    {
        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.DataSource))
            {
                message = "Connection string invalida: falta servidor (Data Source).";
                return false;
            }

            if (string.IsNullOrWhiteSpace(builder.InitialCatalog))
            {
                message = "Connection string invalida: falta Database/Initial Catalog.";
                return false;
            }

            if (builder.IntegratedSecurity && !string.IsNullOrWhiteSpace(builder.UserID))
            {
                message = "Connection string invalida: no mezcles Integrated Security con User ID.";
                return false;
            }

            if (builder.InitialCatalog.Equals("master", StringComparison.OrdinalIgnoreCase) ||
                builder.InitialCatalog.Equals("tempdb", StringComparison.OrdinalIgnoreCase))
            {
                message = "No uses master/tempdb como base de aplicacion.";
                return false;
            }

            message = string.Empty;
            return true;
        }
        catch (Exception ex)
        {
            message = $"Connection string invalida: {ex.Message}";
            return false;
        }
    }

    private static System.Windows.Media.SolidColorBrush CreateBrush(string hexColor)
    {
        return new System.Windows.Media.SolidColorBrush(
            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hexColor));
    }
}