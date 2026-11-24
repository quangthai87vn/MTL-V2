[CmdletBinding()]
param(
    [string]$OutputDirectory = "backups\transfer",
    [string]$FileName = ""
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

New-Item -ItemType Directory -Force -Path $OutputDirectory | Out-Null
$outputPath = Join-Path $OutputDirectory $FileName
if (Test-Path -LiteralPath $outputPath) {
    throw "Backup already exists: $outputPath"
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

Write-Host "Local PostgreSQL export completed."
Write-Host "Dump:     $($backup.FullName)"
Write-Host "SHA-256:  $checksumPath"
Write-Host "Size:     $($backup.Length) bytes"
Write-Host "Do not commit the dump or checksum file to Git. Transfer both files to the server with SCP."
