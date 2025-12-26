
# PowerShell script to build the application in Docker
# Debug or Release
$config = "Release"

$outputDir = if ($config -eq "Debug") { "debug_output" } else { "output" }

Write-Host "Building StellasoraPotentialOverlay in Docker..." -ForegroundColor Cyan

# Dockerイメージをビルド
Write-Host "`nBuilding Docker image..." -ForegroundColor Yellow
docker-compose build

if ($LASTEXITCODE -ne 0) {
    Write-Host "Docker image build failed!" -ForegroundColor Red
    exit 1
}

# プロジェクトをリストア
Write-Host "`nRestoring NuGet packages..." -ForegroundColor Yellow
docker-compose run --rm builder dotnet restore src/StellasoraPotentialOverlay.csproj

if ($LASTEXITCODE -ne 0) {
    Write-Host "NuGet restore failed!" -ForegroundColor Red
    exit 1
}

# プロジェクトをビルド
Write-Host "`nBuilding project..." -ForegroundColor Yellow
docker-compose run --rm builder dotnet build src/StellasoraPotentialOverlay.csproj -c $config

if ($LASTEXITCODE -ne 0) {
    Write-Host "Build failed!" -ForegroundColor Red
    exit 1
}

# シングルファイルEXEとして公開
Write-Host "`nPublishing as single-file executable..." -ForegroundColor Yellow
docker-compose run --rm builder dotnet publish src/StellasoraPotentialOverlay.csproj `
    -c $config `
    -r win-x64 `
    --self-contained true `
    "-p:PublishSingleFile=$($config -eq 'Release')" `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $outputDir

if ($LASTEXITCODE -ne 0) {
    Write-Host "Publish failed!" -ForegroundColor Red
    exit 1
}

Write-Host "`nBuild completed successfully!" -ForegroundColor Green
Write-Host "Output directory: output/" -ForegroundColor Cyan
Write-Host "Executable: output/StellasoraPotentialOverlay.exe" -ForegroundColor Cyan
