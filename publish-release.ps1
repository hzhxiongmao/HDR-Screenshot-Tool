# HDR Screenshot Tool - Release Publisher
# Usage: .\publish-release.ps1 [-Version "v1.0"] [-Message "commit message"]

param(
    [string]$Version = "v1.0",
    [string]$Message = "Release $Version"
)

$ErrorActionPreference = "Stop"
$Root = Split-Path -Parent $MyInvocation.MyCommand.Path
$AppDir = "$Root\App"
$NativeDir = "$Root\NativeCapture"
$PublishDir = "$Root\publish-sc"
$ZipFile = "$Root\HDRScreenshotTool_$Version.zip"
$Tag = $Version

Write-Host "=== 1/6 Building NativeCapture.dll (Release x64, static linking) ===" -ForegroundColor Cyan
& "C:\Program Files\Microsoft Visual Studio\18\Community\MSBuild\Current\Bin\MSBuild.exe" "$NativeDir\NativeCapture.vcxproj" -p:Configuration=Release -p:Platform=x64 -verbosity:minimal
Copy-Item "$NativeDir\x64\Release\NativeCapture.dll" "$AppDir\NativeCapture.dll" -Force

Write-Host "=== 2/6 Publishing .NET app (self-contained, single-file) ===" -ForegroundColor Cyan
Remove-Item -Recurse -Force $PublishDir -ErrorAction SilentlyContinue
Push-Location $AppDir
dotnet publish -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -o $PublishDir
Pop-Location

Write-Host "=== 3/6 Copying NativeCapture.dll to publish folder ===" -ForegroundColor Cyan
Copy-Item "$NativeDir\x64\Release\NativeCapture.dll" "$PublishDir\NativeCapture.dll" -Force

Write-Host "=== 4/6 Creating zip package ===" -ForegroundColor Cyan
Remove-Item $ZipFile -Force -ErrorAction SilentlyContinue
Compress-Archive -Path "$PublishDir\*" -DestinationPath $ZipFile -Force

Write-Host "=== 5/6 Committing & pushing code ===" -ForegroundColor Cyan
Push-Location $Root
git add -A
$hasChanges = git status --porcelain
if ($hasChanges) {
    git commit -m "$Message"
}
git push origin master
Pop-Location

Write-Host "=== 6/6 Creating GitHub Release ===" -ForegroundColor Cyan
$Repo = "hzhxiongmao/HDR-Screenshot-Tool"
# Check if tag exists, create if not
$tagExists = git -C $Root tag -l $Tag
if (-not $tagExists) {
    git -C $Root tag $Tag
    git -C $Root push origin $Tag
}

# Create release via GitHub API using git credentials
$cred = "url=https://github.com" | git credential fill
$token = ($cred | Select-String "password=(.*)").Matches.Groups[1].Value

$body = @{
    tag_name = $Tag
    name = $Tag
    body = "Release $Tag"
    draft = $false
    prerelease = $false
} | ConvertTo-Json

$release = Invoke-RestMethod -Uri "https://api.github.com/repos/$Repo/releases" `
    -Method Post `
    -Headers @{ Authorization = "token $token"; Accept = "application/vnd.github+json" } `
    -ContentType "application/json" `
    -Body $body

# Delete old assets with same name
foreach ($asset in $release.assets) {
    if ($asset.name -eq "HDRScreenshotTool_$Version.zip") {
        Invoke-RestMethod -Uri $asset.url -Method Delete -Headers @{ Authorization = "token $token" }
    }
}

# Upload new asset
$uploadUrl = $release.upload_url -replace '\{.*\}', "?name=HDRScreenshotTool_$Version.zip"
Invoke-RestMethod -Uri $uploadUrl `
    -Method Post `
    -Headers @{ Authorization = "token $token"; ContentType = "application/zip" } `
    -InFile $ZipFile

Write-Host "`n=== DONE ===" -ForegroundColor Green
Write-Host "Release: https://github.com/$Repo/releases/tag/$Tag" -ForegroundColor Green
Write-Host "Download: $($release.assets[0].browser_download_url)" -ForegroundColor Green
