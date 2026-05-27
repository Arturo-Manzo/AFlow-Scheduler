[CmdletBinding(DefaultParameterSetName = 'Preview')]
param(
    [Parameter(Mandatory = $true)]
    [string]$InstallPath,

    [Parameter(Mandatory = $true)]
    [string]$PackagePath,

    [string]$FrontendInstallPath = "",

    [string]$FrontendPackagePath = "",

    [string]$ManifestPath = "",

    [string]$ServiceName = "",

    [string]$LogDirectory = "$env:ProgramData\CHRONIQ\upgrade-logs",

    [Parameter(ParameterSetName = 'Preview')]
    [switch]$Preview,

    [Parameter(ParameterSetName = 'Apply')]
    [switch]$Apply,

    [switch]$SkipDatabase,

    [switch]$SkipServiceRestart
)

$ErrorActionPreference = 'Stop'

function Resolve-AbsolutePath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,
        [string]$BasePath = ""
    )

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return ""
    }

    if ([System.IO.Path]::IsPathRooted($Path)) {
        return [System.IO.Path]::GetFullPath($Path)
    }

    if ([string]::IsNullOrWhiteSpace($BasePath)) {
        $BasePath = (Get-Location).Path
    }

    return [System.IO.Path]::GetFullPath((Join-Path $BasePath $Path))
}

function Ensure-Directory {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path)) {
        New-Item -ItemType Directory -Path $Path -Force | Out-Null
    }
}

function Write-LogLine {
    param(
        [string]$Message,
        [string]$Level = "INFO"
    )

    $timestamp = Get-Date -Format "yyyy-MM-dd HH:mm:ss"
    $line = "[{0}] [{1}] {2}" -f $timestamp, $Level.ToUpperInvariant(), $Message
    Write-Host $line
    Add-Content -LiteralPath $script:LogFilePath -Value $line
}

function Backup-FileIfExists {
    param(
        [string]$Path,
        [string]$BackupDirectory
    )

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        return $null
    }

    Ensure-Directory -Path $BackupDirectory
    $destination = Join-Path $BackupDirectory ([System.IO.Path]::GetFileName($Path))
    Copy-Item -LiteralPath $Path -Destination $destination -Force
    return $destination
}

function ConvertTo-PrettyJson {
    param([object]$Data)
    return ($Data | ConvertTo-Json -Depth 50) + [Environment]::NewLine
}

function Read-JsonFile {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "JSON file not found: $Path"
    }

    try {
        return Get-Content -LiteralPath $Path -Raw | ConvertFrom-Json
    }
    catch {
        throw "Could not parse JSON file '$Path'. $($_.Exception.Message)"
    }
}

function Save-JsonFile {
    param(
        [string]$Path,
        [object]$Data
    )

    $directory = Split-Path -Parent $Path
    Ensure-Directory -Path $directory
    [System.IO.File]::WriteAllText($Path, (ConvertTo-PrettyJson -Data $Data))
}

function Get-ObjectPropertyNames {
    param([object]$Value)

    if ($null -eq $Value) {
        return @()
    }

    if ($Value -is [System.Collections.IDictionary]) {
        return @($Value.Keys)
    }

    return @($Value.PSObject.Properties | ForEach-Object { $_.Name })
}

function Get-ObjectPropertyValue {
    param(
        [object]$Value,
        [string]$Name
    )

    if ($null -eq $Value) {
        return $null
    }

    if ($Value -is [System.Collections.IDictionary]) {
        if ($Value.Contains($Name)) {
            return $Value[$Name]
        }
        return $null
    }

    if ($null -ne $Value.PSObject.Properties[$Name]) {
        return $Value.$Name
    }

    return $null
}

function Set-ObjectPropertyValue {
    param(
        [object]$Value,
        [string]$Name,
        [object]$PropertyValue
    )

    if ($Value -is [System.Collections.IDictionary]) {
        $Value[$Name] = $PropertyValue
        return
    }

    if ($null -eq $Value.PSObject.Properties[$Name]) {
        $Value | Add-Member -MemberType NoteProperty -Name $Name -Value $PropertyValue
    }
    else {
        $Value.$Name = $PropertyValue
    }
}

