using System.CommandLine;
using System.Text.Json;
using StringGen.Core;
using StringGen.Core.Swagger;

namespace StringGen.Cli;

internal static class CommandFactory
{
    internal static RootCommand Build()
    {
        var rootCommand = BuildRootCommand();
        rootCommand.Add(BuildJsonCommand());
        rootCommand.Add(BuildSwaggerCommand());
        return rootCommand;
    }

    private static RootCommand BuildRootCommand()
    {
        var patternArg = new Argument<string>("pattern")
        {
            Description = "Regular expression pattern governing string generation"
        };

        var countOption = new Option<int>("--count")
        {
            Description = "Number of strings to generate",
            DefaultValueFactory = _ => 10
        };

        var modeOption = new Option<GenerationMode>("--mode")
        {
            Description = "compliant or noncompliant",
            DefaultValueFactory = _ => GenerationMode.Compliant
        };

        var seedOption = new Option<int?>("--seed")
        {
            Description = "Optional random seed for reproducible output"
        };

        var maxRepeatOption = new Option<int>("--max-repeat")
        {
            Description = "Maximum repetitions for unbounded quantifiers (*, +, {n,})",
            DefaultValueFactory = _ => 5
        };

        var rootCommand = new RootCommand("StringGen — generate random strings governed by a regex pattern")
        {
            patternArg,
            countOption,
            modeOption,
            seedOption,
            maxRepeatOption
        };

        rootCommand.SetAction((ParseResult parseResult) =>
        {
            var pattern = parseResult.GetValue(patternArg)!;
            var count = parseResult.GetValue(countOption);
            var mode = parseResult.GetValue(modeOption);
            var seed = parseResult.GetValue(seedOption);
            var maxRepeat = parseResult.GetValue(maxRepeatOption);

            if (count is < 0 or > 100_000)
            {
                Console.Error.WriteLine("Error: --count must be between 0 and 100,000");
                return 1;
            }
            if (maxRepeat is < 1 or > 1_000)
            {
                Console.Error.WriteLine("Error: --max-repeat must be between 1 and 1,000");
                return 1;
            }

            try
            {
                var generator = new StringGenerator(pattern, seed, maxRepeat);
                var results = generator.Generate(count, mode);
                foreach (var s in results)
                {
                    Console.WriteLine(s);
                }
                return 0;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Invalid pattern: {ex.Message}");
                return 1;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 2;
            }
        });

        return rootCommand;
    }

