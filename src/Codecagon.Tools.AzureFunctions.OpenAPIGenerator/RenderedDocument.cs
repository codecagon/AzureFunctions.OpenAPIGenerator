using System.IO;

namespace Codecagon.Tools.AzureFunctions.OpenAPIGenerator
{
    public class RenderedDocument
    {
        public string FileName { get; }
        
        public string MimeType { get; }

        public byte[] Content { get; }

        public RenderedDocument(string fileName, string mimeType, byte[] content)
        {
            FileName = fileName;
            MimeType = mimeType;
            Content = content;
        }

    }
}