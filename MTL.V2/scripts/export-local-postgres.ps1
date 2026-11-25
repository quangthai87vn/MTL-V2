[CmdletBinding()]
param(
    [string]$OutputDirectory = "backups\transfer",
    [string]$FileName = "",
    [string]$DataProtectionVolume = "mtl-v2_data-protection-keys"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$projectDirectory = (Resolve-Path (Join-Path $PSScriptRoot "..")).Path
if (-not [System.IO.Path]::IsPathRooted($OutputDirectory)) {
    $OutputDirectory = Join-Path $projectDirectory $OutputDirectory
}

if ([string]::IsNullOrWhiteSpace($FileName)) {
    $FileName = "mtl-v2-local-{0}.dump" -f (Get-Date -Format "yyyyMMdd-HHmmss")
}
if ([System.IO.Path]::GetExtension($FileName) -ne ".dump") {
    throw "FileName must use the .dump extension."
}
if ([System.IO.Path]::GetFileName($FileName) -ne $FileName -or $FileName -notmatch '^[A-Za-z0-9._-]+$') {
    throw "FileName may contain only letters, numbers, dots, underscores, and hyphens."
}

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputPath = Join-Path $OutputDirectory $FileName
$keyBundleName = "{0}.data-protection-keys.tar.gz" -f [System.IO.Path]::GetFileNameWithoutExtension($FileName)
$keyBundlePath = Join-Path $OutputDirectory $keyBundleName
if (Test-Path -LiteralPath $outputPath) {
    throw "Backup already exists: $outputPath"
}
if (Test-Path -LiteralPath $keyBundlePath) {
    throw "Data Protection key bundle already exists: $keyBundlePath"
}

$containerDump = "/tmp/mtl-v2-initial-data.dump"

Push-Location $projectDirectory
try {
    docker compose up -d --wait postgres
    if ($LASTEXITCODE -ne 0) {
        throw "PostgreSQL local could not be started."
    }

    $containerId = (docker compose ps -q postgres).Trim()
    if ([string]::IsNullOrWhiteSpace($containerId)) {
        throw "PostgreSQL local container was not found."
    }

    docker compose exec -T postgres sh -lc 'pg_dump -U "$POSTGRES_USER" -d "$POSTGRES_DB" --format=custom --no-owner --no-privileges --file=/tmp/mtl-v2-initial-data.dump'
    if ($LASTEXITCODE -ne 0) {
        throw "pg_dump failed."
    }

    docker compose exec -T postgres pg_restore --list $containerDump | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "The generated PostgreSQL archive is invalid."
    }

    docker cp "${containerId}:$containerDump" $outputPath | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Could not copy the PostgreSQL archive from Docker."
    }

    docker volume inspect $DataProtectionVolume | Out-Null
    if ($LASTEXITCODE -ne 0) {
        throw "Docker volume not found: $DataProtectionVolume"
    }

    docker run --rm `
        --mount "type=volume,source=$DataProtectionVolume,target=/keys,readonly" `
        --mount "type=bind,source=$OutputDirectory,target=/output" `
        postgres:18-alpine `
        sh -ec "find /keys -maxdepth 1 -type f -name 'key-*.xml' | grep -q .; tar -C /keys -czf '/output/$keyBundleName' ."
    if ($LASTEXITCODE -ne 0) {
        throw "Could not export the Data Protection key ring from Docker volume $DataProtectionVolume."
    }
}
finally {
    docker compose exec -T postgres rm -f $containerDump 2>$null | Out-Null
    Pop-Location
}

$backup = Get-Item -LiteralPath $outputPath
if ($backup.Length -le 0) {
    throw "The generated backup is empty: $outputPath"
}

$checksumPath = "$outputPath.sha256"
$checksum = (Get-FileHash -LiteralPath $outputPath -Algorithm SHA256).Hash.ToLowerInvariant()
"$checksum  $($backup.Name)" | Set-Content -LiteralPath $checksumPath -Encoding ascii -NoNewline

$keyBundle = Get-Item -LiteralPath $keyBundlePath
if ($keyBundle.Length -le 0) {
    throw "The generated Data Protection key bundle is empty: $keyBundlePath"
}
$keyChecksumPath = "$keyBundlePath.sha256"
$keyChecksum = (Get-FileHash -LiteralPath $keyBundlePath -Algorithm SHA256).Hash.ToLowerInvariant()
"$keyChecksum  $($keyBundle.Name)" | Set-Content -LiteralPath $keyChecksumPath -Encoding ascii -NoNewline

Write-Host "Local PostgreSQL export completed."
Write-Host "Dump:     $($backup.FullName)"
Write-Host "SHA-256:  $checksumPath"
Write-Host "Size:     $($backup.Length) bytes"
Write-Host "Keys:     $($keyBundle.FullName)"
Write-Host "Keys SHA: $keyChecksumPath"
Write-Host "Do not commit these files to Git. Transfer the dump, key bundle, and both checksum files to the server with SCP."