    private static Command BuildJsonCommand()
    {
        var schemaFileArg = new Argument<string?>("schema-file")
        {
            Description = "Path to a JSON Schema file",
            Arity = ArgumentArity.ZeroOrOne
        };

        var inlineOption = new Option<string?>("--inline")
        {
            Description = "JSON Schema provided as an inline string (alternative to <schema-file>)"
        };

        var countOption = new Option<int>("--count")
        {
            Description = "Number of JSON documents to generate (default 1)",
            DefaultValueFactory = _ => 1
        };

        var seedOption = new Option<int?>("--seed")
        {
            Description = "Optional random seed for reproducible output"
        };

        var maxRepeatOption = new Option<int>("--max-repeat")
        {
            Description = "Maximum repetitions for unbounded quantifiers in string patterns",
            DefaultValueFactory = _ => 5
        };

        var compactOption = new Option<bool>("--compact")
        {
            Description = "Emit minified JSON instead of pretty-printed"
        };

        var jsonModeOption = new Option<GenerationMode>("--mode")
        {
            Description = "Generation mode: Compliant (default) or NonCompliant",
            DefaultValueFactory = _ => GenerationMode.Compliant
        };

        var jsonCommand = new Command("json", "Generate random JSON documents from a JSON Schema")
        {
            schemaFileArg,
            inlineOption,
            countOption,
            seedOption,
            maxRepeatOption,
            compactOption,
            jsonModeOption
        };

        jsonCommand.SetAction((ParseResult parseResult) =>
        {
            var schemaFile = parseResult.GetValue(schemaFileArg);
            var inline = parseResult.GetValue(inlineOption);
            var count = parseResult.GetValue(countOption);
            var seed = parseResult.GetValue(seedOption);
            var maxRepeat = parseResult.GetValue(maxRepeatOption);
            var compact = parseResult.GetValue(compactOption);
            var jsonMode = parseResult.GetValue(jsonModeOption);

            if (count is < 0 or > 100_000)
            {
                Console.Error.WriteLine("Error: --count must be between 0 and 100,000");
                return 1;
            }
            if (maxRepeat is < 1 or > 1_000)
            {
                Console.Error.WriteLine("Error: --max-repeat must be between 1 and 1,000");
                return 1;
            }

            try
            {
                string schemaJson;

                if (inline is not null && schemaFile is not null)
                {
                    Console.Error.WriteLine("Error: --inline and <schema-file> are mutually exclusive");
                    return 1;
                }
                else if (inline is not null)
                {
                    if (inline.Length > 10 * 1024 * 1024)
                    {
                        Console.Error.WriteLine("Error: --inline schema exceeds maximum allowed size of 10 MB");
                        return 1;
                    }
                    schemaJson = inline;
                }
                else if (schemaFile is not null)
                {
                    try
                    {
                        if (new FileInfo(schemaFile).Length > 10 * 1024 * 1024)
                        {
                            Console.Error.WriteLine("Error: schema file exceeds maximum allowed size of 10 MB");
                            return 1;
                        }
                        schemaJson = File.ReadAllText(schemaFile);
                    }
                    catch (FileNotFoundException)
                    {
                        Console.Error.WriteLine($"Error: schema file '{schemaFile}' not found");
                        return 1;
                    }
                    catch (IOException ex)
                    {
                        Console.Error.WriteLine($"Error: cannot read schema file '{schemaFile}': {ex.Message}");
                        return 1;
                    }
                    catch (UnauthorizedAccessException ex)
                    {
                        Console.Error.WriteLine($"Error: cannot read schema file '{schemaFile}': {ex.Message}");
                        return 1;
                    }
                }
                else
                {
                    Console.Error.WriteLine("Error: provide either <schema-file> or --inline <json>");
                    Console.Error.WriteLine("Usage: stringgen json <schema-file> [options]");
                    Console.Error.WriteLine("       stringgen json --inline <json> [options]");
                    return 1;
                }

                var options = compact ? new JsonSerializerOptions { WriteIndented = false } : null;
                var generator = new JsonGenerator(schemaJson, seed, maxRepeat);
                foreach (var json in generator.GenerateJson(count, options, jsonMode))
                {
                    Console.WriteLine(json);
                }
                return 0;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Invalid schema: {ex.Message}");
                return 1;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 2;
            }
            catch (NotSupportedException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 2;
            }
        });

        return jsonCommand;
    }

