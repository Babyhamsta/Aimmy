"""Only self-generated data fixtures; never executes an uploaded model."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from unittest.mock import patch

import onnx
from onnx import TensorProto, helper
import validator


def model(inputs=(1, 3, 640, 640), outputs=(1, 5, 8400), dtype=TensorProto.FLOAT):
    # A tiny constant-producing graph is sufficient to test metadata contracts.
    value = helper.make_tensor("result", TensorProto.FLOAT, [1, 5, 1], [0.0] * 5)
    graph = helper.make_graph([helper.make_node("Constant", [], ["output"], value=value)], "fixture",
        [helper.make_tensor_value_info("input", dtype, inputs)],
        [helper.make_tensor_value_info("output", TensorProto.FLOAT, outputs)])
    return helper.make_model(graph)


def validate(value):
    return validator.validate_model(value.SerializeToString(), enforce_size=False)


def external():
    tensor = TensorProto(name="external", data_type=TensorProto.FLOAT, dims=[1], data_location=TensorProto.EXTERNAL)
    tensor.external_data.add(key="location", value="/host/secret")
    return tensor


class ConfigTests(unittest.TestCase):
    def test_valid_objects_and_bom(self):
        for raw in [b'{}', b'{"sensitivity":0.25,"name":"test","nested":[true,null]}', b'\xef\xbb\xbf{}']:
            validator.validate_config(raw)

    def test_invalid_json_or_root(self):
        for raw in [b'', b'{', b'[]', b'null', b'1', b'"a"', b'\xff']:
            with self.subTest(raw=raw), self.assertRaises(ValueError):
                validator.validate_config(raw)

    def test_duplicates(self):
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            validator.validate_config(b'{"a":1,"a":2}')
        with self.assertRaisesRegex(ValueError, 'Duplicate'):
            validator.validate_config(b'{"nested":{"a":1,"a":2}}')

    def test_nonfinite(self):
        for value in ['NaN', 'Infinity', '-Infinity', '1e999']:
            with self.subTest(value=value), self.assertRaisesRegex(ValueError, 'finite'):
                validator.validate_config(('{"a":' + value + '}').encode())

    def test_size(self):
        with self.assertRaisesRegex(ValueError, '1 MiB'):
            validator.validate_config(b' ' * (1024 * 1024 + 1))

    def test_depth_and_parser_recursion(self):
        for depth in [40, 2000]:
            with self.subTest(depth=depth), self.assertRaises(ValueError):
                validator.validate_config(('{"a":' * depth + '0' + '}' * depth).encode())

    def test_node_limit(self):
        with self.assertRaisesRegex(ValueError, 'complex'):
            validator.validate_config(json.dumps({'a': [0] * 100001}).encode())


class ModelTests(unittest.TestCase):
    def test_static_640_single_class(self):
        validate(model())

    def test_static_non640_multiclass_arbitrary_anchors(self):
        for dims in [(1, 3, 320, 320), (1, 3, 1280, 736), (1, 3, 960, 960)]:
            with self.subTest(dims=dims):
                validate(model(dims, (1, 84, 2100)))

    def test_symbolic_and_anonymous_dynamic(self):
        validate(model(('batch', 3, 'height', 'width'), ('batch', 84, 'anchors')))
        validate(model((None, 3, None, None), (None, None, None)))

    def test_legacy_minus_one_dynamic(self):
        validate(model((-1, 3, -1, -1), (-1, 84, -1)))

    def test_invalid_ranks_channels_and_zero_dimensions(self):
        for inputs, outputs in [((1, 3, 640), (1, 5, 8400)), ((1, 1, 640, 640), (1, 5, 8400)),
            ((1, 3, 640, 640), (1, 4, 8400)), ((1, 3, 640, 640), (1, 5)),
            ((1, 3, 0, 640), (1, 5, 8400)), ((1, 3, -2, 640), (1, 5, 8400))]:
            with self.subTest(inputs=inputs, outputs=outputs), self.assertRaises(ValueError):
                validate(model(inputs, outputs))

    def test_float_type(self):
        with self.assertRaisesRegex(ValueError, 'float32'):
            validate(model(dtype=TensorProto.FLOAT16))

    def test_missing_inputs_and_outputs(self):
        for name in ['input', 'output']:
            value = model()
            del getattr(value.graph, name)[:]
            with self.subTest(name=name), self.assertRaisesRegex(ValueError, 'exactly one'):
                validate(value)

    def test_invalid_protobuf(self):
        with self.assertRaisesRegex(ValueError, 'binary ONNX'):
            validator.validate_model(b'not a protobuf', enforce_size=False)

    def test_graph_checker_rejects_bad_graph(self):
        value = model()
        value.graph.node[0].op_type = 'NoSuchStandardOperator'
        with self.assertRaisesRegex(ValueError, 'consistency'):
            validate(value)

    def test_size_policy(self):
        with self.assertRaisesRegex(ValueError, '5–50 MiB'):
            validator.validate_model(model().SerializeToString())
        with self.assertRaisesRegex(ValueError, '5–50 MiB'):
            validator.validate_model(b'x' * (50 * 1024 * 1024 + 1))

    def test_no_external_load_or_shape_inference(self):
        with patch('onnx.load', side_effect=AssertionError('must not load files')), \
             patch('onnx.shape_inference.infer_shapes', side_effect=AssertionError('must not infer shapes')):
            validate(model())

    def test_external_tensors_everywhere(self):
        def initializer(m): m.graph.initializer.append(external())
        def attribute(m): m.graph.node[0].attribute.add(name='hidden', type=onnx.AttributeProto.TENSOR).t.CopyFrom(external())
        def sparse(m): m.graph.sparse_initializer.add().values.CopyFrom(external())
        def subgraph(m): m.graph.node[0].attribute.add(name='body', type=onnx.AttributeProto.GRAPH).g.initializer.append(external())
        def training(m): m.training_info.add().initialization.initializer.append(external())
        def function(m): m.functions.add().node.add().attribute.add(name='hidden', type=onnx.AttributeProto.TENSOR).t.CopyFrom(external())
        for insert in [initializer, attribute, sparse, subgraph, training, function]:
            value = model(); insert(value)
            with self.subTest(place=insert.__name__), self.assertRaisesRegex(ValueError, 'External tensor'):
                validate(value)

    def test_external_metadata_even_without_external_enum(self):
        value = model(); tensor = external(); tensor.data_location = TensorProto.DEFAULT
        value.graph.initializer.append(tensor)
        with self.assertRaisesRegex(ValueError, 'External tensor'):
            validate(value)

    def test_protobuf_depth_limit(self):
        value = model(); graph = value.graph
        for _ in range(30):
            graph = graph.node.add().attribute.add(name='body', type=onnx.AttributeProto.GRAPH).g
        with self.assertRaisesRegex(ValueError, 'structure'):
            validator.check_messages(value)

    def test_protobuf_message_count_limit(self):
        value = model()
        with patch.object(validator, 'MAX_MESSAGES', 5), self.assertRaisesRegex(ValueError, 'structure'):
            validator.check_messages(value)

    def test_cli_size_checked_valid_model_and_invalid_config(self):
        value = model(('batch', 3, 'h', 'w'), ('batch', 84, 'anchors'))
        value.doc_string = 'x' * (5 * 1024 * 1024)
        with tempfile.TemporaryDirectory() as directory:
            filename = Path(directory) / 'model.bin'; filename.write_bytes(value.SerializeToString())
            result = subprocess.run([sys.executable, '-I', str(Path(validator.__file__)), 'model', str(filename)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 0, result.stderr)
            self.assertTrue(json.loads(result.stdout)['valid'])
            filename.write_bytes(b'{"x":NaN}')
            result = subprocess.run([sys.executable, '-I', str(Path(validator.__file__)), 'config', str(filename)], capture_output=True, text=True)
            self.assertEqual(result.returncode, 2)
            self.assertFalse(json.loads(result.stdout)['valid'])


if __name__ == '__main__':
    unittest.main()
