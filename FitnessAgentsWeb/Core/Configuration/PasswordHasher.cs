using System;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace FitnessAgentsWeb.Core.Configuration
{
    public static class PasswordHasher
    {
        // Simple PBKDF2 hashing for this demo
        public static string HashPassword(string password)
        {
            byte[] salt = new byte[128 / 8];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(salt);
            }

            string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: password,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 256 / 8));

            return $"{Convert.ToBase64String(salt)}:{hashed}";
        }

        public static bool VerifyPassword(string inputPassword, string storedHashBase64)
        {
            var parts = storedHashBase64.Split(':');
            if (parts.Length != 2) return false;

            byte[] salt = Convert.FromBase64String(parts[0]);
            string expectedHash = parts[1];

            string actualHash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: inputPassword,
                salt: salt,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 100000,
                numBytesRequested: 256 / 8));

            return actualHash == expectedHash;
        }
    }
}
