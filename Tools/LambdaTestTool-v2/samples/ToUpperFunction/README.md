# ToUpperFunction

The gentlest sample: a zero-dependency function that uppercases its input string. Good for a first look at the [web UI](../../README.md#using-the-web-ui) — no API Gateway or AWS resources needed.

## Run it

**1. Start the Lambda emulator:**

```
dotnet lambda-test-tool start --lambda-emulator-port 5050
```

The web UI opens at `http://localhost:5050`.

**2. Start the function** (separate terminal, this directory):

```
dotnet run --launch-profile ToUpperFunction
```

**3. Invoke it from the web UI:**

1. Select `ToUpperFunction` as the function.
2. In the Function Input editor, enter a JSON string, e.g. `"hello world"`.
3. Click **Invoke**. The response is `"HELLO WORLD"`.

Try `"error"` as the input to see how the tool renders a thrown exception and stack trace.
