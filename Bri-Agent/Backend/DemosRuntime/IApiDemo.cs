using Microsoft.AspNetCore.Routing;
using System.Collections.Generic;

namespace BriAgent.Backend.DemosRuntime
{
    public interface IApiDemo
    {
        string Id { get; }
        string Title { get; }
        string Description { get; }
        IEnumerable<string> Tags { get; }
        IEnumerable<string> SourceFiles { get; }
        // Futuro: permitir metadata extra (diagramas, pasos, etc.)
        void MapEndpoints(IEndpointRouteBuilder app);
    }
}
