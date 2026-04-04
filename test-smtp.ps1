###############################################################################
# SMTP Connectivity Test - AScheduler
# Tests whether the SMTP credentials can authenticate against the server.
# Usage: .\test-smtp.ps1
###############################################################################

param(
    [string]$Host     = "smtp-mail.outlook.com",
    [int]   $Port     = 587,
    [string]$Username = "arturo3595@hotmail.com",
    [string]$Password = "",       # Paste your App Password here or pass as param
    [string]$To       = "arturo3595@hotmail.com"
)

if ([string]::IsNullOrWhiteSpace($Password)) {
    $Password = Read-Host "Enter SMTP password / App Password" -AsSecureString |
        ForEach-Object { [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
            [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($_)) }
}

Write-Host ""
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  AScheduler - SMTP Authentication Test"         -ForegroundColor Cyan
Write-Host "=================================================" -ForegroundColor Cyan
Write-Host "  Host    : $Host"
Write-Host "  Port    : $Port"
Write-Host "  User    : $Username"
Write-Host "  Send To : $To"
Write-Host "-------------------------------------------------" -ForegroundColor DarkGray
Write-Host ""

try {
    Add-Type -AssemblyName System.Net.Mail

    $smtp = New-Object System.Net.Mail.SmtpClient($Host, $Port)
    $smtp.EnableSsl   = $true
    $smtp.Credentials = New-Object System.Net.NetworkCredential($Username, $Password)
    $smtp.Timeout     = 15000

    $msg         = New-Object System.Net.Mail.MailMessage
    $msg.From    = $Username
    $msg.To.Add($To)
    $msg.Subject = "[AScheduler] SMTP Test - $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
    $msg.Body    = "This is a test email from AScheduler SMTP test script."

    Write-Host "[1/2] Connecting and authenticating..." -ForegroundColor Yellow
    $smtp.Send($msg)
    Write-Host "[2/2] SUCCESS - Email sent to $To" -ForegroundColor Green
    Write-Host ""
    Write-Host "Result: Credentials ARE valid. Backend config should work." -ForegroundColor Green
}
catch [System.Net.Mail.SmtpException] {
    Write-Host ""
    Write-Host "FAILED - SMTP Error:" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
    Write-Host ""
    if ($_.Exception.Message -match "5.7.139|basic authentication is disabled") {
        Write-Host "Diagnosis: Microsoft has disabled Basic Auth for this account." -ForegroundColor Yellow
        Write-Host "App Passwords do NOT bypass this server-side policy on Outlook.com." -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Options:" -ForegroundColor Cyan
        Write-Host "  1. Use Gmail: enable 2FA + App Password (Basic Auth still allowed)"
        Write-Host "  2. Use a transactional provider: SendGrid, Mailgun, Brevo (free tier)"
        Write-Host "  3. Use Microsoft Graph API (OAuth2) for Outlook accounts"
    }
    elseif ($_.Exception.Message -match "535|authentication") {
        Write-Host "Diagnosis: Wrong username or password / App Password." -ForegroundColor Yellow
    }
    else {
        Write-Host "Diagnosis: Connection or TLS issue. Check host/port." -ForegroundColor Yellow
    }
}
catch {
    Write-Host ""
    Write-Host "FAILED - Unexpected error:" -ForegroundColor Red
    Write-Host "  $($_.Exception.Message)" -ForegroundColor Red
}