    private static Command BuildSwaggerCommand()
    {
        var swaggerFileArg = new Argument<string>("file")
        {
            Description = "Path to a Swagger 2.0 or OpenAPI 3.x JSON document"
        };

        var schemaOption = new Option<string?>("--schema")
        {
            Description = "Name of a definition / component schema to generate"
        };

        var operationOption = new Option<string[]>("--operation")
        {
            Description = "HTTP method and path for operation-based generation (e.g. --operation POST /pets)",
            Arity = new ArgumentArity(2, 2),
            AllowMultipleArgumentsPerToken = true
        };

        var requestFlag = new Option<bool>("--request")
        {
            Description = "Generate a request body for the operation"
        };

        var responseFlag = new Option<bool>("--response")
        {
            Description = "Generate a response body for the operation"
        };

        var statusCodeOption = new Option<string>("--status-code")
        {
            Description = "HTTP status code when using --response (default 200)",
            DefaultValueFactory = _ => "200"
        };

        var countOption = new Option<int>("--count")
        {
            Description = "Number of documents to generate",
            DefaultValueFactory = _ => 1
        };

        var seedOption = new Option<int?>("--seed")
        {
            Description = "Optional random seed for reproducible output"
        };

        var maxRepeatOption = new Option<int>("--max-repeat")
        {
            Description = "Maximum repetitions for unbounded string pattern quantifiers",
            DefaultValueFactory = _ => 5
        };

        var swaggerModeOption = new Option<GenerationMode>("--mode")
        {
            Description = "Generation mode: Compliant (default) or NonCompliant",
            DefaultValueFactory = _ => GenerationMode.Compliant
        };

        var swaggerCommand = new Command(
            "swagger", "Generate random JSON from a Swagger 2.0 or OpenAPI 3.x document")
        {
            swaggerFileArg,
            schemaOption,
            operationOption,
            requestFlag,
            responseFlag,
            statusCodeOption,
            countOption,
            seedOption,
            maxRepeatOption,
            swaggerModeOption
        };

        swaggerCommand.SetAction((ParseResult parseResult) =>
        {
            var file = parseResult.GetValue(swaggerFileArg)!;
            var schema = parseResult.GetValue(schemaOption);
            var operation = parseResult.GetValue(operationOption);
            var request = parseResult.GetValue(requestFlag);
            var response = parseResult.GetValue(responseFlag);
            var statusCode = parseResult.GetValue(statusCodeOption);
            var count = parseResult.GetValue(countOption);
            var seed = parseResult.GetValue(seedOption);
            var maxRepeat = parseResult.GetValue(maxRepeatOption);
            var swaggerMode = parseResult.GetValue(swaggerModeOption);

            if (count is < 0 or > 100_000)
            {
                Console.Error.WriteLine("Error: --count must be between 0 and 100,000");
                return 1;
            }
            if (maxRepeat is < 1 or > 1_000)
            {
                Console.Error.WriteLine("Error: --max-repeat must be between 1 and 1,000");
                return 1;
            }

            try
            {
                string documentJson;
                try
                {
                    if (new FileInfo(file).Length > 10 * 1024 * 1024)
                    {
                        Console.Error.WriteLine("Error: document file exceeds maximum allowed size of 10 MB");
                        return 1;
                    }
                    documentJson = File.ReadAllText(file);
                }
                catch (FileNotFoundException)
                {
                    Console.Error.WriteLine($"Error: file '{file}' not found");
                    return 1;
                }
                catch (IOException ex)
                {
                    Console.Error.WriteLine($"Error: cannot read document file '{file}': {ex.Message}");
                    return 1;
                }
                catch (UnauthorizedAccessException ex)
                {
                    Console.Error.WriteLine($"Error: cannot read document file '{file}': {ex.Message}");
                    return 1;
                }
                var generator = new SwaggerGenerator(documentJson, seed, maxRepeat);
                IReadOnlyList<string> results;

                if (schema is not null)
                {
                    results = generator.GenerateSchema(schema, count, mode: swaggerMode);
                }
                else if (operation is { Length: 2 })
                {
                    var method = operation[0];
                    var path = operation[1];

                    if (request && response)
                    {
                        Console.Error.WriteLine("Error: --request and --response are mutually exclusive");
                        return 1;
                    }

                    results = response
                        ? generator.GenerateResponseBody(method, path, statusCode ?? "200", count, mode: swaggerMode)
                        : generator.GenerateRequestBody(method, path, count, mode: swaggerMode);
                }
                else
                {
                    Console.Error.WriteLine(
                        "Error: specify --schema <name>  or  --operation <METHOD> <path> [--request|--response]");
                    return 1;
                }

                foreach (var json in results)
                {
                    Console.WriteLine(json);
                }
                return 0;
            }
            catch (ArgumentException ex)
            {
                Console.Error.WriteLine($"Invalid document or schema: {ex.Message}");
                return 1;
            }
            catch (InvalidOperationException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 2;
            }
            catch (NotSupportedException ex)
            {
                Console.Error.WriteLine($"Error: {ex.Message}");
                return 2;
            }
        });

        return swaggerCommand;
    }
}