function Remove-ObjectProperty {
    param(
        [object]$Value,
        [string]$Name
    )

    if ($null -eq $Value) {
        return
    }

    if ($Value -is [System.Collections.IDictionary]) {
        if ($Value.Contains($Name)) {
            $Value.Remove($Name)
        }
        return
    }

    $property = $Value.PSObject.Properties[$Name]
    if ($null -ne $property) {
        $Value.PSObject.Properties.Remove($Name)
    }
}

function Test-IsJsonObject {
    param([object]$Value)

    if ($null -eq $Value) {
        return $false
    }

    if ($Value -is [string]) {
        return $false
    }

    if ($Value -is [System.Collections.IDictionary]) {
        return $true
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [pscustomobject]) {
        return $false
    }

    return $Value.PSObject.Properties.Count -gt 0
}

function Clone-JsonValue {
    param([object]$Value)

    if ($null -eq $Value) {
        return $null
    }

    if (Test-IsJsonObject -Value $Value) {
        $clone = [pscustomobject]@{}
        foreach ($propertyName in Get-ObjectPropertyNames -Value $Value) {
            Set-ObjectPropertyValue -Value $clone -Name $propertyName -PropertyValue (Clone-JsonValue -Value (Get-ObjectPropertyValue -Value $Value -Name $propertyName))
        }
        return $clone
    }

    if ($Value -is [System.Collections.IEnumerable] -and $Value -isnot [string]) {
        $items = New-Object System.Collections.ArrayList
        foreach ($item in $Value) {
            [void]$items.Add((Clone-JsonValue -Value $item))
        }
        return ,$items.ToArray()
    }

    return $Value
}

function Merge-MissingJsonProperties {
    param(
        [object]$Existing,
        [object]$Template,
        [string]$PathPrefix,
        [System.Collections.Generic.List[string]]$AddedPaths
    )

    foreach ($propertyName in Get-ObjectPropertyNames -Value $Template) {
        $currentPath = if ([string]::IsNullOrWhiteSpace($PathPrefix)) { $propertyName } else { "$PathPrefix.$propertyName" }
        $templateValue = Get-ObjectPropertyValue -Value $Template -Name $propertyName
        $existingProperty = $Existing.PSObject.Properties[$propertyName]

        if ($null -eq $existingProperty) {
            Set-ObjectPropertyValue -Value $Existing -Name $propertyName -PropertyValue (Clone-JsonValue -Value $templateValue)
            $AddedPaths.Add($currentPath)
            continue
        }

        $existingValue = $existingProperty.Value
        if ((Test-IsJsonObject -Value $existingValue) -and (Test-IsJsonObject -Value $templateValue)) {
            Merge-MissingJsonProperties -Existing $existingValue -Template $templateValue -PathPrefix $currentPath -AddedPaths $AddedPaths
        }
    }
}

function Get-JsonPathSegments {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return @()
    }

    return @($Path.Split('.', [System.StringSplitOptions]::RemoveEmptyEntries))
}

function Get-JsonValueByPath {
    param(
        [object]$Root,
        [string]$Path
    )

    $segments = Get-JsonPathSegments -Path $Path
    $current = $Root
    foreach ($segment in $segments) {
        if ($null -eq $current) {
            return $null
        }

        if ($current -is [System.Collections.IDictionary]) {
            if (-not $current.Contains($segment)) {
                return $null
            }
            $current = $current[$segment]
            continue
        }

        if ($null -eq $current.PSObject.Properties[$segment]) {
            return $null
        }
        $current = $current.$segment
    }

    return $current
}

function Test-JsonPathExists {
    param(
        [object]$Root,
        [string]$Path
    )

    $segments = Get-JsonPathSegments -Path $Path
    if ($segments.Count -eq 0) {
        return $false
    }

    $current = $Root
    foreach ($segment in $segments) {
        if ($null -eq $current) {
            return $false
        }

        if ($current -is [System.Collections.IDictionary]) {
            if (-not $current.Contains($segment)) {
                return $false
            }
            $current = $current[$segment]
            continue
        }

        if ($null -eq $current.PSObject.Properties[$segment]) {
            return $false
        }
        $current = $current.$segment
    }

    return $true
}

