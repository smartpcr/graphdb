function EnsureCertificateInKeyVault {
    param(
        [string] $VaultName,
        [string] $CertName,
        [string] $EnvFolder
    )

    $existingCert = az keyvault certificate list --vault-name $VaultName --query "[?id=='https://$VaultName.vault.azure.net/certificates/$CertName']" | ConvertFrom-Json
    if ($existingCert) {
        Write-Host "Certificate '$CertName' already exists in vault '$VaultName'" -ForegroundColor Yellow
    }
    else {
        Write-Host "Creating certificate '$CertName' in vault '$VaultName'..." -ForegroundColor Green
        $credentialFolder = Join-Path $ScriptFolder "credential"
        New-Item -Path $credentialFolder -ItemType Directory -Force | Out-Null
        $defaultPolicyFile = Join-Path $credentialFolder "default_policy.json"
        az keyvault certificate get-default-policy -o json | Out-File $defaultPolicyFile -Encoding utf8 
        az keyvault certificate create -n $CertName --vault-name $vaultName -p @$defaultPolicyFile | Out-Null
    }
}

function Get-OrCreatePasswordInVault { 
    param(
        [string] $VaultName, 
        [string] $SecretName
    )

    $secretsFound = az keyvault secret list `
        --vault-name $VaultName `
        --query "[?id=='https://$($VaultName).vault.azure.net/secrets/$SecretName']" | ConvertFrom-Json
    if (!$secretsFound) {
        $prng = [System.Security.Cryptography.RNGCryptoServiceProvider]::Create()
        $bytes = New-Object Byte[] 30
        $prng.GetBytes($bytes)
        $password = [System.Convert]::ToBase64String($bytes) + "!@1wW" #  ensure we meet password requirements
        az keyvault secret set --vault-name $VaultName --name $SecretName --value $password
        $res = az keyvault secret show --vault-name $VaultName --name $SecretName | ConvertFrom-Json
        return $res 
    }

    $res = az keyvault secret show --vault-name $VaultName --name $SecretName | ConvertFrom-Json
    if ($res) {
        return $res
    }
}

function Get-OrCreateServicePrincipalWithCert {
    param(
        [string]$SpnName,
        [string]$SpnCertName,
        [string]$VaultName,
        [string]$ResourceGroupName,
        [string]$EnvFolder,
        $AzureAccount
    )

    $sp = az ad sp list --display-name $SpnName | ConvertFrom-Json
    if (!$sp) {
        Write-Host "Creating service principal with name '$SpnName'..." -ForegroundColor Green
        
        EnsureCertificateInKeyVault -VaultName $VaultName -CertName $SpnCertName -EnvFolder $EnvFolder

        az ad sp create-for-rbac -n $SpnName --role contributor --keyvault $VaultName --cert $SpnCertName | Out-Null
        $sp = az ad sp list --display-name $SpnName | ConvertFrom-Json
        Write-Host "Granting spn '$SpnName' 'contributor' role to subscription" -ForegroundColor Green
        az role assignment create --assignee $sp.appId --role Contributor --scope "/subscriptions/$($AzureAccount.id)" | Out-Null

        Write-Host "Granting spn '$($SpnName)' permissions to keyvault '$($VaultName)'" -ForegroundColor Green
        az keyvault set-policy `
            --name $VaultName `
            --resource-group $ResourceGroupName `
            --object-id $sp.objectId `
            --spn $sp.displayName `
            --certificate-permissions get list update delete `
            --secret-permissions get list set delete | Out-Null
    }
    else {
        Write-Host "Service principal '$SpnName' already exists." -ForegroundColor Yellow 
    }

    return $sp 
}


