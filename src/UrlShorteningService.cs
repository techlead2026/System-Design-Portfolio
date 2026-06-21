namespace UrlShortenerApi
{
    public class UrlShorteningService
    {
        private const string Base62Chars = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";

        public string Generate7CharacterCode(string longUrl)
        {
            // 1. Generate a deterministic 32-bit integer hash from the URL string
            // Using MurmurHash or a simple deterministic hash algorithm
            uint hashCode = ComputeDeterministicHash(longUrl);

            // 2. Convert that unique integer into a short Base62 string string
            return EncodeBase62(hashCode);
        }

        private uint ComputeDeterministicHash(string input)
        {
            // Computes a fast, stable hash code unaffected by machine architecture
            unchecked
            {
                uint hash = 2166136261;
                foreach (char c in input)
                {
                    hash = (hash ^ c) * 16777619;
                }
                return hash;
            }
        }

        private string EncodeBase62(uint value)
        {
            if (value == 0) return "0";

            var result = new System.Text.StringBuilder();
            while (value > 0)
            {
                result.Insert(0, Base62Chars[(int)(value % 62)]);
                value /= 62;
            }

            // Pad or trim to maintain a clean, uniform length profile
            string code = result.ToString();
            return code.Length > 7 ? code.Substring(0, 7) : code.PadLeft(7, '0');
        }
    }

}
