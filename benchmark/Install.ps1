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
Import-Module "$moduleFolder\CosmosDbUtil.psm1" -Force
Import-Module "$moduleFolder\KeyVaultUtil.psm1" -Force


Write-Host "1. retrieve environment settings..." -ForegroundColor Green
$bootstrapValues = Get-EnvironmentSettings -EnvName $EnvName -EnvRootFolder $envFolder
$azAccount = LoginAzure -SubscriptionName $bootstrapValues.global.subscriptionName

Write-Host "2. Ensure keyvault is created " -ForegroundColor Green
EnsureKeyVault -rgName $bootstrapValues.global.resourceGroup -vaultName $bootstrapValues.kv.name -location $bootstrapValues.global.location

Write-Host "3. Ensure service principal is created and assigned proper permission to key vault..." -ForegroundColor Green
$spnName = $bootstrapValues.global.servicePrincipal
$spnCertSecretName = "$spnName-cert"
$sp = Get-OrCreateServicePrincipalWithCert -SpnName $spnName -SpnCertName $spnCertSecretName -VaultName $bootstrapValues.kv.name -EnvFolder $envFolder -AzureAccount $azAccount

Write-Host "4. Install service principal certificate to cert:\CurrentUsers\My" -ForegroundColor Green
$cert = Install-CertFromVaultSecret -VaultName $bootstrapValues.kv.name -CertSecretName $spnCertSecretName 

Write-Host "5. Ensure doc db is created..." -ForegroundColor Green
EnsureCosmosDbAccount -AccountName $bootstrapValues.docdb.account -API $bootstrapValues.docdb.api -ResourceGroupName $bootstrapValues.global.resourceGroup -Location $bootstrapValues.global.location 
$docdbPrimaryMasterKey = GetCosmosDbAccountKey -AccountName $bootstrapValues.docdb.account -ResourceGroupName $bootstrapValues.global.resourceGroup
$docDbKeySecretName = "docdb-key"
az keyvault secret set --vault-name $bootstrapValues.kv.name --name $docDbKeySecretName --value $docdbPrimaryMasterKey | Out-Null
EnsureDatabaseExists -Endpoint "https://$($bootstrapValues.docdb.account).documents.azure.com:443/" -MasterKey $docdbPrimaryMasterKey -DatabaseName $bootstrapValues.docdb.db | Out-Null
EnsureCollectionExists `
    -AccountName $bootstrapValues.docdb.account `
    -ResourceGroupName $bootstrapValues.global.resourceGroup `
    -DbName $bootstrapValues.docdb.db `
    -CollectionName $bootstrapValues.docdb.collection `
    -CosmosDbKey $docdbPrimaryMasterKey


Write-Host "6. Ensure graph db is created..." -ForegroundColor Green
EnsureCosmosDbAccount -AccountName $bootstrapValues.graphdb.account -API $bootstrapValues.graphdb.api -ResourceGroupName $bootstrapValues.global.resourceGroup -Location $bootstrapValues.global.location 
$graphdbPrimaryMasterKey = GetCosmosDbAccountKey -AccountName $bootstrapValues.graphdb.account -ResourceGroupName $bootstrapValues.global.resourceGroup
$graphDbKeySecretName = "graphdb-key"
az keyvault secret set --vault-name $bootstrapValues.kv.name --name $graphDbKeySecretName --value $graphdbPrimaryMasterKey | Out-Null
EnsureDatabaseExists -Endpoint "https://$($bootstrapValues.graphdb.account).documents.azure.com:443/" -MasterKey $graphdbPrimaryMasterKey -DatabaseName $bootstrapValues.graphdb.db | Out-Null
EnsureCollectionExists `
    -AccountName $bootstrapValues.graphdb.account `
    -ResourceGroupName $bootstrapValues.global.resourceGroup `
    -DbName $bootstrapValues.graphdb.db `
    -CollectionName $bootstrapValues.graphdb.collection `
    -CosmosDbKey $graphdbPrimaryMasterKey

Write-Host "5. Output docdb settings" -ForegroundColor Green
$docDbSettings = @{
    ServicePrincipal = @{
        ApplicationId         = $sp.appId
        CertificateThumbprint = $cert.Thumbprint 
    }
    Vault            = @{
        Name    = $bootstrapValues.kv.name
        Secrets = @{
            SpnCert    = $spnCertSecretName
            DocDbKey   = $docDbKeySecretName
            GraphDbKey = $graphDbKeySecretName
        }
    }
    DocDb            = @{
        Name       = $bootstrapValues.docdb.account
        Db         = $bootstrapValues.docdb.db
        Collection = $bootstrapValues.docdb.collection
    }
    GraphDb          = @{
        Name       = $bootstrapValues.graphdb.account
        Db         = $bootstrapValues.graphdb.db
        Collection = $bootstrapValues.graphdb.collection
    }
}

$secretFile = Join-Path $envFolder "secrets.json"
Write-Host "Copy settings from '$secretFile' to your project appsettings.json..." -ForegroundColor Green
$docDbSettings | ConvertTo-Json | Out-File $secretFile
