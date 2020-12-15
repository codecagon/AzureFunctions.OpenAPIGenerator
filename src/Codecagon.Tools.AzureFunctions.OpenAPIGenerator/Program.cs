using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;
using System.Xml.XPath;
using Microsoft.AspNetCore.Mvc;
using Microsoft.OpenApi;
using Microsoft.OpenApi.Extensions;
using Microsoft.OpenApi.Models;
using MoreLinq;

namespace Codecagon.Tools.AzureFunctions.OpenAPIGenerator
{
    static class Program
    {
        static void Main(string[] args)
        {
            var baseUrl = "http://localhost:7071/api";

            var assemblyPath = new FileInfo(args[0]).Directory!.FullName;
            var files = Directory.GetFiles(assemblyPath, "Microsoft.Azure*.dll");
            
            foreach (var assemblyFile in files)
            {
                try
                {
                    Assembly.LoadFrom(assemblyFile);
                }
                catch (Exception)
                {
                    // We could still try to generate OpenAPI
                }
            }

            var documentationFiles = Directory
                .GetFiles(assemblyPath, "*.xml")
                .ToList();
            var docs = XDocument.Load(documentationFiles[0]);
            documentationFiles.RemoveAt(0);
            documentationFiles.ForEach(f => docs.Root!.Add(XDocument.Load(f).Root!.Elements()));
            
            var assemblyFiles = Directory
                .GetFiles(assemblyPath, "*Lambda.dll") // TODO: path pattern as parameter
                .ToList();
            
            var firstAssembly = Assembly.LoadFrom(assemblyFiles[0]);
            assemblyFiles.RemoveAt(0);
            var classes = firstAssembly
                .GetTypes()
                .Where(t => t.IsClass)
                .ToList();
            assemblyFiles.ForEach(f => classes.AddRange(Assembly.LoadFrom(f).GetTypes().Where(t => t.IsClass)));
            
            var methodInfos = classes
                .SelectMany(c => c.GetMethods())
                .ToList();
            var functions = methodInfos
                .Where(m => m.GetCustomAttributes().Any(a => a.GetType().Name == "FunctionNameAttribute"))
                .ToList();

            var schemas = PopulateSchemas(CollectSchemas(functions));

            var result = functions
                .Select(function =>
                {
                    var triggerAttribute = function
                        .GetParameters()
                        .SelectMany(p => p.GetCustomAttributes())
                        .Single(a => a.GetType().Name == "HttpTriggerAttribute");

                    var route = triggerAttribute
                        .GetType()
                        .GetProperty("Route")
                        ?.GetValue(triggerAttribute) as string;

                    var parameters = Regex.Matches(route!, @"\{([^\}]+)\}")
                        .Select(m => m.Groups[1].Value)
                        .ToList();

                    var method = (triggerAttribute
                        .GetType()
                        .GetProperty("Methods")
                        ?.GetValue(triggerAttribute) as string[])
                        ?[0];
                    Enum.TryParse(typeof(OperationType), method, true, out var operationType);

                    var functionNameForDocs = "M:" + function.DeclaringType + "." +
                                              function.ToString()!.Remove(0, function.ToString()!.IndexOf(" ", StringComparison.InvariantCulture) + 1)
                                                  .Replace(" ", "");

                    var key = "/" + route;
                    var value = new OpenApiPathItem
                    {
                        Operations = new Dictionary<OperationType, OpenApiOperation>
                        {
                            [(OperationType)operationType!] = new OpenApiOperation
                            {
                                Tags = new List<OpenApiTag>
                                {
                                    new OpenApiTag
                                    {
                                        Name = function.DeclaringType!.Name
                                    }
                                },
                                Summary = function.Name + " - " + docs.XPathSelectElement(@$"/doc/members/member[@name = ""{functionNameForDocs}""]/summary")?.Value.Trim(),
                                Parameters = function
                                    .GetParameters()
                                    .Where(param => parameters.Contains(param.Name))
                                    .Select(param => new OpenApiParameter
                                    {
                                        Name = param.Name,
                                        Description = docs.XPathSelectElement(@$"/doc/members/member[@name = ""{functionNameForDocs}""]/param[@name=""{param.Name}""]")?.Value.Trim(),
                                        In = ParameterLocation.Path,
                                        Required = true,
                                        Schema = new OpenApiSchema
                                        {
                                            Type  = ConvertType(param.ParameterType),
                                        }
                                    })
                                    .ToList(),
                                RequestBody = function
                                    .GetParameters()
                                    .Where(param => param.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "HttpTriggerAttribute") && param.ParameterType.Name != "HttpRequest")
                                    .Select(param => new OpenApiRequestBody
                                    {
                                        Required = true,
                                        Description = docs.XPathSelectElement(@$"/doc/members/member[@name = ""{functionNameForDocs}""]/param[@name=""{param.Name}""]")?.Value.Trim(),
                                        Content =
                                        {
                                            ["application/json"] = new OpenApiMediaType()
                                            {
                                                Schema = new OpenApiSchema()
                                                {
                                                    Type = "object",
                                                    Reference = new OpenApiReference
                                                    {
                                                        Type = ReferenceType.Schema,
                                                        Id = param.ParameterType.Name
                                                    }
                                                }
                                            }
                                        }
                                    }).SingleOrDefault(p => true),
                             
                                Description = ReadInnerXml(docs.XPathSelectElement(@$"/doc/members/member[@name = ""{functionNameForDocs}""]/remarks")),
                                Responses = UnwrapReturnType(function.ReturnType) == null 
                                    ? new OpenApiResponses
                                        {
                                            ["204"] = new OpenApiResponse
                                            {
                                                Description = "No Content"
                                            }
                                        } 
                                    : new OpenApiResponses
                                        {
                                            ["200"] = new OpenApiResponse
                                            {
                                                Description = "OK - " + docs.XPathSelectElement(@$"/doc/members/member[@name = ""{functionNameForDocs}""]/returns")?.Value.Trim(),
                                                Content = {
                                                    ["application/json"] = new OpenApiMediaType
                                                    {
                                                        Schema = new OpenApiSchema
                                                        {
                                                            Type = ConvertType(UnwrapReturnType(function.ReturnType)),
                                                            Reference = ConvertType(UnwrapReturnType(function.ReturnType)) == "object" 
                                                                ? new OpenApiReference
                                                                    {
                                                                        Type = ReferenceType.Schema,
                                                                        Id = ListItem(UnwrapReturnType(function.ReturnType))?.Name
                                                                    }
                                                                : null,
                                                            Items = ConvertType(UnwrapReturnType(function.ReturnType)) == "array"
                                                                ? new OpenApiSchema {
                                                                    Type = "object",
                                                                    Reference = new OpenApiReference
                                                                    {
                                                                        Type = ReferenceType.Schema,
                                                                        Id = ListItem(UnwrapReturnType(function.ReturnType)).Name
                                                                    }
                                                                }
                                                                : null
                                                        }
                                                    }
                                                }
                                            }
                                        }
                            }
                        }
                    };
                    return KeyValuePair.Create(key, value);
                })
                .ToList();

