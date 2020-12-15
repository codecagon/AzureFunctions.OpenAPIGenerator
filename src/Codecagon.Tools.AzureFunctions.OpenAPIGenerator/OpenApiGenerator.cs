using System;
using System.IO;
using System.Linq;

namespace Codecagon.Tools.AzureFunctions.OpenAPIGenerator
{
    public class OpenApiGenerator: IOpenApiGenerator
    {
        public string Generate()
        {
            var assembly = AppDomain
                .CurrentDomain
                .GetAssemblies()
                .Single(a =>
                {
                    try
                    {
                        return a.GetManifestResourceNames().Any(name => name.EndsWith("swagger.json"));
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                });
                
            var swaggerJson = assembly
                .GetManifestResourceNames()
                .Single(name => name.EndsWith("swagger.json"));

            using var stream = assembly.GetManifestResourceStream(swaggerJson);
            using var reader = new StreamReader(stream!);
            return reader.ReadToEnd();
        }
    }
}