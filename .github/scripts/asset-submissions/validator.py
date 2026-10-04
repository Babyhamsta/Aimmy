"""Static, bounded data checks. Never load external tensor files or run inference."""
import json
import math
from pathlib import Path
import sys

MAX_DEPTH = 64
MAX_MESSAGES = 200_000


def validate_config(raw):
    if not 0 < len(raw) <= 1024 * 1024:
        raise ValueError("Config must be nonempty and at most 1 MiB.")

    def unique(pairs):
        result = {}
        for key, value in pairs:
            if key in result:
                raise ValueError("Duplicate JSON keys are not allowed.")
            result[key] = value
        return result

    def no_constant(_):
        raise ValueError("JSON numbers must be finite.")

    try:
        data = json.loads(raw.decode("utf-8-sig"), object_pairs_hook=unique, parse_constant=no_constant)
    except (UnicodeError, json.JSONDecodeError, RecursionError):
        raise ValueError("Config must contain valid UTF-8 JSON.") from None
    if not isinstance(data, dict):
        raise ValueError("Config must be a JSON object.")
    pending = [(data, 0)]
    count = 0
    while pending:
        value, depth = pending.pop()
        count += 1
        if depth > 32 or count > 100_000:
            raise ValueError("JSON is too deeply nested or complex.")
        if isinstance(value, float) and not math.isfinite(value):
            raise ValueError("JSON numbers must be finite.")
        if isinstance(value, dict):
            pending.extend((child, depth + 1) for child in value.values())
        elif isinstance(value, list):
            pending.extend((child, depth + 1) for child in value)


def check_messages(model):
    # Inspect every protobuf message, including tensors inside subgraphs,
    # sparse tensors, function attributes and training_info. No tensor arrays.
    from google.protobuf.descriptor import FieldDescriptor
    import onnx

    pending = [(model, 0)]
    count = 0
    while pending:
        message, depth = pending.pop()
        count += 1
        if depth > MAX_DEPTH or count > MAX_MESSAGES:
            raise ValueError("ONNX structure exceeds validation limits.")
        if isinstance(message, onnx.TensorProto):
            if message.data_location == onnx.TensorProto.EXTERNAL or message.external_data:
                raise ValueError("External tensor data is not allowed; export a self-contained ONNX file.")
        for field, value in message.ListFields():
            if field.type == FieldDescriptor.TYPE_MESSAGE:
                if field.is_repeated:
                    # Avoid constructing an unbounded work list before checking it.
                    if len(value) + len(pending) + count > MAX_MESSAGES:
                        raise ValueError("ONNX structure exceeds validation limits.")
                    pending.extend((child, depth + 1) for child in value)
                else:
                    pending.append((value, depth + 1))


def shape(value):
    import onnx
    if not value.type.HasField("tensor_type") or value.type.tensor_type.elem_type != onnx.TensorProto.FLOAT:
        raise ValueError("Inputs and outputs must be float32 tensors.")
    tensor = value.type.tensor_type
    if not tensor.HasField("shape"):
        raise ValueError("Input and output ranks must be declared.")
    dimensions = []
    for dimension in tensor.shape.dim:
        if dimension.HasField("dim_value"):
            if dimension.dim_value == -1:
                dimensions.append(None)
                continue
            if dimension.dim_value <= 0:
                raise ValueError("Fixed dimensions must be positive.")
            dimensions.append(dimension.dim_value)
        else:
            dimensions.append(None)
    return dimensions


def validate_model(raw, enforce_size=True):
    if enforce_size and not 5 * 1024 * 1024 <= len(raw) <= 50 * 1024 * 1024:
        raise ValueError("Models must be 5–50 MiB.")
    import onnx
    try:
        # Parse bytes only. onnx.load(path) defaults to loading external files.
        model = onnx.load_model_from_string(raw, format="protobuf")
    except Exception:
        raise ValueError("File is not a valid binary ONNX model.") from None
    check_messages(model)
    if len(model.graph.input) != 1 or len(model.graph.output) != 1:
        raise ValueError("Model must declare exactly one input and one output.")
    inputs = shape(model.graph.input[0])
    outputs = shape(model.graph.output[0])
    if len(inputs) != 4 or inputs[1] != 3:
        raise ValueError("Input must have shape [B, 3, H, W].")
    if len(outputs) != 3 or (outputs[1] is not None and outputs[1] < 5):
        raise ValueError("Output must have shape [B, >=5, N].")
    # Dynamic resolution, dynamic anchor count and multiclass outputs are allowed.
    try:
        onnx.checker.check_model(model, full_check=False)
    except Exception:
        raise ValueError("ONNX graph failed the static consistency check.") from None


def main():
    if len(sys.argv) != 3 or sys.argv[1] not in {"model", "config"}:
        raise SystemExit(1)
    kind, filename = sys.argv[1:]
    limit = 50 * 1024 * 1024 if kind == "model" else 1024 * 1024
    try:
        with Path(filename).open("rb") as stream:
            raw = stream.read(limit + 1)
        if len(raw) > limit:
            raise ValueError("File exceeds the size limit.")
        (validate_model if kind == "model" else validate_config)(raw)
    except ValueError as error:
        print(json.dumps({"valid": False, "message": str(error)[:500]}))
        return 2
    print(json.dumps({"valid": True, "message": "Static validation passed."}))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
