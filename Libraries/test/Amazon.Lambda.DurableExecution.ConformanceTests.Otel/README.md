# OpenTelemetry conformance handlers (.NET)

Handlers and a SAM template for the `otel-invocation` and `otel-execution` suites of
[aws-durable-execution-conformance-tests](https://github.com/aws/aws-durable-execution-conformance-tests).
The requirements, validators and runner live there. This directory only holds what the SDK owns.

One executable serves every case. `template.yaml` gives each function a `CONFORMANCE_SCENARIO`
(a class in `Scenarios/`) and an `OTEL_PLUGIN_MODE` (`invocation` or `execution`).
`template.yaml` is generated: edit the case list in `scripts/generate_template.py` and rerun it.

Spans leave the function in-process. `Program.cs` builds an OpenTelemetry `TracerProvider` that listens to the
plugin's `ActivitySource` and sends OTLP to the collector Lambda layer. `AWSLambdaWrapper` (from
`OpenTelemetry.Instrumentation.AWSLambda`) starts the server span the plugin's Invocation span hangs from. It plays the
role the ADOT wrapper plays for the Java, JavaScript and Python handlers.

## Run

```bash
./scripts/build_examples.sh
durable-execution-conformance \
  --template template.yaml --language dotnet --region us-west-2 --name dotnet-xray-inv \
  --suite otel-invocation --otel-service-name durable-execution-conformance \
  --otel-exporter adot --otel-backend xray \
  --otel-layer-arn arn:aws:lambda:us-west-2:901920570463:layer:aws-otel-collector-amd64-ver-0-156-0:4 \
  --parameter-overrides LambdaExecutionRoleArn=<role> OtelServiceName=durable-execution-conformance OtelSuite=otel-invocation
```

`--language dotnet` needs the `dotnet` ADOT profile in the OTel conformance package. It uses the ADOT collector layer
and no exec wrapper.

## Coverage

| Cases | Status |
|---|---|
| 1 to 4 | handlers written |
| 5 to 27 | not yet |
