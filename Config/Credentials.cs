using System;
using System.IO;

namespace demo_agent_framework.Config
{
    public static class Credentials
    {
        // Claves esperadas en las variables de entorno o en el archivo .env
        private const string EndpointKey = "AZURE_OPENAI_ENDPOINT";
        private const string ApiKeyKey = "AZURE_OPENAI_KEY";
        private const string ModelKey = "AZURE_OPENAI_MODEL";

        // Nuevas claves para autenticación mediante Service Principal (opcional)
        private const string ClientIdKey = "AZURE_CLIENT_ID";
        private const string TenantIdKey = "AZURE_TENANT_ID";
        private const string ClientSecretKey = "AZURE_CLIENT_SECRET";

        // Propiedades públicas que exponen las credenciales necesarias
        public static string Endpoint => GetRequired(EndpointKey);
        public static string ApiKey => GetRequired(ApiKeyKey);
        public static string Model => GetRequired(ModelKey);

        // Propiedades opcionales para autenticación con Service Principal
        // Devuelven null si no están definidas
        public static string? ClientId => GetOptional(ClientIdKey);
        public static string? TenantId => GetOptional(TenantIdKey);
        public static string? ClientSecret => GetOptional(ClientSecretKey);

        // Indica si las tres variables de Service Principal están definidas
        public static bool HasClientCredentials =>
            !string.IsNullOrEmpty(ClientId) && !string.IsNullOrEmpty(TenantId) && !string.IsNullOrEmpty(ClientSecret);

        // Intenta leer una variable de entorno; si no existe, intenta cargar .env y volver a leerla.
        private static string GetRequired(string key)
        {
            var v = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(v))
                return v;

            // Si no está en variables de entorno, intentar cargar .env manualmente
            TryLoadEnvFile();

            v = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(v))
                return v;

            // Si sigue sin existir, lanzar excepción con mensaje en castellano
            throw new InvalidOperationException($"Falta la variable de entorno requerida: {key}");
        }

        // Leer variable opcional (devuelve null si no existe)
        private static string? GetOptional(string key)
        {
            var v = Environment.GetEnvironmentVariable(key);
            if (!string.IsNullOrEmpty(v))
                return v;

            TryLoadEnvFile();

            v = Environment.GetEnvironmentVariable(key);
            return string.IsNullOrEmpty(v) ? null : v;
        }

        // Busca un archivo .env en el directorio actual o en padres hasta 10 niveles y carga sus pares clave=valor
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

                            // Eliminar comillas envolventes si existen
                            if ((val.StartsWith("\"") && val.EndsWith("\"")) || (val.StartsWith("'") && val.EndsWith("'")))
                            {
                                val = val.Substring(1, val.Length - 2);
                            }

                            // Sólo establecer si no existe ya la variable de entorno
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
                // Ignorar errores al intentar cargar .env para no romper la ejecución de demos
            }
        }
    }
}