function Get-OrCreateServicePrincipalWithPwd {
    param(
        [string] $ServicePrincipalName,
        [string] $ServicePrincipalPwdSecretName,
        [string] $VaultName,
        [string] $EnvRootFolder,
        [string] $EnvName
    )

    $templatesFolder = Join-Path $EnvRootFolder "templates"
    $spnAuthJsonFile = Join-Path $templatesFolder "aks-spn-auth.json"
    $servicePrincipalPwd = Get-OrCreatePasswordInVault2 -VaultName $VaultName -secretName $ServicePrincipalPwdSecretName
    $spFound = az ad sp list --display-name $ServicePrincipalName | ConvertFrom-Json
    if ($spFound) {
        az ad sp credential reset --name $ServicePrincipalName --password $servicePrincipalPwd.value 
        $spn = az ad sp list --display-name $ServicePrincipalName | ConvertFrom-Json
        az ad app update --id $spn.appId --required-resource-accesses $spnAuthJsonFile | Out-Null
        return $spn
    }

    $bootstrapValues = Get-EnvironmentSettings -EnvName $EnvName -EnvRootFolder $EnvRootFolder
    $rgName = $bootstrapValues.aks.resourceGroup
    $azAccount = az account show | ConvertFrom-Json
    $subscriptionId = $azAccount.id
    $scopes = "/subscriptions/$subscriptionId/resourceGroups/$($rgName)"
    
    Write-Host "Granting spn '$ServicePrincipalName' 'Contributor' role to resource group '$rgName'"
    az ad sp create-for-rbac `
        --name $ServicePrincipalName `
        --password $($servicePrincipalPwd.value) `
        --role="Contributor" `
        --scopes=$scopes | Out-Null
    
    $spn = az ad sp list --display-name $ServicePrincipalName | ConvertFrom-Json

    Write-Host "Grant required resource access for aad app..."
    az ad app update --id $spn.appId --required-resource-accesses $spnAuthJsonFile | Out-Null
    az ad app update --id $spn.appId --reply-urls "http://$($ServicePrincipalName)" | Out-Null
    
    return $spn 
}

function New-CertificateAsSecret {
    param(
        [string] $CertName,
        [string] $VaultName 
    )

    $cert = New-SelfSignedCertificate `
        -CertStoreLocation "cert:\CurrentUser\My" `
        -Subject "CN=$CertName" `
        -KeySpec KeyExchange `
        -HashAlgorithm "SHA256" `
        -Provider "Microsoft Enhanced RSA and AES Cryptographic Provider"
    $certPwdSecretName = "$CertName-pwd"
    $spCertPwdSecret = Get-OrCreatePasswordInVault -vaultName $VaultName -secretName $certPwdSecretName
    $pwd = $spCertPwdSecret.SecretValue
    $pfxFilePath = [System.IO.Path]::GetTempFileName() 
    Export-PfxCertificate -cert $cert -FilePath $pfxFilePath -Password $pwd -ErrorAction Stop | Out-Null
    $Bytes = [System.IO.File]::ReadAllBytes($pfxFilePath)
    $Base64 = [System.Convert]::ToBase64String($Bytes)
    $JSONBlob = @{
        data     = $Base64
        dataType = 'pfx'
        password = $spCertPwdSecret.SecretValueText
    } | ConvertTo-Json
    $ContentBytes = [System.Text.Encoding]::UTF8.GetBytes($JSONBlob)
    $Content = [System.Convert]::ToBase64String($ContentBytes)
    $SecretValue = ConvertTo-SecureString -String $Content -AsPlainText -Force
    Set-AzureKeyVaultSecret -VaultName $VaultName -Name $CertName -SecretValue $SecretValue | Out-Null

    Remove-Item $pfxFilePath
    Remove-Item "cert:\\CurrentUser\My\$($cert.Thumbprint)"

    return $cert
}

function Install-CertFromVaultSecret {
    param(
        [string] $VaultName = "rrdult-kv",
        [string] $CertSecretName = "bm-dev-xd-wus2-spn-cert"
    )

    $certFile = Join-Path ([System.IO.Path]::GetTempPath()) "$CertSecretName.pfx"
    az keyvault secret download --file $certFile --vault-name $VaultName --name $CertSecretName
    $cert = Import-PfxCertificate -FilePath $certFile -Exportable -CertStoreLocation "cert:\CurrentUser\My" 
    Remove-Item $certFile -Force
    return $cert 
}

function EnsureKeyVault {
    param(
        [string] $rgName,
        [string] $vaultName,
        [string] $location 
    )

    $rgGroups = az group list --query "[?name=='$($rgName)']" | ConvertFrom-Json
    if (!$rgGroups -or $rgGroups.Count -eq 0) {
        Write-Host "Creating resource group '$($rgName)' in location '$($location)'"
        az group create --name $rgName --location $location | Out-Null
    }

    $kvs = az keyvault list --resource-group $rgName --query "[?name=='$($vaultName)']" | ConvertFrom-Json
    if ($kvs.Count -eq 0) {
        Write-Host "Creating Key Vault $($vaultName)..." -ForegroundColor Green
        
        az keyvault create `
            --resource-group $rgName `
            --name $($vaultName) `
            --sku standard `
            --location $location `
            --enabled-for-deployment $true `
            --enabled-for-disk-encryption $true `
            --enabled-for-template-deployment $true | Out-Null
    }
    else {
        Write-Host "Key vault $($vaultName) is already created" -ForegroundColor Yellow
    }
}