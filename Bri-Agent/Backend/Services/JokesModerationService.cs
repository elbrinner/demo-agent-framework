using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;

namespace BriAgent.Backend.Services
{
    public record ModerationResult(bool Allowed, string? Category = null, string? Reason = null);

    /// <summary>
    /// Servicio de moderación con dos modos: heurístico local (por defecto) o mediante un agente LLM (si USE_AGENT_MODERATION=true).
    /// </summary>
    public class JokesModerationService
    {
        private readonly IAgentFactory? _factory;
        private readonly AgentRunner _runner;
        private readonly bool _useAgent;
        private readonly string? _model;

        private static readonly string[] DefaultBlocklist = new[]
        {
            // profanidad común (inglés/español) - lista ilustrativa y acotada
            "fuck", "shit", "bitch", "asshole", "bastard", "cunt",
            "mierda", "puta", "puto", "gilipollas", "imbécil",
            // categorías sensibles
            "violencia extrema", "incitación al odio", "discurso de odio"
        };

        public JokesModerationService(AgentRunner runner, IAgentFactory? factory = null)
        {
            _runner = runner;
            _factory = factory;
            _useAgent = string.Equals(Environment.GetEnvironmentVariable("USE_AGENT_MODERATION"), "true", StringComparison.OrdinalIgnoreCase);
            // Evitar acceder a credenciales si no se usará agente de moderación
            _model = _useAgent ? BriAgent.Backend.Config.Credentials.Model : null;
        }

        public async Task<ModerationResult> EvaluateAsync(string text, CancellationToken ct = default)
        {
            text ??= string.Empty;
            if (!_useAgent || _factory is null)
            {
                return Heuristic(text);
            }
            try
            {
                var agent = _factory.CreateBasicAgent(
                    instructions: "Actúas como moderador de contenido. Devuelve SOLO 'allow' o 'block:<categoria>' según políticas generales. Considera lenguaje ofensivo, odio, violencia, sexual explícito, datos sensibles.",
                    tools: Array.Empty<AIFunction>()
                );
                var resp = await _runner.RunAsync(agent, $"Evalúa el siguiente texto para moderación y responde 'allow' o 'block:<categoria>':\n\n" + text, model: _model, agentType: "moderation.agent", cancellationToken: ct);
                var r = (resp.Text ?? string.Empty).Trim().ToLowerInvariant();
                if (r.StartsWith("block:"))
                {
                    var category = r.Substring("block:".Length).Trim();
                    return new ModerationResult(false, string.IsNullOrWhiteSpace(category) ? "blocked" : category, "blocked_by_agent");
                }
                return new ModerationResult(true);
            }
            catch
            {
                // fallback duro si el agente falla
                return Heuristic(text);
            }
        }

        private static ModerationResult Heuristic(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return new ModerationResult(true);
            var blEnv = Environment.GetEnvironmentVariable("JOKES_MODERATION_BLOCKLIST");
            var blocklist = (blEnv?.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries) ?? DefaultBlocklist)
                            .Select(s => s.ToLowerInvariant()).ToArray();
            var low = text.ToLowerInvariant();
            foreach (var w in blocklist)
            {
                if (string.IsNullOrWhiteSpace(w)) continue;
                if (low.Contains(w)) return new ModerationResult(false, "profanity", $"blocked_by_heuristic:{w}");
            }
            // Ejemplo de regla simple adicional: longitud excesiva (no típico en chistes)
            if (text.Length > 2000) return new ModerationResult(false, "length", "too_long");
            return new ModerationResult(true);
        }
    }
}
