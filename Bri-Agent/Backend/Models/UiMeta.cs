using System.Collections.Generic;

namespace BriAgent.Backend.Models
{
    public record UiProfile(
        string mode,
        bool stream = false,
        bool history = false,
        bool structured = false,
        IEnumerable<string>? tools = null,
        string? recommendedView = null,
        IEnumerable<string>? capabilities = null
    );

    public record UiMeta(
        string version,
        string demoId,
        string controller,
        UiProfile ui
    );
}