            var openApiPaths = new OpenApiPaths();
            result.ForEach(pair =>
            {
                if (openApiPaths.ContainsKey(pair.Key))
                {
                    foreach (var openApiOperation in pair.Value.Operations)
                    {
                        openApiPaths[pair.Key].Operations.Add(openApiOperation);
                    }
                }
                else
                {
                    openApiPaths.Add(pair.Key, pair.Value);
                }
            });


            var document = new OpenApiDocument
            {
                Info = new OpenApiInfo
                {
                    Version = firstAssembly.GetName().Version?.ToString(),
                    Title = "Competency API",
                },
                Servers = new List<OpenApiServer>
                {
                    new OpenApiServer {Url = baseUrl}
                },
                Tags = functions
                    .Select(f => f.DeclaringType?.Name)
                    .Distinct()
                    .Select(name => new OpenApiTag { Name = name })
                    .ToList(),
                Paths = openApiPaths,
                Components = new OpenApiComponents
                {
                    Schemas = schemas
                        .ToDictionary(s => s.Name, s => new OpenApiSchema
                        {
                            Type = "object",
                            Description = docs.XPathSelectElement(@$"/doc/members/member[@name = ""T:{s}""]")?.Value.Trim(),
                            Properties = s
                                .GetProperties()
                                .ToDictionary(
                                    p => p.Name, 
                                    p => new OpenApiSchema
                                    {
                                        Type = ConvertType(p.PropertyType),
                                        Description = docs.XPathSelectElement(@$"/doc/members/member[@name = ""P:{s}.{p.Name}""]")?.Value.Trim(),
                                        Reference = ConvertType(p.PropertyType) == "object" 
                                            ? new OpenApiReference
                                            {
                                                Type = ReferenceType.Schema,
                                                Id = ListItem(p.PropertyType)?.Name
                                            }
                                            : null,
                                        Items = ConvertType(p.PropertyType) == "array"
                                            ? new OpenApiSchema {
                                                Type = "object",
                                                Reference = new OpenApiReference
                                                    {
                                                        Type = ReferenceType.Schema,
                                                        Id = ListItem(p.PropertyType).Name
                                                    }
                                            }
                                            : null
                                    })
                        })
                }
            };
            
            
         
