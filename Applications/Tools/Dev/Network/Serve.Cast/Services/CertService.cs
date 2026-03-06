using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace ServeCast.Services;

/// <summary>localhost용 자체 서명 인증서 생성·로드</summary>
public static class CertService
{
    private static readonly string CertPath =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "ServeCast", "server.pfx");

    public static X509Certificate2 GetOrCreate()
    {
        var dir = Path.GetDirectoryName(CertPath)!;
        if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

        if (File.Exists(CertPath))
        {
            try { return X509CertificateLoader.LoadPkcs12FromFile(CertPath, password: null); }
            catch { /* 손상된 경우 재생성 */ }
        }

        return CreateAndSave();
    }

    private static X509Certificate2 CreateAndSave()
    {
        using var rsa = RSA.Create(2048);

        var req = new CertificateRequest(
            "CN=localhost", rsa,
            HashAlgorithmName.SHA256,
            RSASignaturePadding.Pkcs1);

        req.CertificateExtensions.Add(new X509BasicConstraintsExtension(false, false, 0, false));
        req.CertificateExtensions.Add(new X509KeyUsageExtension(
            X509KeyUsageFlags.DigitalSignature | X509KeyUsageFlags.KeyEncipherment, false));
        req.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(
            [new Oid("1.3.6.1.5.5.7.3.1")], false));  // Server Authentication

        var san = new SubjectAlternativeNameBuilder();
        san.AddDnsName("localhost");
        san.AddIpAddress(System.Net.IPAddress.Loopback);
        san.AddIpAddress(System.Net.IPAddress.IPv6Loopback);
        req.CertificateExtensions.Add(san.Build());

        var cert = req.CreateSelfSigned(
            DateTimeOffset.Now.AddDays(-1),
            DateTimeOffset.Now.AddYears(10));

        var pfxBytes = cert.Export(X509ContentType.Pfx);
        File.WriteAllBytes(CertPath, pfxBytes);

        return X509CertificateLoader.LoadPkcs12(pfxBytes, password: null);
    }
}