function Ensure-JsonPathParent {
    param(
        [object]$Root,
        [string[]]$Segments
    )

    $current = $Root
    for ($index = 0; $index -lt ($Segments.Count - 1); $index++) {
        $segment = $Segments[$index]
        $nextValue = Get-ObjectPropertyValue -Value $current -Name $segment
        if (-not (Test-IsJsonObject -Value $nextValue)) {
            $nextValue = [pscustomobject]@{}
            Set-ObjectPropertyValue -Value $current -Name $segment -PropertyValue $nextValue
        }
        $current = $nextValue
    }

    return $current
}

function Set-JsonValueByPath {
    param(
        [object]$Root,
        [string]$Path,
        [object]$Value
    )

    $segments = Get-JsonPathSegments -Path $Path
    if ($segments.Count -eq 0) {
        return
    }

    $parent = Ensure-JsonPathParent -Root $Root -Segments $segments
    Set-ObjectPropertyValue -Value $parent -Name $segments[$segments.Count - 1] -PropertyValue $Value
}

function Remove-JsonPath {
    param(
        [object]$Root,
        [string]$Path
    )

    $segments = Get-JsonPathSegments -Path $Path
    if ($segments.Count -eq 0) {
        return
    }

    $current = $Root
    for ($index = 0; $index -lt ($segments.Count - 1); $index++) {
        $segment = $segments[$index]
        $current = Get-ObjectPropertyValue -Value $current -Name $segment
        if ($null -eq $current) {
            return
        }
    }

    Remove-ObjectProperty -Value $current -Name $segments[$segments.Count - 1]
}

function Apply-RenameRules {
    param(
        [object]$Root,
        [object[]]$Rules,
        [System.Collections.Generic.List[string]]$AppliedRules
    )

    foreach ($rule in @($Rules)) {
        if ($null -eq $rule) {
            continue
        }

        $from = $rule.from
        $to = $rule.to
        if ([string]::IsNullOrWhiteSpace($from) -or [string]::IsNullOrWhiteSpace($to)) {
            continue
        }

        if ((Test-JsonPathExists -Root $Root -Path $from) -and -not (Test-JsonPathExists -Root $Root -Path $to)) {
            $value = Clone-JsonValue -Value (Get-JsonValueByPath -Root $Root -Path $from)
            Set-JsonValueByPath -Root $Root -Path $to -Value $value
            Remove-JsonPath -Root $Root -Path $from
            $AppliedRules.Add("$from -> $to")
        }
    }
}

function Resolve-RelativePathInTree {
    param(
        [string]$RootPath,
        [string]$ChildRelativePath
    )

    return [System.IO.Path]::GetFullPath((Join-Path $RootPath $ChildRelativePath))
}

function Get-ApplicationVersion {
    param([string]$ExecutablePath)

    if (-not (Test-Path -LiteralPath $ExecutablePath -PathType Leaf)) {
        return "unknown"
    }

    $versionInfo = [System.Diagnostics.FileVersionInfo]::GetVersionInfo($ExecutablePath)
    if (-not [string]::IsNullOrWhiteSpace($versionInfo.ProductVersion)) {
        return $versionInfo.ProductVersion
    }

    if (-not [string]::IsNullOrWhiteSpace($versionInfo.FileVersion)) {
        return $versionInfo.FileVersion
    }

    return "unknown"
}

function Parse-ConnectionString {
    param([string]$ConnectionString)

    if ([string]::IsNullOrWhiteSpace($ConnectionString)) {
        throw "Connection string is required for database upgrade."
    }

    $builder = [System.Data.SqlClient.SqlConnectionStringBuilder]::new($ConnectionString)
    return $builder
}

function Open-SqlConnection {
    param([string]$ConnectionString)

    $connection = [System.Data.SqlClient.SqlConnection]::new($ConnectionString)
    $connection.Open()
    return $connection
}

