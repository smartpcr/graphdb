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
Import-Module "$moduleFolder\Common.psm1" -Force
Import-Module "$moduleFolder\KeyVaultUtil.psm1" -Force


Write-Host "1. retrieve environment settings..."
$bootstrapValues = Get-EnvironmentSettings -EnvName $EnvName -EnvRootFolder "$scriptFolder\env"
$azAccount = LoginAzure -SubscriptionName $bootstrapValues.global.subscriptionName

Write-Host "2. Ensure keyvault is created "
Ensure-KeyVault -rgName $bootstrapValues.global.resourceGroup -vaultName $bootstrapValues.kv.name -location $bootstrapValues.global.location

$spnName = $bootstrapValues.global.servicePrincipal

$spn = Get-OrCreateServicePrincipal -ServicePrincipalName  -ServicePrincipalPwdSecretName