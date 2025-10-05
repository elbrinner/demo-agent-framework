using System;
using System.IO;

namespace demo_agent_framework.Config
{
    public static class Credentials
    {
        // Keys expected in environment or .env file
        private const string EndpointKey = "AZURE_OPENAI_ENDPOINT";
        private const string ApiKeyKey = "AZURE_OPENAI_KEY";
        private const string ModelKey = "AZURE_OPENAI_MODEL";

        public static string Endpoint => GetRequired(EndpointKey);
        public static string ApiKey => GetRequired(ApiKeyKey);
        public static string Model => GetRequired(ModelKey);

        private static string GetRequired(string key)
        {
            var v = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(v))
                return v!;

            // If variable not found, try to load from a .env file located in this repo or parent directories
            TryLoadEnvFile();

            v = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(v))
                return v!;

            throw new InvalidOperationException($"Missing required environment variable: {key}");
        }

        private static void TryLoadEnvFile()
        {
            try
            {
                var dir = Directory.GetCurrentDirectory();
                for (int i = 0; i < 10 && dir != null; i++)
                {
                    var candidate = Path.Combine(dir, ".env");
                    if (File.Exists(candidate))
                    {
                        foreach (var line in File.ReadAllLines(candidate))
                        {
                            var trimmed = line.Trim();
                            if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith("#"))
                                continue;

                            var idx = trimmed.IndexOf('=');
                            if (idx <= 0)
                                continue;

                            var k = trimmed.Substring(0, idx).Trim();
                            var val = trimmed.Substring(idx + 1).Trim();

                            // Remove surrounding quotes if any
                            if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
                            {
                                val = val.Substring(1, val.Length - 2);
                            }

                            // Only set if not already present
                            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(k)))
                            {
                                Environment.SetEnvironmentVariable(k, val);
                            }
                        }

                        return;
                    }

                    var parent = Directory.GetParent(dir);
                    dir = parent?.FullName;
                }
            }
            catch
            {
                // ignore errors when attempting to load .env
            }
        }
    }
}
