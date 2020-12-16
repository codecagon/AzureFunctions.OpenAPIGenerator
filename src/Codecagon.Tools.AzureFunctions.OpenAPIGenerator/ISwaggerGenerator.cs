using Microsoft.AspNetCore.Mvc;

namespace Codecagon.Tools.AzureFunctions.OpenAPIGenerator
{
    public interface ISwaggerGenerator
    {

        IActionResult Render(string fileName);

    }
}