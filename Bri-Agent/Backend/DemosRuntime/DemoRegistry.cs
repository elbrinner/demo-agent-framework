using Microsoft.AspNetCore.Routing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace BriAgent.Backend.DemosRuntime
{
    public static class DemoRegistry
    {
        private static readonly Lazy<IReadOnlyList<IApiDemo>> _demos = new(() => Discover());
        public static IReadOnlyList<IApiDemo> Demos => _demos.Value;

        private static IReadOnlyList<IApiDemo> Discover()
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var demos = asm
                    .GetTypes()
                    .Where(t => typeof(IApiDemo).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                    .Select(t => Activator.CreateInstance(t) as IApiDemo)
                    .Where(x => x is not null)
                    .Cast<IApiDemo>()
                    .OrderBy(d => d.Title)
                    .ToList();
                return demos;
            }
            catch
            {
                return Array.Empty<IApiDemo>();
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
}
