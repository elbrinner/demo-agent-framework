using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace demo_agent_framework.DemosRuntime
{
    // Registro mínimo para el proyecto raíz (independiente del backend Bri-Agent)
    public static class DemoRegistry
    {
        private static readonly Lazy<IReadOnlyList<IDemo>> _demos = new(() => Discover());
        public static IReadOnlyList<IDemo> Demos => _demos.Value;

        private static IReadOnlyList<IDemo> Discover()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var demos = asm
                    .GetTypes()
                    .Where(t => typeof(IDemo).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .Select(t => Activator.CreateInstance(t) as IDemo)
                    .Where(x => x is not null)
                    .Cast<IDemo>()
                    .OrderBy(d => d.Id)
                    .ToList();
                return demos;
            }
            catch
            {
                return Array.Empty<IDemo>();
            }
        }

        public static void MapAllDemoEndpoints(IEndpointRouteBuilder app)
        {
            foreach (var demo in Demos)
            {
                demo.MapEndpoints(app);
            }
        }
    }

    // Interfaz mínima para demos en el proyecto raíz (diferente a IApiDemo del backend)
    public interface IDemo
    {
        string Id { get; }
        string Title { get; }
        string Description { get; }
        IEnumerable<string> Tags { get; }
        IEnumerable<string> SourceFiles { get; }
        void MapEndpoints(IEndpointRouteBuilder app);
    }
}
