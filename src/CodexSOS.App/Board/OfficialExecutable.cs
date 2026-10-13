using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography.X509Certificates;

namespace CodexSOS.App.Board;

internal static class OfficialExecutable
{
    // Only invoke a native binary with a valid Windows signature and an OpenAI
    // publisher. Do not execute same-name scripts or unsigned PATH shims.
    public static string? Find()
    {
        foreach (var directory in (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator).Take(64))
        {
            try
            {
                var localDirectory = directory.Trim('"');
                if (!Path.IsPathFullyQualified(localDirectory) || localDirectory.StartsWith(@"\\", StringComparison.Ordinal)) continue;
                if (new DriveInfo(Path.GetPathRoot(localDirectory)!).DriveType != DriveType.Fixed) continue;
                var file = Path.GetFullPath(Path.Combine(localDirectory, "codex.exe"));
                if (!File.Exists(file) || !Verify(file)) continue;
                // Loader accepts certificate files, not PE Authenticode containers.
                // Trust is checked above; only signer extraction needs this legacy API.
                // https://github.com/dotnet/runtime/issues/109941
#pragma warning disable SYSLIB0057
                using var signedFile = X509Certificate.CreateFromSignedFile(file);
#pragma warning restore SYSLIB0057
                using var certificate = X509CertificateLoader.LoadCertificate(signedFile.GetRawCertData());
                if (certificate.GetNameInfo(X509NameType.SimpleName, false) is "OpenAI OpCo, LLC" or "OpenAI, L.L.C.") return file;
            }
            catch { /* A missing/untrusted CLI is a supported disconnected state. */ }
        }
        return null;
    }

    private static bool Verify(string path)
    {
        var file = new TrustFile { Size = (uint)Marshal.SizeOf<TrustFile>(), Path = path };
        var pointer = Marshal.AllocHGlobal(Marshal.SizeOf<TrustFile>());
        try
        {
            Marshal.StructureToPtr(file, pointer, false);
            var data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, UnionChoice = 1, File = pointer, ProviderFlags = 0x1000 };
            var policy = new Guid("00AAC56B-CD44-11D0-8CC2-00C04FC295EE");
            return WinVerifyTrust(new IntPtr(-1), ref policy, ref data) == 0;
        }
        finally { Marshal.DestroyStructure<TrustFile>(pointer); Marshal.FreeHGlobal(pointer); }
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct TrustFile { public uint Size; [MarshalAs(UnmanagedType.LPWStr)] public string Path; public IntPtr Handle; public IntPtr Subject; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size; public IntPtr PolicyCallback; public IntPtr SipClient;
        public uint UiChoice; public uint Revocation; public uint UnionChoice; public IntPtr File;
        public uint StateAction; public IntPtr StateData; public IntPtr UrlReference;
        public uint ProviderFlags; public uint UiContext; public IntPtr SignatureSettings;
    }
    [DllImport("wintrust.dll", ExactSpelling = true, CharSet = CharSet.Unicode)]
    private static extern int WinVerifyTrust(IntPtr window, ref Guid policy, ref TrustData data);
}
