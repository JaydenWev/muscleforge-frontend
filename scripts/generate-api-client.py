#!/usr/bin/env python3
"""Generate a C# client from the committed, pinned OpenAPI subset (v2)."""
import argparse
import hashlib
import json
from pathlib import Path
import re

ROOT = Path(__file__).resolve().parents[1]
VERSION = 2


def identifier(value):
    if not isinstance(value, str) or not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*', value):
        raise ValueError(f'Unsupported C# identifier: {value}')
    return value[0].upper() + value[1:]


def generate(document, namespace='MuscleForge.Client'):
    info = document.get('info')
    if (document.get('openapi') != '3.0.3' or not isinstance(info, dict)
            or set(info) != {'title', 'version'}
            or any(not isinstance(info[key], str) or not info[key].strip() for key in ('title', 'version'))):
        raise ValueError('Unsupported OpenAPI version or info')
    if document.get('security', []) != [] or not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*(?:\.[A-Za-z][A-Za-z0-9]*)*', namespace):
        raise ValueError('Unsupported global security or namespace')
    if set(document) - {'openapi', 'info', 'paths', 'components'} or set(document.get('components', {})) - {'securitySchemes'}:
        raise ValueError('Unsupported document components')
    schemes = document.get('components', {}).get('securitySchemes', {})
    if schemes and schemes != {'Bearer': {'scheme': 'bearer', 'type': 'http'}}:
        raise ValueError('Unsupported security scheme')
    definitions = []
    names = {'apiproblem', 'apiexception', 'muscleforgeapiclient'}
    operation_names = {'escape', 'throwapiexception'}
    keywords = set('abstract as base bool break byte case catch char checked class const continue decimal default delegate do double else enum event explicit extern false finally fixed float for foreach goto if implicit in int interface internal is lock long namespace new null object operator out override params private protected public readonly ref return sbyte sealed short sizeof stackalloc static string struct switch this throw true try typeof uint ulong unchecked unsafe ushort using virtual void volatile while'.split())

    def schema_type(schema, name, optional=False):
        if not isinstance(schema, dict):
            raise ValueError('Invalid schema')
        allowed = {'type', 'format', 'nullable', 'properties', 'required', 'items', 'additionalProperties', 'enum', 'anyOf', 'pattern', 'minLength', 'maxLength', 'description'}
        if set(schema) - allowed:
            raise ValueError(f'Unsupported schema keywords: {set(schema) - allowed}')
        if 'nullable' in schema and not isinstance(schema['nullable'], bool):
            raise ValueError('Invalid nullable')
        schema_kind = 'union' if 'anyOf' in schema else schema.get('type')
        allowed_by_type = {
            'union': {'anyOf', 'format', 'pattern', 'nullable', 'description'},
            'object': {'type', 'properties', 'required', 'additionalProperties', 'nullable', 'description'},
            'array': {'type', 'items', 'nullable', 'description'},
            'string': {'type', 'format', 'enum', 'pattern', 'minLength', 'maxLength', 'nullable', 'description'},
            'integer': {'type', 'format', 'nullable', 'description'},
            'number': {'type', 'format', 'nullable', 'description'},
            'boolean': {'type', 'nullable', 'description'},
        }
        if schema_kind not in allowed_by_type or set(schema) - allowed_by_type[schema_kind]:
            raise ValueError('Schema keywords do not match its type')
        if 'pattern' in schema and not isinstance(schema['pattern'], str):
            raise ValueError('Invalid pattern')
        if any(key in schema and (type(schema[key]) is not int or schema[key] < 0) for key in ('minLength', 'maxLength')):
            raise ValueError('Invalid string length')
        if 'minLength' in schema and 'maxLength' in schema and schema['minLength'] > schema['maxLength']:
            raise ValueError('Invalid string length range')
        nullable = schema.get('nullable', False) or optional
        if 'anyOf' in schema:
            if schema['anyOf'] != [{'type': 'integer'}, {'type': 'string'}] or schema.get('format') != 'int64' or schema.get('pattern') not in (None, r'^-?(?:0|[1-9]\d*)$'):
                raise ValueError('Unsupported union')
            typ = 'long'
        elif 'enum' in schema:
            if schema.get('type') != 'string' or schema.get('format') is not None or not isinstance(schema['enum'], list) or not schema['enum'] or any(not isinstance(x, str) for x in schema['enum']) or len(set(schema['enum'])) != len(schema['enum']):
                raise ValueError('Unsupported enum')
            enum_name = identifier(name)
            if enum_name.casefold() in names:
                raise ValueError(f'Duplicate type {enum_name}')
            names.add(enum_name.casefold())
            members = []
            member_names = set()
            for value in schema['enum']:
                member = identifier(value)
                if member.casefold() in member_names or member.casefold() == enum_name.casefold():
                    raise ValueError('Duplicate enum member')
                member_names.add(member.casefold())
                members += [f'    [JsonStringEnumMemberName({json.dumps(value)})]', f'    {member},']
            definitions.append('\n'.join(['[JsonConverter(typeof(JsonStringEnumConverter))]', f'public enum {enum_name}', '{', *members, '}', '']))
            typ = enum_name
        elif schema.get('type') == 'object':
            if 'additionalProperties' in schema:
                if set(schema) - {'type', 'additionalProperties', 'nullable', 'description'}:
                    raise ValueError('Unsupported map')
                typ = f'Dictionary<string, {schema_type(schema["additionalProperties"], name + "Value")}>'
            else:
                typ = identifier(name)
                if typ.casefold() in names:
                    raise ValueError(f'Duplicate type {typ}')
                names.add(typ.casefold())
                props = schema.get('properties', {})
                required = schema.get('required', [])
                if (not isinstance(props, dict) or not isinstance(required, list)
                        or any(not isinstance(x, str) for x in required) or len(set(required)) != len(required)
                        or not set(required) <= set(props)):
                    raise ValueError('Invalid object properties')
                property_names = [identifier(key).casefold() for key in props]
                if len(set(property_names)) != len(property_names) or typ.casefold() in property_names:
                    raise ValueError('Duplicate property name')
                lines = [f'public sealed class {typ}', '{']
                for key, prop in sorted(props.items()):
                    prop_name = identifier(key)
                    pt = schema_type(prop, typ + prop_name, key not in required)
                    lines.append(f'    [JsonPropertyName({json.dumps(key)})]')
                    if key not in required and not prop.get('nullable', False):
                        lines.append('    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]')
                    if 'anyOf' in prop:
                        lines.append('    [JsonNumberHandling(JsonNumberHandling.AllowReadingFromString)]')
                    req = 'required ' if key in required else ''
                    lines += [f'    public {req}{pt} {prop_name} {{ get; init; }}', '']
                definitions.append('\n'.join(lines + ['}', '']))
        elif schema.get('type') == 'array':
            typ = schema_type(schema['items'], name + 'Item') + '[]'
        elif schema.get('type') == 'string':
            fmt = schema.get('format')
            if fmt not in (None, 'uuid', 'date-time'):
                raise ValueError('Unsupported string format')
            typ = {None: 'string', 'uuid': 'Guid', 'date-time': 'DateTimeOffset'}[fmt]
        elif schema.get('type') == 'integer':
            fmt = schema.get('format')
            if fmt not in (None, 'int32', 'int64'):
                raise ValueError('Unsupported integer format')
            typ = 'long' if fmt == 'int64' else 'int'
        elif schema.get('type') == 'number':
            if schema.get('format') not in (None, 'decimal', 'double', 'float'):
                raise ValueError('Unsupported number format')
            # Unformatted numbers use decimal for predictable precision.
            typ = {None: 'decimal', 'decimal': 'decimal', 'double': 'double', 'float': 'float'}[schema.get('format')]
        elif schema.get('type') == 'boolean':
            typ = 'bool'
        else:
            raise ValueError('Unsupported schema type')
        return typ + ('?' if nullable else '')

    methods = []
    for path, operations in sorted(document['paths'].items()):
        if not re.fullmatch(r'/[a-zA-Z0-9/_{}-]+', path):
            raise ValueError(f'Unsupported route: {path}')
        for verb, op in sorted(operations.items()):
            if verb not in ('get', 'post', 'put', 'patch', 'delete') or set(op) - {'operationId', 'parameters', 'requestBody', 'security', 'responses', 'summary', 'description', 'tags'}:
                raise ValueError('Unsupported operation')
            opname = identifier(op['operationId'])
            if opname.casefold() in operation_names or opname.casefold() == 'muscleforgeapiclient':
                raise ValueError('Duplicate or reserved operation name')
            operation_names.add(opname.casefold())
            security = op.get('security', [])
            if security not in ([], [{'Bearer': []}]) or (security and not schemes):
                raise ValueError('Unsupported operation security')
            responses = op['responses']
            successes = [k for k in responses if k.startswith('2')]
            if successes != ['200']:
                raise ValueError('Only JSON 200 success is supported')
            def content_schema(spec, media):
                if (set(spec) - {'description', 'content', 'headers'} or set(spec.get('content', {})) != {media}
                        or set(spec['content'][media]) != {'schema'}):
                    raise ValueError('Unsupported response content')
                headers = spec.get('headers', {})
                if not isinstance(headers, dict) or any(name not in ('Retry-After', 'WWW-Authenticate') for name in headers):
                    raise ValueError('Unsupported response header')
                for header in headers.values():
                    if (not isinstance(header, dict) or set(header) - {'description', 'required', 'schema'}
                            or ('required' in header and type(header['required']) is not bool)
                            or header.get('schema') != {'type': 'string'}):
                        raise ValueError('Unsupported response header schema')
                return spec['content'][media]['schema']
            response = schema_type(content_schema(responses['200'], 'application/json'), opname.removeprefix('Get') + 'Response')
            for code, spec in responses.items():
                if code != '200':
                    if not re.fullmatch(r'[45]\d\d', code) or 'application/problem+json' not in spec.get('content', {}):
                        raise ValueError('Unsupported error response')
                    problem = content_schema(spec, 'application/problem+json')
                    # Runtime has one shared superset, so reject error shapes it cannot represent.
                    expected = {'type': {'type': 'string'}, 'title': {'type': 'string'},
                                'status': {'type': 'integer', 'format': 'int32'},
                                'code': {'type': 'string'}, 'traceId': {'type': 'string'},
                                'errors': {'type': 'object', 'additionalProperties': {'type': 'array', 'items': {'type': 'string'}}}}
                    props = problem.get('properties', {})
                    required = problem.get('required', [])
                    if (set(problem) != {'type', 'properties', 'required'} or problem['type'] != 'object'
                            or not isinstance(props, dict) or not isinstance(required, list)
                            or len(set(required)) != len(required) or set(required) != set(props)
                            or set(props) not in (set(expected) - {'errors'}, set(expected))
                            or any(props[k] != expected[k] for k in props)):
                        raise ValueError('Unsupported problem schema')
            args = ['Uri baseUrl']
            if security:
                args.append('string bearerToken')
            body = op.get('requestBody')
            if 'requestBody' in op:
                if set(body) != {'required', 'content'} or body.get('required') is not True or set(body['content']) != {'application/json'} or set(body['content']['application/json']) != {'schema'}:
                    raise ValueError('Unsupported request body')
                bt = schema_type(body['content']['application/json']['schema'], opname + 'Request')
                args.append(bt + ' body')
            route = path.lstrip('/')
            queries = []
            params = op.get('parameters', [])
            required_params = [p for p in params if p.get('required')]
            optional_params = [p for p in params if not p.get('required')]
            parameter_names = [p.get('name') for p in params]
            if (any(not isinstance(n, str) for n in parameter_names) or len({n.casefold() for n in parameter_names}) != len(parameter_names)
                    or any(n.casefold() in {'baseurl', 'bearertoken', 'body', 'cancellationtoken', 'path', 'query', 'request', 'response', 'httpclient'} or n in keywords for n in parameter_names)):
                raise ValueError('Duplicate or reserved parameter')
            for p in required_params + optional_params:
                if set(p) - {'name', 'in', 'required', 'schema', 'description', 'style', 'explode'} or p['in'] not in ('path', 'query') or ('required' in p and type(p['required']) is not bool) or p['schema'].get('type') not in ('string', 'integer', 'number', 'boolean') or p['schema'].get('nullable') or 'enum' in p['schema'] or p['schema'].get('format') == 'date-time':
                    raise ValueError('Unsupported parameter')
                if p.get('style', 'simple' if p['in'] == 'path' else 'form') != ('simple' if p['in'] == 'path' else 'form') or ('explode' in p and type(p['explode']) is not bool):
                    raise ValueError('Unsupported parameter serialization')
                pn = p['name']; identifier(pn)
                pt = schema_type(p['schema'], opname + identifier(pn), not p.get('required'))
                args.append(f'{pt} {pn}' + ('' if p.get('required') else ' = null'))
                if p['in'] == 'path':
                    if not p.get('required') or '{' + pn + '}' not in route:
                        raise ValueError('Invalid path parameter')
                    route = route.replace('{' + pn + '}', '{Escape(' + pn + ')}')
                else:
                    queries.append((pn, p.get('required', False)))
            if re.search(r'\{(?!Escape\()[^}]+\}', route):
                raise ValueError('Missing path parameter')
            args.append('CancellationToken cancellationToken = default')
            lines = [f'    public async Task<{response}> {opname}Async(', '        ' + ', '.join(args) + ')', '    {', f'        var path = $"{route}";']
            if queries:
                lines.append('        var query = new List<string>();')
                for q, required in queries:
                    guard = '' if required else f'if ({q} is not null) '
                    lines.append(f'        {guard}query.Add("{q}=" + Escape({q}));')
                lines.append('        if (query.Count > 0) path += "?" + string.Join("&", query);')
            lines += [f'        using var request = new HttpRequestMessage(HttpMethod.{verb.capitalize()}, new Uri(baseUrl, path));']
            if security:
                lines.append('        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);')
            if 'requestBody' in op:
                lines.append('        request.Content = JsonContent.Create(body);')
            lines += ['        using var response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);', '        if (response.StatusCode != HttpStatusCode.OK) await ThrowApiException(response, cancellationToken).ConfigureAwait(false);', f'        return await response.Content.ReadFromJsonAsync<{response}>(cancellationToken: cancellationToken).ConfigureAwait(false)', '            ?? throw new JsonException("The API returned an empty response.");', '    }', '']
            methods += lines
    header = [f'// <auto-generated> MuscleForge contract generator v{VERSION}. Do not edit. </auto-generated>', '#nullable enable', 'using System.Globalization;', 'using System.Net;', 'using System.Net.Http.Headers;', 'using System.Net.Http.Json;', 'using System.Text.Json;', 'using System.Text.Json.Serialization;', '', f'namespace {namespace};', '']
    support = '''public sealed class ApiProblem
{
    public string? Type { get; init; }
    public string? Title { get; init; }
    public int? Status { get; init; }
    public string? Code { get; init; }
    public string? TraceId { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }
}

public sealed class ApiException(HttpStatusCode statusCode, ApiProblem? problem, RetryConditionHeaderValue? retryAfter)
    : HttpRequestException($"The API request failed with status {(int)statusCode}.", null, statusCode)
{
    public ApiProblem? Problem { get; } = problem;
    public RetryConditionHeaderValue? RetryAfter { get; } = retryAfter;
}

public sealed class MuscleForgeApiClient(HttpClient httpClient)
{
    private static string Escape(object value) => Uri.EscapeDataString(
        value is bool b ? (b ? "true" : "false") : Convert.ToString(value, CultureInfo.InvariantCulture)!);

    private static async Task ThrowApiException(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        ApiProblem? problem = null;
        if (response.Content.Headers.ContentType?.MediaType == "application/problem+json")
        {
            try
            {
                // Bound untrusted error bodies, including chunked responses.
                await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using var buffer = new MemoryStream();
                var chunk = new byte[4096];
                int count;
                while ((count = await stream.ReadAsync(chunk.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
                {
                    if (buffer.Length + count > 65536) break;
                    buffer.Write(chunk, 0, count);
                }
                if (count == 0)
                {
                    buffer.Position = 0;
                    problem = await JsonSerializer.DeserializeAsync<ApiProblem>(buffer, new JsonSerializerOptions(JsonSerializerDefaults.Web), cancellationToken).ConfigureAwait(false);
                }
            }
            catch (JsonException) { }
        }
        throw new ApiException(response.StatusCode, problem, response.Headers.RetryAfter);
    }
'''
    return '\n'.join(header + definitions + [support, *methods, '}', ''])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--check', action='store_true')
    args = parser.parse_args()
    contract = (ROOT / 'contracts/openapi.json').read_bytes()
    source = json.loads((ROOT / 'contracts/source.json').read_text())
    if not re.fullmatch(r'[0-9a-f]{40}', source.get('backendCommit') or ''):
        raise SystemExit('Pin an explicit backend commit in contracts/source.json')
    if hashlib.sha256(contract).hexdigest() != source['contractSha256']:
        raise SystemExit('Contract hash does not match contracts/source.json')
    if source['generatorVersion'] != VERSION:
        raise SystemExit('Contract source expects a different generator version')
    document = json.loads(contract)
    if (source.get('backendRepository') != 'https://github.com/JaydenWev/muscleforge-backend'
            or source.get('backendVersion') != document.get('info', {}).get('version')
            or source.get('apiVersion') != 'v1'
            or any(not path.startswith('/api/' + source['apiVersion'] + '/') for path in document['paths'])):
        raise SystemExit('Contract source metadata does not match the snapshot')
    output = generate(document)
    target = ROOT / 'src/MuscleForge.Client/Generated/MuscleForgeApiClient.g.cs'
    if args.check:
        if not target.is_file() or target.read_text() != output:
            raise SystemExit('Generated-client drift: run python3 scripts/generate-api-client.py')
        print('Generated client and pinned contract hash match')
    else:
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(output)
        print(f'Generated {target}')


if __name__ == '__main__':
    main()