function Split-SqlBatches {
    param([string]$ScriptText)

    return @([regex]::Split($ScriptText, '(?im)^\s*GO\s*(?:--.*)?$') | Where-Object { -not [string]::IsNullOrWhiteSpace($_) })
}

function Invoke-SqlNonQuery {
    param(
        [System.Data.SqlClient.SqlConnection]$Connection,
        [string]$CommandText,
        [System.Data.SqlClient.SqlTransaction]$Transaction = $null
    )

    $command = $Connection.CreateCommand()
    $command.CommandTimeout = 120
    $command.CommandText = $CommandText
    if ($null -ne $Transaction) {
        $command.Transaction = $Transaction
    }
    [void]$command.ExecuteNonQuery()
}

function Ensure-SchemaMigrationsTable {
    param([string]$ConnectionString)

    $sql = @"
IF OBJECT_ID('dbo.SchemaMigrations', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.SchemaMigrations
    (
        MigrationId NVARCHAR(200) NOT NULL PRIMARY KEY,
        AppliedAtUtc DATETIME2(7) NOT NULL CONSTRAINT DF_SchemaMigrations_AppliedAtUtc DEFAULT SYSUTCDATETIME()
    );
END
"@

    $connection = Open-SqlConnection -ConnectionString $ConnectionString
    try {
        Invoke-SqlNonQuery -Connection $connection -CommandText $sql
    }
    finally {
        $connection.Dispose()
    }
}

function Get-AppliedMigrationIds {
    param([string]$ConnectionString)

    $connection = Open-SqlConnection -ConnectionString $ConnectionString
    try {
        $command = $connection.CreateCommand()
        $command.CommandText = "SELECT MigrationId FROM dbo.SchemaMigrations;"
        $reader = $command.ExecuteReader()
        $ids = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
        while ($reader.Read()) {
            [void]$ids.Add($reader.GetString(0))
        }
        $reader.Dispose()
        return $ids
    }
    finally {
        $connection.Dispose()
    }
}

function Invoke-Migration {
    param(
        [string]$ConnectionString,
        [string]$MigrationId,
        [string]$ScriptPath,
        [bool]$Transactional
    )

    $scriptText = Get-Content -LiteralPath $ScriptPath -Raw
    $batches = Split-SqlBatches -ScriptText $scriptText
    $connection = Open-SqlConnection -ConnectionString $ConnectionString
    $transaction = $null

    try {
        if ($Transactional) {
            $transaction = $connection.BeginTransaction()
        }

        foreach ($batch in $batches) {
            Invoke-SqlNonQuery -Connection $connection -CommandText $batch -Transaction $transaction
        }

        $insertSql = "INSERT INTO dbo.SchemaMigrations (MigrationId) VALUES (@MigrationId);"
        $command = $connection.CreateCommand()
        $command.CommandText = $insertSql
        if ($null -ne $transaction) {
            $command.Transaction = $transaction
        }
        [void]$command.Parameters.Add("@MigrationId", [System.Data.SqlDbType]::NVarChar, 200)
        $command.Parameters["@MigrationId"].Value = $MigrationId
        [void]$command.ExecuteNonQuery()

        if ($null -ne $transaction) {
            $transaction.Commit()
        }
    }
    catch {
        if ($null -ne $transaction) {
            $transaction.Rollback()
        }
        throw
    }
    finally {
        if ($null -ne $transaction) {
            $transaction.Dispose()
        }
        $connection.Dispose()
    }
}

function Invoke-DatabaseValidationScript {
    param(
        [string]$ConnectionString,
        [string]$ScriptPath
    )

    if (-not (Test-Path -LiteralPath $ScriptPath -PathType Leaf)) {
        Write-LogLine -Message "Database validation script not found, skipping: $ScriptPath" -Level "WARN"
        return
    }

    $scriptText = Get-Content -LiteralPath $ScriptPath -Raw
    $connection = Open-SqlConnection -ConnectionString $ConnectionString
    try {
        foreach ($batch in (Split-SqlBatches -ScriptText $scriptText)) {
            Invoke-SqlNonQuery -Connection $connection -CommandText $batch
        }
    }
    finally {
        $connection.Dispose()
    }
}

function Test-HealthEndpoint {
    param([string]$BaseUrl)

    if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
        return
    }

    $healthUrl = $BaseUrl.TrimEnd('/') + "/health"
    $response = Invoke-WebRequest -Uri $healthUrl -UseBasicParsing -TimeoutSec 20
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "Health endpoint returned status $($response.StatusCode)."
    }
}

