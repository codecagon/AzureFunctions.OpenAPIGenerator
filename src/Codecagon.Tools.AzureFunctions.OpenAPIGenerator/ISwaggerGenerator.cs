namespace Codecagon.Tools.AzureFunctions.OpenAPIGenerator
{
    public interface ISwaggerGenerator
    {

        RenderedDocument Render(string fileName);

    }
}