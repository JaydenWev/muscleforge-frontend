import copy
import importlib.util
import json
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('generator', ROOT / 'scripts/generate-api-client.py')
generator = importlib.util.module_from_spec(spec)
spec.loader.exec_module(generator)


class GeneratorTests(unittest.TestCase):
    def setUp(self):
        self.fixture_only = json.loads((ROOT / 'tests/contract_tooling/fixtures/authenticated-feature.json').read_text())
        self.fixture = json.loads((ROOT / 'contracts/openapi.json').read_text())
        self.fixture['paths'].update(self.fixture_only['paths'])

    def test_fixture_is_reproducible(self):
        actual = generator.generate(self.fixture_only, 'MuscleForge.Client.Fixture')
        self.assertEqual(actual, (ROOT / 'tests/MuscleForge.Mobile.UnitTests/GeneratedApiClientFixture.g.cs').read_text())

    def test_rejects_unsupported_union_and_security(self):
        doc = copy.deepcopy(self.fixture)
        op = doc['paths']['/api/v1/auth/login']['post']
        op['requestBody']['content']['application/json']['schema']['properties']['email'] = {'anyOf': [{'type': 'string'}, {'type': 'boolean'}]}
        with self.assertRaises(ValueError):
            generator.generate(doc)
        doc = copy.deepcopy(self.fixture)
        doc['paths']['/api/v1/auth/me']['get']['security'] = [{'Other': []}]
        with self.assertRaises(ValueError):
            generator.generate(doc)

    def test_rejects_unknown_schema_keywords(self):
        doc = copy.deepcopy(self.fixture)
        doc['paths']['/api/v1/auth/me']['get']['responses']['200']['content']['application/json']['schema']['properties']['id']['oneOf'] = []
        with self.assertRaises(ValueError):
            generator.generate(doc)

    def test_rejects_incompatible_shapes_and_metadata(self):
        mutations = [
            lambda s: s.update(nullable="false"),
            lambda s: s.update(required=["ignored"]),
            lambda s: s.update(default="ignored"),
            lambda s: s.update(items={"type": "string"}),
            lambda s: s.update(oneOf=[{"type": "string"}]),
        ]
        for mutate in mutations:
            with self.subTest(mutation=mutate):
                doc = copy.deepcopy(self.fixture)
                schema = doc['paths']['/api/v1/auth/me']['get']['responses']['200']['content']['application/json']['schema']['properties']['id']
                mutate(schema)
                with self.assertRaises(ValueError):
                    generator.generate(doc)

    def test_rejects_problem_fields_runtime_cannot_represent(self):
        doc = copy.deepcopy(self.fixture)
        problem = doc['paths']['/api/v1/auth/me']['get']['responses']['401']['content']['application/problem+json']['schema']
        problem['properties']['extra'] = {'type': 'string'}
        problem['required'].append('extra')
        with self.assertRaises(ValueError):
            generator.generate(doc)

    def test_rejects_identifier_collisions_and_keywords(self):
        def props(doc):
            schema = doc['paths']['/api/v1/auth/me']['get']['responses']['200']['content']['application/json']['schema']
            schema['properties']['Id'] = {'type': 'string'}
        def enum(doc):
            schema = doc['paths']['/api/v1/contract-fixture/items/{id}']['put']['requestBody']['content']['application/json']['schema']['properties']['mode']
            schema['enum'] = ['foo', 'Foo']
        def operation(doc):
            doc['paths']['/api/v1/auth/me']['get']['operationId'] = 'login'
        def parameter(doc):
            doc['paths']['/api/v1/contract-fixture/items/{id}']['put']['parameters'].append({'name': 'class', 'in': 'query', 'schema': {'type': 'string'}})
        def type_name(doc):
            doc['paths']['/api/v1/auth/me']['get']['operationId'] = 'MuscleForgeApiClient'
        for mutate in (props, enum, operation, parameter, type_name):
            with self.subTest(mutation=mutate):
                doc = copy.deepcopy(self.fixture)
                mutate(doc)
                with self.assertRaises(ValueError):
                    generator.generate(doc)

    def test_number_formats_and_supported_header_metadata(self):
        doc = copy.deepcopy(self.fixture)
        op = doc['paths']['/api/v1/contract-fixture/items/{id}']['put']
        props = op['requestBody']['content']['application/json']['schema']['properties']
        props.update({name: {'type': 'number', 'format': name} for name in ('float', 'double', 'decimal')})
        op['responses']['401']['headers'] = {
            'WWW-Authenticate': {'required': True, 'schema': {'type': 'string'}},
            'Retry-After': {'schema': {'type': 'string'}},
        }
        output = generator.generate(doc)
        for typ in ('float', 'double', 'decimal'):
            self.assertIn(f'public {typ}? {typ.title()}', output)
        op['responses']['401']['headers']['Retry-After']['schema'] = {'type': 'array'}
        with self.assertRaises(ValueError):
            generator.generate(doc)

    def test_rejects_unsupported_openapi_version_and_invalid_info(self):
        for key, value in (('openapi', '3.1.0'), ('info', {}),
                           ('info', {'title': 'API', 'version': ''}),
                           ('info', {'title': 'API', 'version': 3})):
            with self.subTest(key=key, value=value):
                doc = copy.deepcopy(self.fixture)
                doc[key] = value
                with self.assertRaises(ValueError):
                    generator.generate(doc)
