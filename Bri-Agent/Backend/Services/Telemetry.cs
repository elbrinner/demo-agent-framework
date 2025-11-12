using System.Diagnostics;

namespace BriAgent.Backend;

public static class Telemetry
{
    public const string ActivitySourceName = "BriAgent";
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);
}
