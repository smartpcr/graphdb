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
        [string]$VaultName
    )

    $certName = "$SpnName-cert"
    
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
    
    LogInfo -Message "Granting spn '$ServicePrincipalName' 'Contributor' role to resource group '$rgName'"
    az ad sp create-for-rbac `
        --name $ServicePrincipalName `
        --password $($servicePrincipalPwd.value) `
        --role="Contributor" `
        --scopes=$scopes | Out-Null
    
    $spn = az ad sp list --display-name $ServicePrincipalName | ConvertFrom-Json

    LogInfo -Message "Grant required resource access for aad app..."
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
        [string] $VaultName,
        [string] $CertSecretName 
    )
    $certSecret = Get-AzureKeyVaultSecret -VaultName $VaultName -Name $CertSecretName 

    $kvSecretBytes = [System.Convert]::FromBase64String($certSecret.SecretValueText)
    $certDataJson = [System.Text.Encoding]::UTF8.GetString($kvSecretBytes) | ConvertFrom-Json
    $pfxBytes = [System.Convert]::FromBase64String($certDataJson.data)
    $flags = [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::MachineKeySet -bxor [System.Security.Cryptography.X509Certificates.X509KeyStorageFlags]::PersistKeySet
    $pfx = new-object System.Security.Cryptography.X509Certificates.X509Certificate2

    $certPwdSecretName = "$CertSecretName-pwd"
    $certPwdSecret = Get-OrCreatePasswordInVault -vaultName $VaultName -secretName $certPwdSecretName

    $pfx.Import($pfxBytes, $certPwdSecret.SecretValue, $flags)
    $thumbprint = $pfx.Thumbprint

    $certAlreadyExists = Test-Path Cert:\CurrentUser\My\$thumbprint
    if (!$certAlreadyExists) {
        $x509Store = new-object System.Security.Cryptography.X509Certificates.X509Store -ArgumentList My, CurrentUser
        $x509Store.Open('ReadWrite')
        $x509Store.Add($pfx)
    }

    return $pfx 
}

function Ensure-KeyVault {
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

    $kvs = az keyvault list --resource-group $bootstrapValues.kv.resourceGroup --query "[?name=='$($vaultName)']" | ConvertFrom-Json
    if ($kvs.Count -eq 0) {
        Write-Host -Message "Creating Key Vault $($vaultName)..." 
        
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
        Write-Host "Key vault $($vaultName) is already created" 
    }
}