            var outputString = document.Serialize(OpenApiSpecVersion.OpenApi3_0, OpenApiFormat.Json);
            using var file = File.CreateText(assemblyPath + "/swagger.json");
            file.Write(outputString);
            file.Close();
        }

        private static string ReadInnerXml(XElement element)
        {
            if (element == null)
            {
                return null;
            }
            var reader =  element.CreateReader();
            reader.MoveToContent();
            return reader.ReadInnerXml().Replace("<para>", "<p>").Replace("</para>", "</p>").Trim();
        }

        private static List<Type> PopulateSchemas(List<Type> types)
        {
            var current = types;
            var next = new List<Type>();
            while (next.Count != current.Count)
            {
                var newTypes = current
                    .SelectMany(t => t.GetProperties())
                    .Select(p => ListItem(p.PropertyType))
                    .Where(t => ConvertType(t) == "object");

                current = next;

                next = current.Concat(newTypes).DistinctBy(t => t.FullName).ToList();
            }

            return next;
        }

        private static List<Type> CollectSchemas(List<MethodInfo> functions)
        {
            return functions
                .SelectMany(f => f
                    .GetParameters()
                    .Where(param => param.GetCustomAttributes().Any(attribute => attribute.GetType().Name == "HttpTriggerAttribute") && param.ParameterType.Name != "HttpRequest")
                    .Select(p => p.ParameterType)
                    .Concat(Enumerate(ListItem(UnwrapReturnType(f.ReturnType)))))
                .DistinctBy(p => p.FullName)
                .ToList();
        }

        private static Type UnwrapReturnType(Type returnType)
        {
            if (returnType == typeof(void))
            {
                return null;
            }

            var realType = returnType;

            if (realType.Name.Equals(typeof(Task<>).Name))
            {
                realType = realType.GenericTypeArguments[0];
            }

            if (realType.Name.Equals(typeof(ActionResult<>).Name))
            {
                realType = realType.GenericTypeArguments[0];
            }

            if (returnType == typeof(ActionResult))
            {
                return null;
            }
            
            return realType;
        }

        private static Type ListItem(Type type)
        {
            var realType = type;
            
            while (realType != null && realType.Name.Equals(typeof(List<>).Name))
            {
                realType = realType.GenericTypeArguments[0];
            }

            return realType;
        }

        private static IEnumerable<Type> Enumerate(Type type)
        {
            return type == null
                ? Enumerable.Empty<Type>()
                : Enumerable.Repeat(type, 1);
        }

        private static string ConvertType(Type type)
        {
            if (type.Name.Equals(typeof(List<>).Name))
            {
                return "array";
            }
            
            var lowerCased = type.Name.ToLower();
            return new List<string> {"array", "boolean", "integer", "number", "object", "string"}.Contains(lowerCased)
                ? lowerCased
                : "object";
        }
    }
}