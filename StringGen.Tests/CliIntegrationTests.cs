using System.Text.Json.Nodes;
using StringGen.Cli;

namespace StringGen.Tests;

[Collection("Cli")]
public class CliIntegrationTests
{
    private static async Task<(int Code, string Out, string Err)> RunAsync(params string[] args)
    {
        var stdout = new StringWriter();
        var stderr = new StringWriter();
        var prevOut = Console.Out;
        var prevErr = Console.Error;
        Console.SetOut(stdout);
        Console.SetError(stderr);
        try
        {
            int code = await CommandFactory.Build().Parse(args).InvokeAsync();
            return (code, stdout.ToString(), stderr.ToString());
        }
        finally
        {
            Console.SetOut(prevOut);
            Console.SetError(prevErr);
        }
    }

    [Fact]
    public async Task Root_ValidPattern_ExitCode0AndMatchingOutput()
    {
        var (code, output, _) = await RunAsync(@"^\d{4}$", "--count", "5", "--seed", "1");
        Assert.Equal(0, code);
        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(5, lines.Length);
        Assert.All(lines, static x => Assert.Matches(@"^\d{4}$", x));
    }

    [Fact]
    public async Task Root_CountZero_ExitCode0AndNoOutput()
    {
        var (code, output, _) = await RunAsync(@"^\d+$", "--count", "0");
        Assert.Equal(0, code);
        Assert.Empty(output.Trim());
    }

    [Fact]
    public async Task Root_CountTooHigh_ExitCode1()
    {
        var (code, _, err) = await RunAsync(@"^\d+$", "--count", "100001");
        Assert.Equal(1, code);
        Assert.Contains("100,000", err);
    }

    [Fact]
    public async Task Root_MaxRepeatZero_ExitCode1()
    {
        var (code, _, err) = await RunAsync(@"^\d+$", "--max-repeat", "0");
        Assert.Equal(1, code);
        Assert.Contains("max-repeat", err);
    }

    [Fact]
    public async Task Root_InvalidPattern_ExitCode1()
    {
        var (code, _, err) = await RunAsync(@"[invalid");
        Assert.Equal(1, code);
        Assert.Contains("Invalid pattern", err);
    }

    [Fact]
    public async Task Root_NonCompliantMode_ExitCode0()
    {
        var (code, output, _) = await RunAsync(@"^\d{4}$", "--count", "3", "--mode", "noncompliant", "--seed", "7");
        Assert.Equal(0, code);
        // TrimEnd + None so blank lines from empty-string violations are preserved and counted
        var lines = output.TrimEnd('\r', '\n').Split(Environment.NewLine, StringSplitOptions.None);
        Assert.Equal(3, lines.Length);
        Assert.All(lines, static x => Assert.DoesNotMatch(@"^\d{4}$", x));
    }

    [Fact]
    public async Task Json_InlineIntegerSchema_ExitCode0AndParseable()
    {
        var (code, output, _) = await RunAsync("json", "--inline", """{"type":"integer","minimum":1,"maximum":10}""", "--count", "3", "--seed", "0");
        Assert.Equal(0, code);
        var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(3, lines.Length);
        Assert.All(lines, static x => Assert.InRange(long.Parse(x), 1L, 10L));
    }

    [Fact]
    public async Task Json_InlineAndFileConflict_ExitCode1()
    {
        var (code, _, err) = await RunAsync("json", "--inline", "{}", "somefile.json");
        Assert.Equal(1, code);
        Assert.Contains("mutually exclusive", err);
    }

    [Fact]
    public async Task Json_NeitherInlineNorFile_ExitCode1()
    {
        var (code, _, err) = await RunAsync("json");
        Assert.Equal(1, code);
        Assert.Contains("provide either", err);
    }

    [Fact]
    public async Task Json_FileNotFound_ExitCode1()
    {
        var (code, _, err) = await RunAsync("json", "nonexistent_schema_xyz.json");
        Assert.Equal(1, code);
        Assert.Contains("not found", err);
    }

    [Fact]
    public async Task Json_InvalidSchema_ExitCode1()
    {
        var (code, _, err) = await RunAsync("json", "--inline", """{"type":"unknown_type"}""");
        Assert.Equal(1, code);
        Assert.Contains("Invalid schema", err);
    }

    [Fact]
    public async Task Json_InlineTooLarge_ExitCode1()
    {
        var oversized = new string('x', 10 * 1024 * 1024 + 1);
        var (code, _, err) = await RunAsync("json", "--inline", oversized);
        Assert.Equal(1, code);
        Assert.Contains("10 MB", err);
    }

