using System.IO;
using System.Reflection;

namespace Codecagon.Tools.AzureFunctions.OpenAPIGenerator
{
    public class OpenApiGenerator: IOpenApiGenerator
    {
        
        private readonly Assembly _assembly;
        
        public OpenApiGenerator(Assembly assembly)
        {
            _assembly = assembly;
        }
        public string Generate()
        {
            using var stream = _assembly.GetManifestResourceStream($"Swagger.swagger.json");

            using var reader = new StreamReader(stream!);
            return reader.ReadToEnd();
        }
    }
}