using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Azure.KeyVault;
using Microsoft.IdentityModel.Clients.ActiveDirectory;

namespace Common.KeyVault
{
    public class KeyVaultUtil
    {
        private readonly string _vaultUrl;
        private KeyVaultClient _client;

        public KeyVaultUtil(string vaultName, string authClientId, X509Certificate2 cert)
        {
            _vaultUrl = $"https://{vaultName}.vault.azure.net";
            var assertCert = new ClientAssertionCertificate(authClientId, cert);
            Initialize((authority, resource, scope) => GetAccessToken(authority, resource, assertCert));
        }

        public async Task<string> GetSecret(string name)
        {
            var secretBundle = await _client.GetSecretVersionsAsync(_vaultUrl, name);
            var latestVersion = secretBundle.Where(b => b.Attributes.Created.HasValue)
                .OrderByDescending(b => b.Attributes.Created).FirstOrDefault()?.Id;
            if (latestVersion == null)
            {
                return null;
            }

            var secret = await _client.GetSecretAsync(latestVersion);
            return secret.Value;
        }

        private void Initialize(KeyVaultClient.AuthenticationCallback authCallback)
        {
            _client = new KeyVaultClient(authCallback);
        }

        private async Task<string> GetAccessToken(string authority, string resource,
            ClientAssertionCertificate assertionCert)
        {
            var context = new AuthenticationContext(authority, TokenCache.DefaultShared);
            var result = await context.AcquireTokenAsync(resource, assertionCert);
            return result.AccessToken;
        }
    }
}
