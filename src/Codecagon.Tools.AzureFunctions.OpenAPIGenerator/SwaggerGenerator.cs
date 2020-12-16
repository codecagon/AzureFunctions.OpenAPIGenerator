using System.IO;
using System.Reflection;
using Microsoft.AspNetCore.Mvc;

namespace Codecagon.Tools.AzureFunctions.OpenAPIGenerator
{
    public class SwaggerGenerator: ISwaggerGenerator
    {

        private readonly Assembly _assembly;
        
        public SwaggerGenerator(Assembly assembly)
        {
            _assembly = assembly;
        }
        
        public IActionResult Render(string fileName)
        {
            using var stream = _assembly.GetManifestResourceStream($"Swagger.{fileName}");
            using var reader = new StreamReader(stream!);
            
            return new ContentResult {
                Content = reader.ReadToEnd(),
                ContentType = MimeUtils.GetMimeType(fileName)
            };
        }
    }
}