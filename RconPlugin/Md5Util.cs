using System.Security.Cryptography;
using System.Text;

namespace RconPlugin
{
    public static class Md5Util
    {
        public static byte[] HashString(string str)
        {
            return MD5.Create().ComputeHash(Encoding.Unicode.GetBytes(str));
        }
    }
}