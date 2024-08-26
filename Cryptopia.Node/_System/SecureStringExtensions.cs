using System.Runtime.InteropServices;
using System.Security;

public static class SecureStringExtensions
{
    /// <summary>
    /// https://stackoverflow.com/questions/11458894/reading-single-chars-from-a-net-securestring-in-c/11459012#11459012
    /// </summary>
    /// <param name="value"></param>
    /// <param name="i"></param>
    /// <returns></returns>
    public static char GetCharAt(this SecureString value, int i)
    {
        var bstr = Marshal.SecureStringToBSTR(value);
        try
        {
            // Index in 2-byte (char) chunks
            return (char)Marshal.ReadByte(bstr, i * 2);
        }
        finally
        {
            Marshal.FreeBSTR(bstr);
        }
    }

    // convert a secure string into a normal plain text string
    public static string ToPlainString(this SecureString secureStr)
    {
        return new System.Net.NetworkCredential(string.Empty, secureStr).Password;
    }

    // convert a plain text string into a secure string
    public static SecureString ToSecureString(this string plainStr)
    {
        var secStr = new SecureString();
        foreach (char c in plainStr.ToCharArray())
        {
            secStr.AppendChar(c);
        }
        return secStr;
    }
}