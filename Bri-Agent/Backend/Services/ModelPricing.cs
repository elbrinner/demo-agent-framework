using System;
using System.Globalization;

namespace BriAgent.Backend.Services
{
    public sealed record ModelPrice(double PromptPer1K, double CompletionPer1K);

    /// <summary>
    /// Utilidad para obtener precios por modelo desde variables de entorno.
    /// Permite nombres específicos por modelo y genéricos:
    ///   - PRICE_{MODEL}_PROMPT_PER_1K / PRICE_{MODEL}_COMPLETION_PER_1K
    ///   - AZURE_OPENAI_PRICE_PROMPT_PER_1K / AZURE_OPENAI_PRICE_COMPLETION_PER_1K (fallback)
    /// Si no hay datos, devuelve null (sin coste).
    /// </summary>
    public static class ModelPricing
    {
        public static ModelPrice? GetPriceForModel(string model)
        {
            if (string.IsNullOrWhiteSpace(model)) return GetGeneric();
            var key = Sanitize(model);
            var pSpec = ReadDouble($"PRICE_{key}_PROMPT_PER_1K");
            var cSpec = ReadDouble($"PRICE_{key}_COMPLETION_PER_1K");
            if (pSpec.HasValue && cSpec.HasValue)
            {
                return new ModelPrice(pSpec.Value, cSpec.Value);
            }
            // Fallback genérico
            return GetGeneric();
        }

        private static ModelPrice? GetGeneric()
        {
            var p = ReadDouble("AZURE_OPENAI_PRICE_PROMPT_PER_1K");
            var c = ReadDouble("AZURE_OPENAI_PRICE_COMPLETION_PER_1K");
            if (p.HasValue && c.HasValue) return new ModelPrice(p.Value, c.Value);
            return null;
        }

        private static string Sanitize(string value)
        {
            // Mayúsculas y reemplazar no alfanum por guion bajo
            var up = value.ToUpperInvariant();
            char[] buf = new char[up.Length];
            for (int i = 0; i < up.Length; i++)
            {
                var ch = up[i];
                buf[i] = char.IsLetterOrDigit(ch) ? ch : '_';
            }
            return new string(buf);
        }

        private static double? ReadDouble(string envName)
        {
            try
            {
                var v = Environment.GetEnvironmentVariable(envName);
                if (string.IsNullOrWhiteSpace(v)) return null;
                if (double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d)) return d;
            }
            catch { }
            return null;
        }
    }
}