    [Fact]
    public async Task Json_CountZero_ExitCode0AndNoOutput()
    {
        var (code, output, _) = await RunAsync("json", "--inline", """{"type":"string"}""", "--count", "0");
        Assert.Equal(0, code);
        Assert.Empty(output.Trim());
    }

    [Fact]
    public async Task Json_CompactFlag_EmitsMinifiedJson()
    {
        var (code, output, _) = await RunAsync("json", "--inline", """{"type":"object","properties":{"x":{"type":"integer"}},"required":["x"]}""", "--compact", "--seed", "1");
        Assert.Equal(0, code);
        var line = output.Trim();
        Assert.False(line.Contains('\n'), "compact output should be single line");
        _ = JsonNode.Parse(line);
    }

    [Fact]
    public async Task Json_InlineFromFile_ExitCode0()
    {
        var tmp = Path.GetTempFileName() + ".json";
        try
        {
            await File.WriteAllTextAsync(tmp, """{"type":"boolean"}""");
            var (code, output, _) = await RunAsync("json", tmp, "--count", "2", "--seed", "0");
            Assert.Equal(0, code);
            var lines = output.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
            Assert.Equal(2, lines.Length);
            Assert.All(lines, static x => Assert.True(x == "true" || x == "false"));
        }
        finally { File.Delete(tmp); }
    }

    private static readonly string PetstoreSwaggerJson = """
        {
            "swagger": "2.0",
            "info": { "title": "Petstore", "version": "1.0" },
            "definitions": {
                "Pet": {
                    "type": "object",
                    "required": ["id", "name"],
                    "properties": {
                        "id":   { "type": "integer", "minimum": 1 },
                        "name": { "type": "string",  "minLength": 1, "maxLength": 50 }
                    }
                }
            },
            "paths": {
                "/pets": {
                    "post": {
                        "parameters": [
                            { "in": "body", "name": "body",
                              "schema": { "$ref": "#/definitions/Pet" } }
                        ],
                        "responses": {
                            "200": { "schema": { "$ref": "#/definitions/Pet" } }
                        }
                    }
                }
            }
        }
        """;

    private async Task<string> WriteTempSwaggerAsync()
    {
        var tmp = Path.GetTempFileName() + ".json";
        await File.WriteAllTextAsync(tmp, PetstoreSwaggerJson);
        return tmp;
    }

