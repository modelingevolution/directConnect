# Set variables
$projectName = "ModelingEvolution.DirectConnect"
$version = "1.1.0.1"
$configuration = "Release"

# Clean previous builds
dotnet clean

# Restore dependencies
dotnet restore

# Build the project
dotnet build --configuration $configuration /p:Version=$version

# Get the API key from .api file
$apiKeyPath = ".api"
if (Test-Path $apiKeyPath) {
    $apiKey = Get-Content $apiKeyPath -Raw
    $apiKey = $apiKey.Trim()  # Remove any whitespace
} else {
    Write-Error "API key file '.api' not found. Please create it with your NuGet.org API key."
    exit 1
}

# Check if package exists
$packagePath = ".\bin\Release\$projectName.$version.nupkg"
if (-not (Test-Path $packagePath)) {
    Write-Error "Package not found at: $packagePath"
    exit 1
}

# Publish to NuGet.org
dotnet nuget push $packagePath --source https://api.nuget.org/v3/index.json --api-key $apiKey
