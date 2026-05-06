$ErrorActionPreference = "Stop"

function Write-Step($msg) { Write-Host "`n==> $msg" -ForegroundColor Cyan }
function Fail($msg)       { Write-Host "ERROR: $msg" -ForegroundColor Red; exit 1 }

$pluginMasterPath = "pluginmaster.json"
$csprojPath = "SamplePlugin\MarauderMap.csproj"

if (-not (Test-Path $pluginMasterPath)) { Fail "pluginmaster.json not found." }
if (-not (Test-Path $csprojPath))       { Fail "MarauderMap.csproj not found." }

Write-Step "Caching original file contents..."

$pluginMasterOriginal = Get-Content $pluginMasterPath -Raw
$csprojOriginal       = Get-Content $csprojPath -Raw

try {
	Write-Step "Reading version from pluginmaster.json..."

	$pluginMaster = $pluginMasterOriginal | ConvertFrom-Json
	$currentVersion = $pluginMaster[0].AssemblyVersion

	Write-Step "  Current version: $currentVersion"

	$parts = $currentVersion -split '\.'
	$parts[-1] = [int]$parts[-1] + 1
	$Version = $parts -join '.'

	Write-Step "Updating version in MarauderMap.csproj..."

	$content = Get-Content $csprojPath -Raw
	$content = $content -replace '<Version>.*?</Version>', "<Version>$Version</Version>"
	Set-Content $csprojPath $content.trim()

	$lastReleaseHash = (Get-Content "LAST_RELEASE").trim()
	$releaseBody = (git log "$lastReleaseHash..HEAD" --pretty=format:"- %s").trim() | Join-String -Separator '\n'

	Write-Step $releaseBody

	$pluginMaster[0].AssemblyVersion = $Version
	$pluginMaster[0].DownloadLinkInstall = "https://github.com/Sowce/whodis/releases/download/$Version/latest.zip"
	$pluginMaster[0].DownloadLinkTesting = "https://github.com/Sowce/whodis/releases/download/$Version/latest.zip"
	$pluginMaster[0].DownloadLinkUpdate = "https://github.com/Sowce/whodis/releases/download/$Version/latest.zip"
	$pluginMaster[0].Changelog = $releaseBody
	$pluginMaster[0].LastUpdate = [Int64]::Parse((Get-Date -UFormat %s))

	$pluginMaster | ConvertTo-Json -AsArray | Set-Content $pluginMasterPath

	Write-Step "  Updated to $Version"

	Remove-Item -Recurse -Force ".\SamplePlugin\bin\x64" 

	dotnet build MarauderMap.sln --no-incremental --no-restore -c All
	if ($LASTEXITCODE -ne 0) { Fail "Build failed." }

	Write-Step "Creating GitHub release and uploading assets..."

	$zipPath = ".\SamplePlugin\bin\x64\Release\MarauderMap\latest.zip"

	gh release create "$Version" "$zipPath" --notes "$releaseBody"

	if ($LASTEXITCODE -ne 0) { Fail "gh release create failed." }

	git add $pluginMasterPath $csprojPath
	git commit -m "Bump version to $Version"
	git push

	Set-Content "LAST_RELEASE" ((git rev-parse HEAD).Trim())

	if ($LASTEXITCODE -ne 0) { Fail "git push failed." }
} catch {
	Write-Host "Release failed, restoring original files..." -ForegroundColor Yellow

    Set-Content $pluginMasterPath $pluginMasterOriginal.trim()
    Set-Content $csprojPath       $csprojOriginal.trim()

    Write-Host "  Restored: pluginmaster.json" -ForegroundColor Yellow
    Write-Host "  Restored: MarauderMap.csproj" -ForegroundColor Yellow

    Fail $_.Exception.Message
}

Write-Step "Release process completed successfully!"