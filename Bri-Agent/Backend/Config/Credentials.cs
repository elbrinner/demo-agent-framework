using System;
using System.IO;

namespace BriAgent.Backend.Config
{
    public static class Credentials
    {
        private const string EndpointKey = "AZURE_OPENAI_ENDPOINT";
        private const string ApiKeyKey = "AZURE_OPENAI_KEY";
        private const string ModelKey = "AZURE_OPENAI_MODEL";

        private const string ClientIdKey = "AZURE_CLIENT_ID";
        private const string TenantIdKey = "AZURE_TENANT_ID";
        private const string ClientSecretKey = "AZURE_CLIENT_SECRET";

        public static string Endpoint => GetRequired(EndpointKey);
        public static string ApiKey => GetRequired(ApiKeyKey);
        public static string Model => GetRequired(ModelKey);

        public static string? ClientId => GetOptional(ClientIdKey);
        public static string? TenantId => GetOptional(TenantIdKey);
        public static string? ClientSecret => GetOptional(ClientSecretKey);

        public static bool HasClientCredentials =>
            !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(TenantId) && !string.IsNullOrEmpty(ClientSecret);

        private static string GetRequired(string key)
        {
            var v = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(v))
                return v;

            TryLoadEnvFile();

            v = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(v))
                return v;

            throw new InvalidOperationException($"Falta la variable de entorno requerida: {key}");
        }

        private static string? GetOptional(string key)
        {
            var v = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(v))
                return v;

            TryLoadEnvFile();

            v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(v) ? null : v;
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

                            if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
                            {
                                val = val.Substring(1, val.Length - 2);
                            }

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
                // noop
            }
        }
    }
}