function Copy-DirectoryContents {
    param(
        [string]$SourceDirectory,
        [string]$DestinationDirectory,
        [string[]]$ExcludeRelativePaths
    )

    Ensure-Directory -Path $DestinationDirectory
    $sourceRoot = (Resolve-Path -LiteralPath $SourceDirectory).Path.TrimEnd('\')
    $excludeSet = New-Object System.Collections.Generic.HashSet[string] ([System.StringComparer]::OrdinalIgnoreCase)
    foreach ($exclude in @($ExcludeRelativePaths)) {
        [void]$excludeSet.Add(($exclude -replace '/', '\').TrimStart('\'))
    }

    Get-ChildItem -LiteralPath $sourceRoot -Recurse -Force | ForEach-Object {
        $fullName = $_.FullName
        $relativePath = $fullName.Substring($sourceRoot.Length).TrimStart('\')
        if ($excludeSet.Contains($relativePath)) {
            return
        }

        $destination = Join-Path $DestinationDirectory $relativePath
        if ($_.PSIsContainer) {
            Ensure-Directory -Path $destination
            return
        }

        $destinationDirectory = Split-Path -Parent $destination
        Ensure-Directory -Path $destinationDirectory
        Copy-Item -LiteralPath $fullName -Destination $destination -Force
    }
}

function Resolve-ServiceName {
    param(
        [string]$ExplicitName,
        [object]$BackendConfig
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitName)) {
        return $ExplicitName
    }

    $configured = Get-JsonValueByPath -Root $BackendConfig -Path "WindowsService.ServiceName"
    if (-not [string]::IsNullOrWhiteSpace($configured)) {
        return [string]$configured
    }

    return "CHRONIQ"
}

function Backup-ServiceMetadata {
    param(
        [string]$Name,
        [string]$BackupDirectory
    )

    if ([string]::IsNullOrWhiteSpace($Name)) {
        return
    }

    $service = Get-CimInstance Win32_Service -Filter ("Name='{0}'" -f $Name) -ErrorAction SilentlyContinue
    if ($null -eq $service) {
        Write-LogLine -Message "Service '$Name' not found; metadata backup skipped." -Level "WARN"
        return
    }

    Ensure-Directory -Path $BackupDirectory
    $metadataPath = Join-Path $BackupDirectory "service-metadata.json"
    [System.IO.File]::WriteAllText($metadataPath, (($service | Select-Object Name, DisplayName, State, StartMode, PathName, StartName | ConvertTo-Json -Depth 5) + [Environment]::NewLine))
    Write-LogLine -Message "Backed up service metadata to $metadataPath"
}

function Get-KestrelBaseUrl {
    param([object]$BackendConfig)

    $url = Get-JsonValueByPath -Root $BackendConfig -Path "Kestrel.Endpoints.Http.Url"
    if ([string]::IsNullOrWhiteSpace($url)) {
        return ""
    }

    return [string]$url
}

function Resolve-ManifestPath {
    param(
        [string]$ExplicitManifestPath,
        [string]$ResolvedPackagePath
    )

    if (-not [string]::IsNullOrWhiteSpace($ExplicitManifestPath)) {
        return Resolve-AbsolutePath -Path $ExplicitManifestPath -BasePath $ResolvedPackagePath
    }

    $candidates = @(
        (Join-Path $ResolvedPackagePath "upgrade-manifest.json"),
        (Join-Path $ResolvedPackagePath "deployment-templates\upgrade-manifest.json")
    )

    foreach ($candidate in $candidates) {
        if (Test-Path -LiteralPath $candidate -PathType Leaf) {
            return $candidate
        }
    }

    throw "Upgrade manifest not found under package path '$ResolvedPackagePath'."
}

function Resolve-FrontendConfigPath {
    param([string]$ResolvedFrontendInstallPath)

    if ([string]::IsNullOrWhiteSpace($ResolvedFrontendInstallPath)) {
        return ""
    }

    return Join-Path $ResolvedFrontendInstallPath "config.json"
}

$mode = if ($Apply) { "Apply" } else { "Preview" }
$resolvedInstallPath = Resolve-AbsolutePath -Path $InstallPath
$resolvedPackagePath = Resolve-AbsolutePath -Path $PackagePath
$resolvedFrontendInstallPath = if ([string]::IsNullOrWhiteSpace($FrontendInstallPath)) { "" } else { Resolve-AbsolutePath -Path $FrontendInstallPath -BasePath $resolvedInstallPath }
$resolvedFrontendPackagePath = if ([string]::IsNullOrWhiteSpace($FrontendPackagePath)) { "" } else { Resolve-AbsolutePath -Path $FrontendPackagePath }
$resolvedManifestPath = Resolve-ManifestPath -ExplicitManifestPath $ManifestPath -ResolvedPackagePath $resolvedPackagePath

Ensure-Directory -Path $LogDirectory
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$script:LogFilePath = Join-Path $LogDirectory "upgrade-$timestamp.log"
New-Item -ItemType File -Path $script:LogFilePath -Force | Out-Null

Write-LogLine -Message "Starting CHRONIQ upgrade in $mode mode."
Write-LogLine -Message "Install path: $resolvedInstallPath"
Write-LogLine -Message "Package path: $resolvedPackagePath"

if (-not (Test-Path -LiteralPath $resolvedInstallPath -PathType Container)) {
    throw "Install path does not exist: $resolvedInstallPath"
}

if (-not (Test-Path -LiteralPath $resolvedPackagePath -PathType Container)) {
    throw "Package path does not exist: $resolvedPackagePath"
}

$manifest = Read-JsonFile -Path $resolvedManifestPath
$backendTemplatePath = Join-Path $resolvedPackagePath "deployment-templates\backend\appsettings.upgrade-template.json"
$frontendTemplatePath = Join-Path $resolvedPackagePath "deployment-templates\frontend\config.upgrade-template.json"
if ([string]::IsNullOrWhiteSpace($resolvedFrontendPackagePath) -and -not (Test-Path -LiteralPath $frontendTemplatePath -PathType Leaf)) {
    $frontendTemplatePath = ""
}
elseif (-not [string]::IsNullOrWhiteSpace($resolvedFrontendPackagePath)) {
    $frontendTemplatePath = Join-Path $resolvedFrontendPackagePath "deployment-templates\frontend\config.upgrade-template.json"
}

if (-not (Test-Path -LiteralPath $backendTemplatePath -PathType Leaf)) {
    throw "Backend upgrade template not found: $backendTemplatePath"
}

$backendConfigPath = Join-Path $resolvedInstallPath "appsettings.Production.json"
$backendTemplate = Read-JsonFile -Path $backendTemplatePath
$backendConfig = if (Test-Path -LiteralPath $backendConfigPath -PathType Leaf) { Read-JsonFile -Path $backendConfigPath } else { [pscustomobject]@{} }
$backendAddedPaths = New-Object 'System.Collections.Generic.List[string]'
$backendRenames = New-Object 'System.Collections.Generic.List[string]'

Apply-RenameRules -Root $backendConfig -Rules @($manifest.configRenames.backend) -AppliedRules $backendRenames
Merge-MissingJsonProperties -Existing $backendConfig -Template $backendTemplate -PathPrefix "" -AddedPaths $backendAddedPaths
Set-JsonValueByPath -Root $backendConfig -Path "Jwt.Secret" -Value "__FROM_ENV__"

$frontendConfigPath = Resolve-FrontendConfigPath -ResolvedFrontendInstallPath $resolvedFrontendInstallPath
$frontendConfig = $null
$frontendAddedPaths = New-Object 'System.Collections.Generic.List[string]'
$frontendRenames = New-Object 'System.Collections.Generic.List[string]'
if (-not [string]::IsNullOrWhiteSpace($frontendConfigPath) -and (Test-Path -LiteralPath $frontendConfigPath -PathType Leaf) -and -not [string]::IsNullOrWhiteSpace($frontendTemplatePath) -and (Test-Path -LiteralPath $frontendTemplatePath -PathType Leaf)) {
    $frontendConfig = Read-JsonFile -Path $frontendConfigPath
    $frontendTemplate = Read-JsonFile -Path $frontendTemplatePath
    Apply-RenameRules -Root $frontendConfig -Rules @($manifest.configRenames.frontend) -AppliedRules $frontendRenames
    Merge-MissingJsonProperties -Existing $frontendConfig -Template $frontendTemplate -PathPrefix "" -AddedPaths $frontendAddedPaths
}

$backendServiceName = Resolve-ServiceName -ExplicitName $ServiceName -BackendConfig $backendConfig
$installedExePath = Join-Path $resolvedInstallPath "CHRONIQ.exe"
$packageExePath = Join-Path $resolvedPackagePath "CHRONIQ.exe"
$installedVersion = Get-ApplicationVersion -ExecutablePath $installedExePath
$packageVersion = Get-ApplicationVersion -ExecutablePath $packageExePath

Write-LogLine -Message "Installed version: $installedVersion"
Write-LogLine -Message "Package version: $packageVersion"
Write-LogLine -Message "Service name: $backendServiceName"

$backupDirectory = Join-Path $resolvedInstallPath ("upgrade-backups\" + $timestamp)
if ($Apply) {
    Ensure-Directory -Path $backupDirectory
    Backup-FileIfExists -Path $backendConfigPath -BackupDirectory $backupDirectory | Out-Null
    if (-not [string]::IsNullOrWhiteSpace($frontendConfigPath)) {
        Backup-FileIfExists -Path $frontendConfigPath -BackupDirectory $backupDirectory | Out-Null
    }
    Backup-ServiceMetadata -Name $backendServiceName -BackupDirectory $backupDirectory
}

$connectionString = [string](Get-JsonValueByPath -Root $backendConfig -Path "ConnectionStrings.Default")
if (-not $SkipDatabase) {
    $null = Parse-ConnectionString -ConnectionString $connectionString
    Write-LogLine -Message "Database connectivity configuration detected."
}

Write-LogLine -Message ("Backend config merge added {0} path(s)." -f $backendAddedPaths.Count)
if ($backendAddedPaths.Count -gt 0) {
    foreach ($path in $backendAddedPaths) {
        Write-LogLine -Message "Added backend config path: $path"
    }
}

foreach ($rename in $backendRenames) {
    Write-LogLine -Message "Applied backend rename: $rename"
}

if ($null -ne $frontendConfig) {
    Write-LogLine -Message ("Frontend config merge added {0} path(s)." -f $frontendAddedPaths.Count)
    foreach ($path in $frontendAddedPaths) {
        Write-LogLine -Message "Added frontend config path: $path"
    }
    foreach ($rename in $frontendRenames) {
        Write-LogLine -Message "Applied frontend rename: $rename"
    }
}
elseif (-not [string]::IsNullOrWhiteSpace($resolvedFrontendInstallPath)) {
    Write-LogLine -Message "Frontend config merge skipped because template or config file was not found." -Level "WARN"
}

foreach ($deprecatedKey in @($manifest.deprecatedKeys.backend)) {
    if (Test-JsonPathExists -Root $backendConfig -Path $deprecatedKey) {
        Write-LogLine -Message "Deprecated backend key present: $deprecatedKey" -Level "WARN"
    }
}

if ($null -ne $frontendConfig) {
    foreach ($deprecatedKey in @($manifest.deprecatedKeys.frontend)) {
        if (Test-JsonPathExists -Root $frontendConfig -Path $deprecatedKey) {
            Write-LogLine -Message "Deprecated frontend key present: $deprecatedKey" -Level "WARN"
        }
    }
}

if (-not $Apply) {
    Write-LogLine -Message "Preview complete. No installation files, database objects, or service state were changed."
    Write-LogLine -Message "Log file: $script:LogFilePath"
    return
}

Save-JsonFile -Path $backendConfigPath -Data $backendConfig
Write-LogLine -Message "Wrote merged backend configuration to $backendConfigPath"

if ($null -ne $frontendConfig -and -not [string]::IsNullOrWhiteSpace($frontendConfigPath)) {
    Save-JsonFile -Path $frontendConfigPath -Data $frontendConfig
    Write-LogLine -Message "Wrote merged frontend configuration to $frontendConfigPath"
}

if (-not $SkipDatabase) {
    Ensure-SchemaMigrationsTable -ConnectionString $connectionString
    $appliedMigrationIds = Get-AppliedMigrationIds -ConnectionString $connectionString
    $executedMigrations = New-Object 'System.Collections.Generic.List[string]'

    foreach ($migration in @($manifest.databaseMigrations)) {
        if ($null -eq $migration) {
            continue
        }

        $migrationId = [string]$migration.id
        $migrationPath = Resolve-RelativePathInTree -RootPath $resolvedPackagePath -ChildRelativePath ([string]$migration.path)
        $transactional = $true
        if ($null -ne $migration.transactional) {
            $transactional = [bool]$migration.transactional
        }

        if ($appliedMigrationIds.Contains($migrationId)) {
            Write-LogLine -Message "Skipping already applied migration: $migrationId"
            continue
        }

        if (-not (Test-Path -LiteralPath $migrationPath -PathType Leaf)) {
            throw "Migration file not found: $migrationPath"
        }

        Invoke-Migration -ConnectionString $connectionString -MigrationId $migrationId -ScriptPath $migrationPath -Transactional $transactional
        $executedMigrations.Add($migrationId)
        Write-LogLine -Message "Applied migration: $migrationId"
    }

    if ($executedMigrations.Count -eq 0) {
        Write-LogLine -Message "No pending database migrations were found."
    }
}

Copy-DirectoryContents -SourceDirectory $resolvedPackagePath -DestinationDirectory $resolvedInstallPath -ExcludeRelativePaths @("appsettings.Production.json")
Write-LogLine -Message "Copied backend package contents into install path."

if (-not [string]::IsNullOrWhiteSpace($resolvedFrontendPackagePath) -and -not [string]::IsNullOrWhiteSpace($resolvedFrontendInstallPath)) {
    if (-not (Test-Path -LiteralPath $resolvedFrontendPackagePath -PathType Container)) {
        throw "Frontend package path does not exist: $resolvedFrontendPackagePath"
    }

    Copy-DirectoryContents -SourceDirectory $resolvedFrontendPackagePath -DestinationDirectory $resolvedFrontendInstallPath -ExcludeRelativePaths @("config.json")
    Write-LogLine -Message "Copied frontend package contents into frontend install path."
}

if (-not $SkipServiceRestart) {
    $serviceInstallerPath = Join-Path $resolvedPackagePath "operations\scripts\infra\install-windows-service.ps1"
    if (-not (Test-Path -LiteralPath $serviceInstallerPath -PathType Leaf)) {
        throw "Service installer script not found in package: $serviceInstallerPath"
    }

    & $serviceInstallerPath -ServiceName $backendServiceName -BinaryPath $installedExePath -Force
    Write-LogLine -Message "Windows service upgrade completed through install-windows-service.ps1."
}
else {
    Write-LogLine -Message "Service restart/update skipped by request." -Level "WARN"
}

$backendValidationScript = Join-Path $resolvedPackagePath "Database\090_Validate_Integrity_And_Continuity.sql"
if (-not $SkipDatabase) {
    Invoke-DatabaseValidationScript -ConnectionString $connectionString -ScriptPath $backendValidationScript
    Write-LogLine -Message "Database validation completed."
}

$healthBaseUrl = Get-KestrelBaseUrl -BackendConfig $backendConfig
if (-not $SkipServiceRestart -and -not [string]::IsNullOrWhiteSpace($healthBaseUrl)) {
    Test-HealthEndpoint -BaseUrl $healthBaseUrl
    Write-LogLine -Message "Health endpoint validation completed successfully."
}

Write-LogLine -Message "Upgrade completed successfully."
Write-LogLine -Message "Log file: $script:LogFilePath"
