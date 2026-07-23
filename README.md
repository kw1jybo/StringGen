# StringGen

Random data generator for regex patterns, JSON Schema, Swagger 2.0, and OpenAPI 3.x.

---

## CLI

### Regex string generation

```
stringgen <pattern> [options]
```

| Option | Default | Description |
|---|---|---|
| `--count <n>` | `10` | Number of strings to generate |
| `--mode <mode>` | `Compliant` | `Compliant` or `NonCompliant` |
| `--seed <n>` | _(random)_ | Integer seed for reproducible output |
| `--max-repeat <n>` | `5` | Max repetitions for `*`, `+`, `{n,}` quantifiers |

**Examples**

```
stringgen "[A-Z]{3}-\d{4}"
stringgen "\d{3}-\d{2}-\d{4}" --count 5 --seed 42
stringgen "foo|bar" --mode NonCompliant --count 3
```

---

### JSON Schema generation — `json` subcommand

```
stringgen json <schema-file>  [options]
stringgen json --inline <json> [options]
```

Schema source (exactly one required):

| Argument / Option | Description |
|---|---|
| `<schema-file>` | Path to a JSON Schema file |
| `--inline <json>` | JSON Schema as an inline string |

| Option | Default | Description |
|---|---|---|
| `--count <n>` | `1` | Number of JSON documents to generate |
| `--seed <n>` | _(random)_ | Integer seed for reproducible output |
| `--max-repeat <n>` | `5` | Max repetitions for string `pattern` quantifiers |
| `--compact` | _(off)_ | Minified output instead of pretty-printed |

**Supported JSON Schema vocabulary**

| Keyword | Notes |
|---|---|
| `type` | `string`, `number`, `integer`, `boolean`, `null`, `object`, `array`; type arrays → `anyOf` |
| `enum` | Picks a random value from the list |
| `const` | Always emits the constant value |
| `anyOf` / `oneOf` | Picks a random branch |
| `allOf` | Merges all sub-schemas (intersects constraints) |
| `$ref` / `$defs` / `definitions` | Resolved within the document |
| **String** | `minLength`, `maxLength`, `pattern` (ECMAScript regex) |
| **Number / Integer** | `minimum`, `maximum`, `exclusiveMinimum`, `exclusiveMaximum`, `multipleOf` |
| **Object** | `properties`, `required`, `additionalProperties` |
| **Array** | `items`, `minItems`, `maxItems`, `uniqueItems` |
| Boolean schemas | `true` (any value) · `false` (always errors) |

**Examples**

```
stringgen json schema.json --count 10
stringgen json schema.json --seed 1 --compact
stringgen json --inline "{\"type\":\"integer\",\"minimum\":1,\"maximum\":100}" --count 5
```

---

### Swagger / OpenAPI generation — `swagger` subcommand

```
# Generate from a named definition or component schema
stringgen swagger <file> --schema <name> [options]

# Generate a request body for an operation
stringgen swagger <file> --operation <METHOD> <path> --request [options]

# Generate a response body for an operation
stringgen swagger <file> --operation <METHOD> <path> --response [options]
```

Auto-detects **Swagger 2.0** (`swagger: "2.0"`) and **OpenAPI 3.x** (`openapi: "3.x"`) documents.

| Argument / Option | Description |
|---|---|
| `<file>` | Path to the Swagger / OpenAPI JSON document |
| `--schema <name>` | Name of a `definitions` (Swagger) or `components/schemas` (OAS) entry |
| `--operation <METHOD> <path>` | HTTP method and path, e.g. `POST /pets` |
| `--request` | Generate the request body for `--operation` |
| `--response` | Generate the response body for `--operation` |
| `--status-code <code>` | Status code to use with `--response` (default `200`) |
| `--count <n>` | Number of documents to generate (default `1`) |
| `--seed <n>` | Integer seed for reproducible output |
| `--max-repeat <n>` | Max repetitions for string `pattern` quantifiers (default `5`) |

**Examples**

