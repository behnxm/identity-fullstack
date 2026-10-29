# PowerShell Local Environment Initializer for Identity Fullstack
$ErrorActionPreference = "Stop"

$scriptDir = if ($PSScriptRoot) { $PSScriptRoot } else { (Get-Location).Path }
$repoRoot = (Resolve-Path (Join-Path $scriptDir "..")).Path
$secretsDir = Join-Path $repoRoot "secrets"
$envFile = Join-Path $repoRoot ".env"
$envExample = Join-Path $repoRoot ".env.example"

Write-Host "==> Initializing Identity Fullstack environment..." -ForegroundColor Cyan

# 1. Create secrets directory
if (-not (Test-Path $secretsDir)) {
    New-Item -ItemType Directory -Path $secretsDir | Out-Null
}

# 2. Generate ES256 ECDSA P-256 key pair if not already existing
$keyFile = Join-Path $secretsDir "signing-key.pem"
$pubKeyFile = Join-Path $secretsDir "public-key.pem"

if (-not (Test-Path $keyFile)) {
    Write-Host "==> Generating ES256 (P-256) ECDSA signing key via .NET Cryptography..." -ForegroundColor Yellow
    $curve = [System.Security.Cryptography.ECCurve+NamedCurves]::nistP256
    $ecdsa = [System.Security.Cryptography.ECDsa]::Create($curve)
    
    $keyPem = $ecdsa.ExportECPrivateKeyPem()
    Set-Content -Path $keyFile -Value $keyPem -Encoding Ascii
    Write-Host "==> Saved private key to secrets/signing-key.pem" -ForegroundColor Green

    $pubKeyPem = $ecdsa.ExportSubjectPublicKeyInfoPem()
    Set-Content -Path $pubKeyFile -Value $pubKeyPem -Encoding Ascii
    Write-Host "==> Saved public key to secrets/public-key.pem" -ForegroundColor Green
} else {
    Write-Host "==> Signing key already exists at secrets/signing-key.pem" -ForegroundColor Gray
}

# 3. Generate .env from template if missing
if (-not (Test-Path $envFile)) {
    Write-Host "==> Generating .env from .env.example..." -ForegroundColor Yellow
    $rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    
    $dbBytes = [byte[]]::new(16)
    $rng.GetBytes($dbBytes)
    $dbPassword = [Convert]::ToHexString($dbBytes).ToLower()

    $demoBytes = [byte[]]::new(12)
    $rng.GetBytes($demoBytes)
    $demoPassword = "Pass!" + [Convert]::ToBase64String($demoBytes)

    $sessionBytes = [byte[]]::new(24)
    $rng.GetBytes($sessionBytes)
    $sessionPassword = [Convert]::ToHexString($sessionBytes).ToLower()

    $content = Get-Content $envExample -Raw
    $content = $content.Replace("replace_with_strong_random_db_password", $dbPassword)
    $content = $content.Replace("replace_with_strong_random_demo_password", $demoPassword)
    $content = $content.Replace("replace_with_at_least_32_characters_session_seal_secret", $sessionPassword)

    Set-Content -Path $envFile -Value $content -Encoding Utf8
    Write-Host "==> Created local .env configuration file." -ForegroundColor Green
} else {
    Write-Host "==> Using existing .env file." -ForegroundColor Gray
}

Write-Host ""
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "                  DEMO CREDENTIALS & QUICK START                 " -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "  Admin Account:   admin@identity.local (or admin@demo.test)" -ForegroundColor White
Write-Host "  Support Account: support@identity.local (or support@demo.test)" -ForegroundColor White
Write-Host "  User Account:    user@identity.local (or user@demo.test)" -ForegroundColor White
Write-Host "  Password:        Password!12345678" -ForegroundColor White
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "Environment initialization complete." -ForegroundColor Green
