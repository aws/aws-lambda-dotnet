@{
    # The settings a deployed PowerShell function gets when New-LambdaPSFunctionDeployment is not
    # given them, and that the scaffolded function and layer READMEs suggest (the RuntimeImage README
    # does not). Lambda's own defaults (3 seconds, 128 MB) do not fit pwsh: a warm invoke can take
    # about 2 seconds at 128 MB, pwsh uses about 115-130 MB on startup, and memory also scales CPU, so
    # cold starts slow down as well.

    # Function timeout, in seconds.
    Timeout    = 30

    # Function memory, in MB.
    MemorySize = 512
}
