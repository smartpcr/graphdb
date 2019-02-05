$SubscriptionName = "Compliance_Tools_Eng"
$AccountName = "product-catalog-api-1811d0c3"
$ResourceGroupName = "int-disco-east-us"

$gitRootFolder = $PSScriptRoot
if (!$gitRootFolder) {
    $gitRootFolder = Get-Location
}
while (-not (Test-Path (Join-Path $gitRootFolder ".git") -PathType Container)) {
    $gitRootFolder = Split-Path $gitRootFolder
}

az login | Out-Null
az account set -s $SubscriptionName | Out-Null
$dbAcctKeys = az cosmosdb list-keys --name $AccountName --resource-group $ResourceGroupName | ConvertFrom-Json
$dbKey = $dbAcctKeys.primaryMasterKey

$sourceDbSettings = @{
    account     = $AccountName
    dbName      = "product-catalog"
    authKey     = $dbKey
    collections = @(
        @{
            name  = "policy-catalog"
            query = "select * from c where c.documentType = 'ApplicabilityScope'"
            model = "ApplicabilityScope"
        },
        @{
            name  = "policy-catalog"
            query = "select * from c where c.documentType = 'Control'"
            model = "Control"
        },
        @{
            name  = "products"
            query = "select * from c where c.documentType = 'NodeMapping'"
            model = "NodeMapping"
        }
    )
}
$secretJsonFile = "$gitRootFolder\benchmark\src\Benchmark\secrets.json"
$sourceDbSettings | ConvertTo-Json | Out-File $secretJsonFile -Force