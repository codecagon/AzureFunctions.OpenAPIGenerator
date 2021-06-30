using System.Reflection;

namespace Codecagon.Tools.AzureFunctions.OpenAPIGenerator
{
    public class SwaggerGenerator: ISwaggerGenerator
    {

        private readonly Assembly _assembly;
        
        public SwaggerGenerator(Assembly assembly)
        {
            _assembly = assembly;
        }
        
        public RenderedDocument Render(string fileName)
        {
            using var stream = _assembly.GetManifestResourceStream($"Swagger.{fileName}");
            return new(fileName, MimeUtils.GetMimeType(fileName), stream);
        }
    }
}