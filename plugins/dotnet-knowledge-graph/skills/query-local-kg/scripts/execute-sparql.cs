// execute-sparql.cs
// .NET 10 file-based app — no .csproj required.
// Loads Turtle files from a directory, executes a SPARQL query, outputs JSON.
//
// Usage: dotnet run execute-sparql.cs -- <graph-dir> <sparql-query>
// Example: dotnet run execute-sparql.cs -- ./graph/articles "PREFIX schema: <https://schema.org/> SELECT ?name WHERE { ?e schema:name ?name }"

#:package dotNetRdf@3.5.1
#:property PublishAot=false

using System.Text.Json;
using VDS.RDF;
using VDS.RDF.Parsing;
using VDS.RDF.Query;
using VDS.RDF.Query.Datasets;
using VDS.RDF.Writing;

if (args.Length < 2)
{
    Console.Error.WriteLine("Usage: dotnet run execute-sparql.cs -- <graph-dir> <sparql-query>");
    Console.Error.WriteLine("  graph-dir:    Directory containing .ttl files");
    Console.Error.WriteLine("  sparql-query:  SPARQL SELECT or ASK query string");
    return 1;
}

var graphDir = args[0];
var sparql = args[1];

// Block mutating queries
var upper = sparql.Trim().ToUpperInvariant();
string[] blocked = ["INSERT", "DELETE", "LOAD", "CLEAR", "DROP", "CREATE"];
foreach (var kw in blocked)
{
    if (upper.Contains(kw))
    {
        Console.Error.WriteLine($"Error: Mutating keyword '{kw}' is not allowed. Only SELECT and ASK queries are supported.");
        return 1;
    }
}

// Load all .ttl files into an in-memory triple store
var store = new TripleStore();
var ttlFiles = Directory.GetFiles(graphDir, "*.ttl", SearchOption.AllDirectories);

if (ttlFiles.Length == 0)
{
    Console.Error.WriteLine($"Error: No .ttl files found in '{graphDir}'");
    return 1;
}

foreach (var file in ttlFiles)
{
    try
    {
        var g = new Graph();
        g.LoadFromFile(file);
        store.Add(g);
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Warning: Failed to parse {Path.GetFileName(file)}: {ex.Message}");
    }
}

Console.Error.WriteLine($"Loaded {store.Triples.Count()} triples from {ttlFiles.Length} file(s)");

// Execute SPARQL
try
{
    var parser = new SparqlQueryParser();
    var query = parser.ParseFromString(sparql);
    var dataset = new InMemoryDataset(store);
    var processor = new LeviathanQueryProcessor(dataset);
    var results = processor.ProcessQuery(query);

    if (results is SparqlResultSet resultSet)
    {
        // Serialize as SPARQL Results JSON
        var output = new
        {
            head = new { vars = resultSet.Variables.ToArray() },
            results = new
            {
                bindings = resultSet.Select(r =>
                {
                    var binding = new Dictionary<string, object>();
                    foreach (var v in resultSet.Variables)
                    {
                        var node = r[v];
                        if (node != null)
                        {
                            binding[v] = node switch
                            {
                                IUriNode uri => new { type = "uri", value = uri.Uri.ToString() },
                                ILiteralNode lit => (object)new { type = "literal", value = lit.Value, datatype = lit.DataType?.ToString() },
                                IBlankNode blank => new { type = "bnode", value = blank.InternalID },
                                _ => new { type = "unknown", value = node.ToString() ?? "" }
                            };
                        }
                    }
                    return binding;
                }).ToArray()
            }
        };

        Console.WriteLine(JsonSerializer.Serialize(output, new JsonSerializerOptions { WriteIndented = true }));
    }
    else if (results is IGraph resultGraph)
    {
        Console.Error.WriteLine($"CONSTRUCT/DESCRIBE returned {resultGraph.Triples.Count} triples");
        var writer = new CompressingTurtleWriter();
        using var sw = new System.IO.StringWriter();
        writer.Save(resultGraph, sw);
        Console.WriteLine(sw.ToString());
    }
    else
    {
        Console.Error.WriteLine("Unexpected result type");
        return 1;
    }
}
catch (Exception ex)
{
    Console.Error.WriteLine($"SPARQL error: {ex.Message}");
    return 1;
}

return 0;