```
stringgen swagger petstore.json --schema Pet --count 5
stringgen swagger petstore.json --operation POST /pets --request
stringgen swagger petstore.json --operation GET  /pets --response --count 3
stringgen swagger petstore.json --operation POST /pets --response --status-code 400
stringgen openapi.json --schema Order --seed 7 --count 10
```

---

### Exit codes (all subcommands)

| Code | Meaning |
|---|---|
| `0` | Success |
| `1` | Invalid input (bad pattern / schema / file not found / unknown schema name) |
| `2` | Generation error (unsatisfiable schema, constraints conflict) |
| `3` | Unexpected internal error |

---

## .NET API

Add a project reference to `StringGen.Core`.

### `StringGenerator` — regex-driven string generation

```csharp
using StringGen.Core;

var gen = new StringGenerator(
    pattern:   @"[A-Z]{2}\d{4}",
    seed:      42,        // optional; omit for non-deterministic output
    maxRepeat: 5);        // optional; caps *, +, {n,} repetitions

IReadOnlyList<string> strings = gen.Generate(count: 10, GenerationMode.Compliant);
```

**`GenerationMode`**

| Value | Behaviour |
|---|---|
| `Compliant` _(default)_ | Every result matches the pattern (verified; throws on bug) |
| `NonCompliant` | Results intentionally do not match the pattern |

---

### `JsonGenerator` — JSON Schema–driven JSON generation

```csharp
using StringGen.Core;
using System.Text.Json;

// From a JSON Schema string
var gen = new JsonGenerator(
    schemaJson: """{"type":"object","properties":{"id":{"type":"integer"}}}""",
    seed:       1,   // optional
    maxRepeat:  5);  // optional

// Returns pretty-printed JSON strings (one per element)
IReadOnlyList<string> docs = gen.GenerateJson(count: 5);

// Custom serialiser options (e.g. compact output)
IReadOnlyList<string> compact = gen.GenerateJson(
    count:   5,
    options: new JsonSerializerOptions { WriteIndented = false });

// Returns raw JsonNode? objects if you need to manipulate the tree
IReadOnlyList<JsonNode?> nodes = gen.Generate(count: 5);

// Construct from an already-parsed JsonElement
JsonElement el = JsonDocument.Parse(schemaJson).RootElement;
var gen2 = new JsonGenerator(el, seed: 1);
```

---

### `SwaggerGenerator` — Swagger 2.0 / OpenAPI 3.x–driven JSON generation

Auto-detects the document format from the top-level `swagger` or `openapi` field.

```csharp
using StringGen.Core.Swagger;

var gen = new SwaggerGenerator(
    documentJson: File.ReadAllText("petstore.json"),
    seed:         42,   // optional
    maxRepeat:    5);   // optional

// Generate from a named definition (Swagger) or component schema (OpenAPI)
IReadOnlyList<string> pets = gen.GenerateSchema("Pet", count: 5);

// Generate a request body for an operation
IReadOnlyList<string> bodies = gen.GenerateRequestBody(
    method: "POST",
    path:   "/pets",
    count:  3);

// Generate a response body for an operation
IReadOnlyList<string> responses = gen.GenerateResponseBody(
    method:     "POST",
    path:       "/pets",
    statusCode: "200",   // default "200"
    count:      3);
```

**Supported document features**

| Feature | Swagger 2.0 | OpenAPI 3.x |
|---|---|---|
| Named schemas | `definitions` | `components/schemas` |
| Cross-schema `$ref` | `#/definitions/...` | `#/components/schemas/...` |
| Request body | `in: body` parameter | `requestBody.content["application/json"]` |
| Response body | `responses.<code>.schema` | `responses.<code>.content["application/json"]` |
| `nullable: true` | — | Normalised to `anyOf [{…}, {type:"null"}]` |

---

## Error handling

All generators throw on invalid input or unsatisfiable constraints:

| Exception | When |
|---|---|
| `ArgumentException` | Invalid pattern, malformed schema, unknown schema name |
| `InvalidOperationException` | Satisfiable but generation failed (e.g. constraint conflict, `false` schema) |
| `NotSupportedException` | Unresolved `$ref`, unsupported construct |