    [Fact]
    public async Task Swagger_GenerateSchema_ExitCode0AndValidJson()
    {
        var tmp = await WriteTempSwaggerAsync();
        try
        {
            var (code, output, _) = await RunAsync("swagger", tmp, "--schema", "Pet", "--seed", "1");
            Assert.Equal(0, code);
            var node = Assert.IsType<JsonObject>(JsonNode.Parse(output.Trim()));
            Assert.True(node.ContainsKey("id"));
            Assert.True(node.ContainsKey("name"));
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Swagger_UnknownSchema_ExitCode1()
    {
        var tmp = await WriteTempSwaggerAsync();
        try
        {
            var (code, _, err) = await RunAsync("swagger", tmp, "--schema", "NoSuchSchema");
            Assert.Equal(1, code);
            Assert.Contains("Invalid document or schema", err);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Swagger_FileNotFound_ExitCode1()
    {
        var (code, _, err) = await RunAsync("swagger", "nosuchfile.json", "--schema", "Pet");
        Assert.Equal(1, code);
        Assert.Contains("not found", err);
    }

    [Fact]
    public async Task Swagger_MissingSchemaAndOperation_ExitCode1()
    {
        var tmp = await WriteTempSwaggerAsync();
        try
        {
            var (code, _, err) = await RunAsync("swagger", tmp);
            Assert.Equal(1, code);
            Assert.Contains("--schema", err);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Swagger_RequestAndResponseConflict_ExitCode1()
    {
        var tmp = await WriteTempSwaggerAsync();
        try
        {
            var (code, _, err) = await RunAsync("swagger", tmp, "--operation", "POST", "/pets", "--request", "--response");
            Assert.Equal(1, code);
            Assert.Contains("mutually exclusive", err);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Swagger_GenerateRequestBody_ExitCode0()
    {
        var tmp = await WriteTempSwaggerAsync();
        try
        {
            var (code, output, _) = await RunAsync("swagger", tmp, "--operation", "POST", "/pets", "--request", "--seed", "2");
            Assert.Equal(0, code);
            _ = JsonNode.Parse(output.Trim());
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Swagger_GenerateResponseBody_ExitCode0()
    {
        var tmp = await WriteTempSwaggerAsync();
        try
        {
            var (code, output, _) = await RunAsync("swagger", tmp, "--operation", "POST", "/pets", "--response", "--status-code", "200", "--seed", "3");
            Assert.Equal(0, code);
            _ = JsonNode.Parse(output.Trim());
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Root_CountExactMax_ExitCode0()
    {
        var (code, _, _) = await RunAsync(@"^\d$", "--count", "100000", "--seed", "0");
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Root_MaxRepeatMin_ExitCode0()
    {
        var (code, _, _) = await RunAsync(@"^\d+$", "--count", "3", "--max-repeat", "1", "--seed", "0");
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Root_MaxRepeatMax_ExitCode0()
    {
        var (code, _, _) = await RunAsync(@"^\d+$", "--count", "3", "--max-repeat", "1000", "--seed", "0");
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Root_MaxRepeatTooHigh_ExitCode1()
    {
        var (code, _, err) = await RunAsync(@"^\d+$", "--max-repeat", "1001");
        Assert.Equal(1, code);
        Assert.Contains("max-repeat", err);
    }

    [Fact]
    public async Task Json_MaxRepeatMin_ExitCode0()
    {
        var (code, _, _) = await RunAsync("json", "--inline", """{"type":"integer"}""", "--max-repeat", "1", "--seed", "0");
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Json_MaxRepeatMax_ExitCode0()
    {
        var (code, _, _) = await RunAsync("json", "--inline", """{"type":"integer"}""", "--max-repeat", "1000", "--seed", "0");
        Assert.Equal(0, code);
    }

    [Fact]
    public async Task Json_MaxRepeatTooHigh_ExitCode1()
    {
        var (code, _, err) = await RunAsync("json", "--inline", """{"type":"integer"}""", "--max-repeat", "1001");
        Assert.Equal(1, code);
        Assert.Contains("max-repeat", err);
    }

    [Fact]
    public async Task Json_FileTooLarge_ExitCode1()
    {
        var tmp = Path.GetTempFileName() + ".json";
        try
        {
            await File.WriteAllTextAsync(tmp, new string('x', 10 * 1024 * 1024 + 1));
            var (code, _, err) = await RunAsync("json", tmp);
            Assert.Equal(1, code);
            Assert.Contains("10 MB", err);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Swagger_MaxRepeatMin_ExitCode0()
    {
        var tmp = await WriteTempSwaggerAsync();
        try
        {
            var (code, _, _) = await RunAsync("swagger", tmp, "--schema", "Pet", "--max-repeat", "1", "--seed", "0");
            Assert.Equal(0, code);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Swagger_MaxRepeatMax_ExitCode0()
    {
        var tmp = await WriteTempSwaggerAsync();
        try
        {
            var (code, _, _) = await RunAsync("swagger", tmp, "--schema", "Pet", "--max-repeat", "1000", "--seed", "0");
            Assert.Equal(0, code);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Swagger_MaxRepeatTooHigh_ExitCode1()
    {
        var tmp = await WriteTempSwaggerAsync();
        try
        {
            var (code, _, err) = await RunAsync("swagger", tmp, "--schema", "Pet", "--max-repeat", "1001");
            Assert.Equal(1, code);
            Assert.Contains("max-repeat", err);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Swagger_FileTooLarge_ExitCode1()
    {
        var tmp = Path.GetTempFileName() + ".json";
        try
        {
            await File.WriteAllTextAsync(tmp, new string('x', 10 * 1024 * 1024 + 1));
            var (code, _, err) = await RunAsync("swagger", tmp, "--schema", "Any");
            Assert.Equal(1, code);
            Assert.Contains("10 MB", err);
        }
        finally { File.Delete(tmp); }
    }

    [Fact]
    public async Task Swagger_CountTooHigh_ExitCode1()
    {
        var tmp = await WriteTempSwaggerAsync();
        try
        {
            var (code, _, err) = await RunAsync("swagger", tmp, "--schema", "Pet", "--count", "200000");
            Assert.Equal(1, code);
            Assert.Contains("100,000", err);
        }
        finally { File.Delete(tmp); }
    }
}

[CollectionDefinition("Cli")]
public class CliCollection { }
