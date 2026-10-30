$ErrorActionPreference = 'Stop'
$projectRoot = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '../..'))
$webRoot = Join-Path $projectRoot 'Builds/StarlightWorkshopWeb'
foreach ($required in @('index.html', 'Build', 'DEPLOYMENT.txt', 'CREDITS.txt')) {
    if (!(Test-Path -LiteralPath (Join-Path $webRoot $required))) { throw "Missing web output: $required. Run WebReleaseBuild.Run first." }
}
$distribution = Join-Path $projectRoot 'Builds/Distribution'
New-Item -ItemType Directory -Force -Path $distribution | Out-Null
$archive = Join-Path $distribution ('StarlightWorkshopWeb-' + (Get-Date -Format 'yyyyMMdd-HHmmss') + '.zip')
Compress-Archive -Path (Join-Path $webRoot '*') -DestinationPath $archive -CompressionLevel Optimal
Get-FileHash -LiteralPath $archive -Algorithm SHA256 | Format-List
Write-Output $archive
