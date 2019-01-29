param(
    [string]$EnvName = "dev"
)

$gitRootFolder = $PSScriptRoot
if (!$gitRootFolder) {
    $gitRootFolder = Get-Location
}
if (-not (Test-Path (Join-Path $gitRootFolder ".git") -PathType Container)) {
    $gitRootFolder = Split-Path $gitRootFolder -Parent
}
$scriptFolder = Join-Path $gitRootFolder "benchmark"
$moduleFolder = Join-Path $scriptFolder "modules"
$envFolder = Join-Path $scriptFolder "env"
Import-Module "$moduleFolder\Common.psm1" -Force
Import-Module "$moduleFolder\KeyVaultUtil.psm1" -Force


Write-Host "1. retrieve environment settings..."
$bootstrapValues = Get-EnvironmentSettings -EnvName $EnvName -EnvRootFolder $envFolder
$azAccount = LoginAzure -SubscriptionName $bootstrapValues.global.subscriptionName

Write-Host "2. Ensure keyvault is created "
Ensure-KeyVault -rgName $bootstrapValues.global.resourceGroup -vaultName $bootstrapValues.kv.name -location $bootstrapValues.global.location

Write-Host "3. Ensure service principal is created and assigned proper permission to key vault..."
$spnName = $bootstrapValues.global.servicePrincipal
$spnCertName = "$spnName-cert"
Get-OrCreateServicePrincipalWithCert -SpnName $spnName -SpnCertName $spnCertName -VaultName $bootstrapValues.kv.name -EnvFolder $envFolder -AzureAccount $azAccount

Write-Host "4. Install service principal certificate to cert:\CurrentUsers\My"
Install-CertFromVaultSecret -VaultName $bootstrapValues.kv.name -CertSecretName $spnCertName 
