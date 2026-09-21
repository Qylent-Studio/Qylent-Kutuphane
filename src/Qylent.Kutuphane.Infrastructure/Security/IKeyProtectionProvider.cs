using System.Security.Cryptography;

namespace Qylent.Kutuphane.Infrastructure.Security;

public interface IKeyProtectionProvider
{
    byte[] Protect(byte[] data);
    byte[] Unprotect(byte[] protectedData);
}

public sealed class DpapiKeyProtectionProvider : IKeyProtectionProvider
{
    public byte[] Protect(byte[] data) => ProtectedData.Protect(data, null, DataProtectionScope.CurrentUser);
    public byte[] Unprotect(byte[] protectedData) => ProtectedData.Unprotect(protectedData, null, DataProtectionScope.CurrentUser);
}

