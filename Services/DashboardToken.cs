using System;
using System.Security.Cryptography;
using System.Text;

namespace SaoKeBot.Services
{
    // Generates & validates per-uid dashboard access tokens (HMAC-SHA256).
    // A user can only view their own data; without a valid token access is denied.
    public static class DashboardToken
    {
        public static string Generate(long uid, string secret)
        {
            if (string.IsNullOrEmpty(secret)) return "";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(uid.ToString()));
            // URL-safe base64 so it can be placed in a query string.
            return Convert.ToBase64String(hash)
                .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        }

        public static bool Validate(long uid, string? token, string secret)
        {
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(token)) return false;
            var expected = Generate(uid, secret);
            // Constant-time comparison to prevent timing attacks.
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(token));
        }
    }
}
