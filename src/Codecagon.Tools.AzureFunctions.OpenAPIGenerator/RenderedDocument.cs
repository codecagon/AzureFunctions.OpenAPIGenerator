using System.IO;

namespace Codecagon.Tools.AzureFunctions.OpenAPIGenerator
{
    public class RenderedDocument
    {
        public string FileName { get; }
        
        public string MimeType { get; }

        public Stream Content { get; }

        public RenderedDocument(string fileName, string mimeType, Stream content)
        {
            FileName = fileName;
            MimeType = mimeType;
            Content = content;
        }

    }
}