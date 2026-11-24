param(
    [string]$BaseUrl = "http://localhost:8080",
    [string]$EnvironmentFile = ".env"
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path -LiteralPath $EnvironmentFile)) {
    throw "Không tìm thấy file môi trường: $EnvironmentFile"
}

$settings = @{}
foreach ($line in Get-Content -LiteralPath $EnvironmentFile) {
    if ($line -match '^\s*([^#=]+)=(.*)$') {
        $settings[$matches[1].Trim()] = $matches[2].Trim()
    }
}

$email = $settings["MTL_ADMIN_EMAIL"]
$password = $settings["MTL_ADMIN_PASSWORD"]
if ([string]::IsNullOrWhiteSpace($email) -or [string]::IsNullOrWhiteSpace($password)) {
    throw "Environment file phải có MTL_ADMIN_EMAIL và MTL_ADMIN_PASSWORD."
}

$session = New-Object Microsoft.PowerShell.Commands.WebRequestSession
$csrf = Invoke-RestMethod -Uri "$BaseUrl/api/auth/csrf" -WebSession $session
$headers = @{ "X-CSRF-TOKEN" = $csrf.token }
$loginBody = @{
    email = $email
    password = $password
    rememberMe = $false
} | ConvertTo-Json

$login = Invoke-RestMethod `
    -Uri "$BaseUrl/api/auth/login" `
    -Method Post `
    -WebSession $session `
    -Headers $headers `
    -ContentType "application/json" `
    -Body $loginBody

$me = Invoke-RestMethod -Uri "$BaseUrl/api/auth/me" -WebSession $session

[pscustomobject]@{
    Status = "OK"
    User = $me.email
    Roles = ($login.roles -join ", ")
    MustChangePassword = $me.mustChangePassword
    PermissionCount = $me.permissions.Count
} | Format-List

