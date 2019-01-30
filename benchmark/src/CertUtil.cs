using System.Security.Cryptography.X509Certificates;

namespace Benchmark
{
    public static class CertUtil
    {
        public static X509Certificate2 FindCertificateByThumbprint(string certThumbprint, StoreLocation storeLocation = StoreLocation.CurrentUser,
            StoreName storeName = StoreName.My)
        {
            X509Store store = new X509Store(storeName, storeLocation);
            store.Open(OpenFlags.ReadOnly);
            X509Certificate2Collection col = store.Certificates.Find(X509FindType.FindByThumbprint, certThumbprint, false); // Don't validate certs, since the test root isn't installed.
            if (col.Count != 0)
            {
                foreach (X509Certificate2 cert in col)
                {
                    if (cert.HasPrivateKey)
                    {
                        store.Close();
                        return cert;
                    }
                }
            }

            store.Close();

            return null;
        }
    }
}
