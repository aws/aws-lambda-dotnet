function Read-LambdaPSBootstrap {
    <#
    .SYNOPSIS
        Reads a bootstrap once into the normalised view every Test-LambdaPSRuntimeProject rule checks.
    .DESCRIPTION
        The single read and parse step behind the bootstrap rules, so they agree on what the file
        says.
        With -Path it reads the file: a symbolic link is followed to its final target, and the target
        must be a regular file of 1 to 1048576 bytes. A FIFO or a device reports a length of 0, like
        an empty file, so it is refused without being opened (opening a FIFO waits for a writer;
        reading a device may never end). With -Bytes it parses bytes already in hand and reads nothing.

        On success it returns an AWS.ToolExtras.LambdaPSRuntime.Bootstrap with:
          - Bytes, and ByteOrderMark: the Unicode byte order mark the bytes start with, named (for
            example 'UTF-8 (EF BB BF)'), or $null.
          - StartsWithShebang: whether the first two bytes are '#!', the only start the kernel runs.
          - Text: the bytes decoded by their mark (UTF-8 when there is none), without the mark.
          - Shebang: line 1 when it starts with '#!', else $null.
          - CodeLines: the lines the shell runs up to the pwsh invocation, as written: comment-only
            and blank lines are left out, and a trailing comment is kept.
          - ShebangRunsPwsh: whether the shebang's interpreter is a path ending in /pwsh.
          - Unrecognised: the shell constructs outside the known forms below, named (for example
            'a function' or 'a here-doc'); empty when every line is a known form. The shell rules
            report a bootstrap with any of them as unverified instead of guessing.
          - Pwsh: the one statement that runs pwsh (by bare name or a path ending in /pwsh), with
            Program, Arguments, IsExec, Assignments (NAME=VALUE set for that command: before it, or
            through 'exec env'), and Unexported (variables assigned but not exported before it);
            $null when none does.
          - Environment: the exported variables the pwsh statement inherits, by name
            (case-sensitive), with the command's own assignments applied over them.
          - Writes: the files and folders the code writes to, through an output redirect, tee or
            mkdir.

        The known forms, one statement per line or separated by ';', are what the scaffolded
        bootstraps write plus documented variants: comments and blank lines; assignments
        ('NAME=VALUE'); 'export' of names or unquoted assignments; an 'exec' that only redirects
        ('exec 2>&1', 'exec >/tmp/log 2>&1'); the plain commands echo, printf, cd, cat, tee, mkdir,
        ls, pwd, true, false and ':', with redirects and pipes (for example 'echo started | tee
        /tmp/log'); and one pwsh statement in the forms 'exec .../pwsh ...', 'NAME=1 exec .../pwsh
        ...', 'exec env NAME=1 .../pwsh ...' and 'NAME=1 .../pwsh ...' without exec, where env may
        also be /usr/bin/env or /bin/env and exec may take '--'. Nothing after an exec of pwsh runs.
        Anything else is named in Unrecognised: any other command word (eval, sh -c, trap, env,
        unset, set, timeout, or a wrapper around pwsh, for example), export with an option, a quoted
        export assignment ('export "NAME=1"') or another export argument that is not a variable
        name, an export or assignment in a pipeline (each part of a pipeline runs in a subshell), a
        '$' expansion in a write target or in PATH, LD_LIBRARY_PATH or
        DOTNET_SYSTEM_GLOBALIZATION_INVARIANT, a relative write target, printf -v, a shebang
        interpreter other than /bin/sh, /usr/bin/sh, /bin/bash or /usr/bin/bash, whitespace other
        than a space or a tab, a function, an if/case/loop block, a here-doc, a command
        substitution, a variable as the program, a '\' line continuation, a pipeline continued on
        the next line (a line ending in '|'), an '&&' or '||' list, a background '&', a subshell, a
        sourced file, a quote left open at the end of a line, a redirect with no target, an
        operator after an operator, an empty statement or pipeline element (which sh refuses as a
        syntax error), an exec of another program, or more than one pwsh invocation.

        When the file cannot be used it returns an AWS.ToolExtras.LambdaPSRuntime.BootstrapReadError
        instead, with ErrorId (BootstrapNotFound or BootstrapNotReadable), Category and Message, for
        the caller to report against the project.
    .PARAMETER Path
        The bootstrap file to read.
    .PARAMETER Bytes
        The bootstrap's bytes, to parse without reading anything.
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.Bootstrap
    .OUTPUTS
        AWS.ToolExtras.LambdaPSRuntime.BootstrapReadError
    .EXAMPLE
        $bootstrap = Read-LambdaPSBootstrap -Path ./rt/bootstrap

        Returns the parsed bootstrap, or the read error to report for ./rt.
    #>
    [CmdletBinding(DefaultParameterSetName = 'Path')]
    [OutputType('AWS.ToolExtras.LambdaPSRuntime.Bootstrap', 'AWS.ToolExtras.LambdaPSRuntime.BootstrapReadError')]
    param(
        [Parameter(Mandatory, ParameterSetName = 'Path')]
        [ValidateNotNullOrWhiteSpace()]
        [string]$Path,

        [Parameter(Mandatory, ParameterSetName = 'Bytes')]
        [AllowEmptyCollection()]
        [byte[]]$Bytes
    )

    $maxBytes = 1048576

    if ($PSCmdlet.ParameterSetName -eq 'Path') {
        $readError = {
            param([string]$ErrorId, [string]$Category, [string]$Message)
            [PSCustomObject]@{
                PSTypeName = 'AWS.ToolExtras.LambdaPSRuntime.BootstrapReadError'
                ErrorId    = $ErrorId
                Category   = $Category
                Message    = $Message
            }
        }
        # Follow a link to its final target: a dangling link is a missing bootstrap, and a link that
        # cannot be followed (a loop) is this project's read error, not one that stops the caller.
        try {
            $file = [System.IO.FileInfo]::new((Resolve-LambdaPSPath -Path $Path))
            $target = if ($file.LinkTarget) { $file.ResolveLinkTarget($true) } else { $file }
        }
        catch [System.UnauthorizedAccessException], [System.IO.IOException] {
            return & $readError 'BootstrapNotReadable' 'ReadError' "The bootstrap '$Path' could not be read: $($_.Exception.Message)"
        }
        if ($null -eq $target -or -not $target.Exists -or $target -isnot [System.IO.FileInfo]) {
            return & $readError 'BootstrapNotFound' 'ObjectNotFound' "No bootstrap file found at '$Path'."
        }
        # A FIFO or a device reports a length of 0, like an empty file: refuse all three unopened.
        if ($target.Length -eq 0) {
            return & $readError 'BootstrapNotReadable' 'ReadError' "The bootstrap '$Path' is empty, or is not a regular file (for example a FIFO or a device)."
        }
        if ($target.Length -gt $maxBytes) {
            return & $readError 'BootstrapNotReadable' 'ReadError' "The bootstrap '$Path' is $($target.Length) bytes; a bootstrap is a short sh wrapper, and more than $maxBytes bytes is not read."
        }
        try {
            $stream = [System.IO.FileStream]::new($target.FullName, [System.IO.FileMode]::Open, [System.IO.FileAccess]::Read, [System.IO.FileShare]::Read)
            try {
                $buffer = [byte[]]::new($maxBytes + 1)
                $count = 0
                while ($count -lt $buffer.Length) {
                    $read = $stream.Read($buffer, $count, $buffer.Length - $count)
                    if ($read -eq 0) { break }
                    $count += $read
                }
            }
            finally { $stream.Dispose() }
        }
        catch [System.UnauthorizedAccessException], [System.IO.IOException] {
            return & $readError 'BootstrapNotReadable' 'ReadError' "The bootstrap '$Path' could not be read: $($_.Exception.Message)"
        }
        if ($count -gt $maxBytes) {
            return & $readError 'BootstrapNotReadable' 'ReadError' "The bootstrap '$Path' grew past $maxBytes bytes while it was read."
        }
        $Bytes = [byte[]]::new($count)
        [System.Array]::Copy($buffer, $Bytes, $count)
    }

    # Longest first: the UTF-32 LE mark starts with the UTF-16 LE one.
    $marks = [ordered]@{
        'UTF-32 LE (FF FE 00 00)' = [byte[]](0xFF, 0xFE, 0x00, 0x00)
        'UTF-32 BE (00 00 FE FF)' = [byte[]](0x00, 0x00, 0xFE, 0xFF)
        'UTF-8 (EF BB BF)'        = [byte[]](0xEF, 0xBB, 0xBF)
        'UTF-16 LE (FF FE)'       = [byte[]](0xFF, 0xFE)
        'UTF-16 BE (FE FF)'       = [byte[]](0xFE, 0xFF)
    }
    $byteOrderMark = $null
    foreach ($mark in $marks.GetEnumerator()) {
        $prefix = $mark.Value
        if ($Bytes.Count -ge $prefix.Count -and
            [System.Linq.Enumerable]::SequenceEqual([byte[]]$Bytes[0..($prefix.Count - 1)], $prefix)) {
            $byteOrderMark = $mark.Key
            break
        }
    }
    $startsWithShebang = $Bytes.Count -ge 2 -and $Bytes[0] -eq 0x23 -and $Bytes[1] -eq 0x21

    $reader = [System.IO.StreamReader]::new([System.IO.MemoryStream]::new($Bytes), [System.Text.UTF8Encoding]::new($false), $true)
    try { $text = $reader.ReadToEnd() }
    finally { $reader.Dispose() }

    # Scan one line into statements, outside quotes: words (unquoted text, and whether a quote came
    # before any '='), redirects, and pipe breaks. Constructs outside the known forms are added to
    # $unrecognised by name rather than interpreted.
    $unrecognised = [System.Collections.Generic.List[string]]::new()
    $note = { param([string]$Construct) if (-not $unrecognised.Contains($Construct)) { $unrecognised.Add($Construct) } }
    $scanLine = {
        param([string]$Line)
        $statements = [System.Collections.Generic.List[object]]::new()
        $newStatement = { [PSCustomObject]@{ Segments = [System.Collections.Generic.List[object]]::new() } }
        $newSegment = { [PSCustomObject]@{ Words = [System.Collections.Generic.List[object]]::new(); Redirects = [System.Collections.Generic.List[object]]::new() } }
        $statement = & $newStatement
        $segment = & $newSegment
        $word = [System.Text.StringBuilder]::new()
        $inWord = $false
        $quoteAt = -1
        $dollar = $false
        $pendingRedirect = $null
        # Dot-sourced, so it updates the scanner's own variables.
        $flush = {
            if (-not $inWord) { return }
            $text = $word.ToString()
            if ($null -ne $pendingRedirect) { $pendingRedirect.Target = $text; $pendingRedirect.Dollar = $dollar; $segment.Redirects.Add($pendingRedirect); $pendingRedirect = $null }
            else { $segment.Words.Add([PSCustomObject]@{ Text = $text; QuoteAt = $quoteAt; Dollar = $dollar }) }
            $null = $word.Clear()
            $inWord = $false
            $quoteAt = -1
            $dollar = $false
        }
        $quote = [char]0
        $substitution = 0
        # sh refuses these with a syntax error and stops before pwsh runs.
        $isEmpty = { param($Segment) $Segment.Words.Count -eq 0 -and $Segment.Redirects.Count -eq 0 }
        $endRedirect = { if ($null -ne $pendingRedirect) { & $note 'a redirect with no target'; $pendingRedirect = $null } }
        $i = 0
        while ($i -lt $Line.Length) {
            $c = $Line[$i]
            if ($quote -ne [char]0) {
                if ($c -eq $quote) { $quote = [char]0 }
                elseif ($quote -eq '"' -and $c -eq '\' -and $i + 1 -lt $Line.Length -and '"\$`'.Contains($Line[$i + 1])) {
                    # Inside "...", \ escapes ", \, $ and a backtick.
                    $null = $word.Append($Line[$i + 1]); $i += 2; continue
                }
                else {
                    if ($quote -eq '"' -and ($c -eq '`' -or ($c -eq '$' -and $i + 1 -lt $Line.Length -and $Line[$i + 1] -eq '('))) { & $note 'a command substitution' }
                    if ($quote -eq '"' -and $c -eq '$') { $dollar = $true }
                    $null = $word.Append($c)
                }
                $i++; continue
            }
            if ($c -eq "'" -or $c -eq '"') { if ($quoteAt -lt 0) { $quoteAt = $word.Length }; $quote = $c; $inWord = $true; $i++; continue }
            if ($c -eq '#' -and -not $inWord) { break }
            if ($c -eq '\') {
                if ($i -eq $Line.Length - 1) { & $note "a '\' line continuation"; break }
                $null = $word.Append($Line[$i + 1]); $inWord = $true; $i += 2; continue
            }
            if ($c -eq '`') { & $note 'a command substitution'; $null = $word.Append($c); $inWord = $true; $i++; continue }
            if ($c -eq '$') { $dollar = $true }
            if ($c -eq '$' -and $i + 1 -lt $Line.Length -and $Line[$i + 1] -eq '(') { & $note 'a command substitution'; $null = $word.Append('$('); $inWord = $true; $substitution++; $i += 2; continue }
            if ($substitution -gt 0 -and ($c -eq '(' -or $c -eq ')')) { if ($c -eq '(') { $substitution++ } else { $substitution-- }; $null = $word.Append($c); $i++; continue }
            if ($c -eq '(' -or $c -eq ')') { & $note 'a subshell or function'; . $flush; $i++; continue }
            if ($c -eq ';') {
                . $flush; . $endRedirect
                if (& $isEmpty $segment) {
                    if ($statement.Segments.Count -gt 0) { & $note 'an empty pipeline element' } else { & $note 'an empty statement' }
                }
                $statement.Segments.Add($segment); $statements.Add($statement); $statement = & $newStatement; $segment = & $newSegment; $i++; continue
            }
            if ($c -eq '|') {
                if ($i + 1 -lt $Line.Length -and $Line[$i + 1] -eq '|') { & $note "an '&&' or '||' list"; $i += 2; continue }
                . $flush; . $endRedirect
                if (& $isEmpty $segment) { & $note 'an empty pipeline element' }
                $statement.Segments.Add($segment); $segment = & $newSegment; $i++; continue
            }
            if ($c -eq '&' -and -not ($i + 1 -lt $Line.Length -and $Line[$i + 1] -eq '>')) {
                if ($i + 1 -lt $Line.Length -and $Line[$i + 1] -eq '&') { & $note "an '&&' or '||' list"; $i += 2; continue }
                if ($i -gt 0 -and ($Line[$i - 1] -eq '>' -or $Line[$i - 1] -eq '<')) { $null = $word.Append($c); $inWord = $true; $i++; continue }
                & $note "a background '&'"; . $flush; $i++; continue
            }
            if ($c -eq '>' -or $c -eq '<' -or $c -eq '&') {
                # A redirect: an fd number just before it belongs to it, and the target follows.
                $fd = if ($inWord -and $word.ToString() -match '\A[0-9]+\z' -and $quoteAt -lt 0) { $word.ToString() } else { $null }
                if ($null -ne $fd) { $null = $word.Clear(); $inWord = $false } else { . $flush }
                if ($null -ne $pendingRedirect) { & $note 'an operator after an operator'; $pendingRedirect = $null }
                $op = [System.Text.StringBuilder]::new()
                while ($i -lt $Line.Length -and '<>&|'.Contains($Line[$i])) { $null = $op.Append($Line[$i]); $i++ }
                $opText = $op.ToString()
                if ($opText.StartsWith('<<') -and -not $opText.StartsWith('<<<')) { & $note 'a here-doc' }
                $pendingRedirect = [PSCustomObject]@{ Op = $opText; Target = $null; Output = $opText.Contains('>'); Dollar = $false }
                # '>&1' and '<&-' name a descriptor, not a file.
                if ($opText.EndsWith('&') -and $i -lt $Line.Length -and ($Line[$i] -match '[0-9-]')) {
                    $pendingRedirect.Target = [string]$Line[$i]; $pendingRedirect.Output = $false
                    $segment.Redirects.Add($pendingRedirect); $pendingRedirect = $null; $i++
                }
                continue
            }
            # sh splits words on space and tab only; other whitespace (a no-break space pasted from a
            # web page) is part of a word to it.
            if ($c -eq ' ' -or $c -eq "`t") { . $flush; $i++; continue }
            if ([char]::IsWhiteSpace($c)) { & $note 'whitespace other than a space or a tab' }
            $null = $word.Append($c); $inWord = $true; $i++
        }
        if ($quote -ne [char]0) { & $note 'a quote left open at the end of a line' }
        . $flush
        . $endRedirect
        # A line that ends in '|' carries the pipeline onto the next line, which is read on its own here.
        if ($statement.Segments.Count -gt 0 -and (& $isEmpty $segment)) { & $note 'a pipeline continued on the next line' }
        $statement.Segments.Add($segment)
        $statements.Add($statement)
        , $statements
    }
    $assignment = '\A([A-Za-z_][A-Za-z0-9_]*)=(.*)\z'
    $isAssignment = { param($Word) $Word.Text -cmatch $assignment -and ($Word.QuoteAt -lt 0 -or $Word.QuoteAt -gt $Word.Text.IndexOf('=')) }
    $runsPwsh = { param([string]$Program) $Program -ceq 'pwsh' -or $Program -cmatch '/pwsh\z' }
    # The plain commands a known-form bootstrap may run besides export, exec and pwsh: none runs other
    # code or ends the shell. printf can assign a variable with -v, so printf with -v is unverified.
    # Any other command word is unverified.
    $plainCommands = @('echo', 'printf', 'cd', 'cat', 'tee', 'mkdir', 'ls', 'pwd', 'true', 'false', ':')
    # The variables the rules read; a '$' expansion in their value is not evaluated.
    $ruleVariables = @('PATH', 'LD_LIBRARY_PATH', 'DOTNET_SYSTEM_GLOBALIZATION_INVARIANT')
    $blockWords = @('if', 'then', 'else', 'elif', 'fi', 'case', 'esac', 'for', 'while', 'until', 'do', 'done', 'select', 'function', '{', '}', '[[', ']]')

    $lines = @($text -split "`n" | ForEach-Object { $_.TrimEnd("`r") })
    $shebang = if ($lines.Count -gt 0 -and $lines[0].StartsWith('#!', [System.StringComparison]::Ordinal)) { $lines[0] } else { $null }
    # The known forms are sh code, run by sh or bash (provided.al2023 has no other shell). Another interpreter (pwsh, python, '/usr/bin/env pwsh') reads the
    # body by its own rules, so it is named rather than judged.
    $interpreter = if ($null -ne $shebang) { ($shebang.Substring(2).Trim(' ', "`t") -split '[ \t]+')[0] } else { $null }
    if ($null -ne $interpreter -and $interpreter -cnotmatch '\A(/usr)?/bin/(sh|bash)\z') { & $note "a shebang interpreter other than sh: $interpreter" }
    # Ordinal: the shell's variable names are case-sensitive, and a hashtable's keys are not.
    $variables = [System.Collections.Generic.Dictionary[string, object]]::new([System.StringComparer]::Ordinal)
    $codeLines = [System.Collections.Generic.List[string]]::new()
    $writes = [System.Collections.Generic.List[string]]::new()
    $pwsh = $null
    $pwshCount = 0
    $stop = $false
    for ($n = 0; $n -lt $lines.Count -and -not $stop; $n++) {
        if ($n -eq 0 -and $null -ne $shebang) { continue }
        if ($lines[$n].Trim(' ', "`t").Length -eq 0 -or $lines[$n].TrimStart(' ', "`t").StartsWith('#')) { continue }
        $codeLines.Add($lines[$n])
        if ($lines[$n] -cmatch '\A\s*(function\s+)?[A-Za-z_][A-Za-z0-9_]*\s*\(\s*\)') { & $note 'a function' }
        foreach ($statement in (& $scanLine $lines[$n])) {
            $first = $true
            # Each part of a pipeline runs in a subshell, so its exports and assignments never reach pwsh.
            $inPipeline = $statement.Segments.Count -gt 1
            foreach ($segment in $statement.Segments) {
                $words = $segment.Words
                foreach ($redirect in $segment.Redirects) {
                    if ($redirect.Output -and $redirect.Target) {
                        if ($redirect.Dollar) { & $note 'a variable expansion in a write target' }
                        elseif (-not $redirect.Target.StartsWith('/')) { & $note 'a relative write target' }
                        $writes.Add($redirect.Target)
                    }
                }
                if ($words.Count -eq 0) { $first = $false; continue }
                $prefix = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
                $index = 0
                while ($index -lt $words.Count -and (& $isAssignment $words[$index])) {
                    $null = $words[$index].Text -cmatch $assignment
                    if ($words[$index].Dollar -and $ruleVariables -ccontains $Matches[1]) { & $note "a variable expansion in $($Matches[1])" }
                    $prefix[$Matches[1]] = $Matches[2]
                    $index++
                }
                if ($index -ge $words.Count) {
                    if ($inPipeline) { & $note 'an export or assignment in a pipeline'; $first = $false; continue }
                    # Assignments alone set shell variables.
                    foreach ($name in $prefix.Keys) {
                        if (-not $variables.ContainsKey($name)) { $variables[$name] = [PSCustomObject]@{ Value = $null; Exported = $false } }
                        $variables[$name].Value = $prefix[$name]
                    }
                    $first = $false; continue
                }
                $command = $words[$index].Text
                if ($blockWords -ccontains $command) { & $note 'an if, case or loop block' }
                if ($command -ceq '.' -or $command -ceq 'source') { & $note 'a sourced file' }
                if ($command -ceq 'export') {
                    if ($inPipeline) { & $note 'an export or assignment in a pipeline'; $first = $false; continue }
                    for ($j = $index + 1; $j -lt $words.Count; $j++) {
                        $name = $null; $value = $null
                        if ($words[$j].Text.StartsWith('-')) { & $note 'export with an option'; continue }
                        if (& $isAssignment $words[$j]) {
                            $null = $words[$j].Text -cmatch $assignment; $name = $Matches[1]; $value = $Matches[2]
                            if ($words[$j].Dollar -and $ruleVariables -ccontains $name) { & $note "a variable expansion in $name" }
                        }
                        elseif ($words[$j].Text -cmatch '\A[A-Za-z_][A-Za-z0-9_]*\z') { $name = $words[$j].Text }
                        elseif ($words[$j].Text -cmatch $assignment) { & $note 'a quoted export assignment'; continue }
                        else { & $note 'an export argument that is not a variable name'; continue }
                        if (-not $variables.ContainsKey($name)) { $variables[$name] = [PSCustomObject]@{ Value = $null; Exported = $false } }
                        if ($null -ne $value) { $variables[$name].Value = $value }
                        $variables[$name].Exported = $true
                    }
                    $first = $false; continue
                }
                if ($command -ceq 'tee' -or $command -ceq 'mkdir') {
                    for ($j = $index + 1; $j -lt $words.Count; $j++) {
                        if ($words[$j].Text.StartsWith('-')) { continue }
                        if ($words[$j].Dollar) { & $note 'a variable expansion in a write target' }
                        elseif (-not $words[$j].Text.StartsWith('/')) { & $note 'a relative write target' }
                        $writes.Add($words[$j].Text)
                    }
                }
                $isExec = $command -ceq 'exec'
                $programIndex = $index
                if ($isExec) {
                    if (-not $first -or $statement.Segments.Count -gt 1) { & $note 'an exec in a pipeline' }
                    $programIndex++
                    # '--' ends exec's options. bash's 'exec -a NAME' is left unrecognised: what it does
                    # depends on which shell /bin/sh is.
                    if ($programIndex -lt $words.Count -and $words[$programIndex].Text -ceq '--') { $programIndex++ }
                    if ($programIndex -ge $words.Count) { $first = $false; continue }   # an exec that only redirects
                }
                if ($isExec -and $words[$programIndex].Text -cin 'env', '/usr/bin/env', '/bin/env') {
                    $programIndex++
                    while ($programIndex -lt $words.Count -and ($words[$programIndex].Text.StartsWith('-') -or (& $isAssignment $words[$programIndex]))) {
                        if ($words[$programIndex].Text.StartsWith('-')) { & $note 'an env option' }
                        else {
                            $null = $words[$programIndex].Text -cmatch $assignment
                            if ($words[$programIndex].Dollar -and $ruleVariables -ccontains $Matches[1]) { & $note "a variable expansion in $($Matches[1])" }
                            $prefix[$Matches[1]] = $Matches[2]
                        }
                        $programIndex++
                    }
                    # 'exec env' with no program replaces the shell with env, and pwsh never runs.
                    if ($programIndex -ge $words.Count) { & $note 'an exec of another program'; $first = $false; continue }
                }
                $program = $words[$programIndex].Text
                if ($program.Contains('$')) { & $note 'a variable as the program' }
                elseif (& $runsPwsh $program) {
                    $pwshCount++
                    if ($pwshCount -gt 1) { & $note 'more than one pwsh invocation' }
                    elseif ($null -eq $pwsh) {
                        $environment = [System.Collections.Generic.Dictionary[string, string]]::new([System.StringComparer]::Ordinal)
                        foreach ($entry in $variables.GetEnumerator()) { if ($entry.Value.Exported -and $null -ne $entry.Value.Value) { $environment[$entry.Key] = $entry.Value.Value } }
                        foreach ($name in $prefix.Keys) { $environment[$name] = $prefix[$name] }
                        $unexported = [string[]]@($variables.GetEnumerator() | Where-Object { -not $_.Value.Exported -and $null -ne $_.Value.Value -and -not $environment.ContainsKey($_.Key) } | ForEach-Object Key)
                        $pwsh = [PSCustomObject]@{
                            Program     = $program
                            Arguments   = [string[]]@($words | Select-Object -Skip ($programIndex + 1) | ForEach-Object Text)
                            IsExec      = $isExec
                            Assignments = $prefix
                            Environment = $environment
                            Unexported  = $unexported
                        }
                    }
                    # Nothing after an exec of pwsh runs.
                    if ($isExec) { $stop = $true }
                }
                elseif ($isExec) { & $note 'an exec of another program' }
                elseif ($plainCommands -cnotcontains $program -and $blockWords -cnotcontains $program -and $program -cne '.' -and $program -cne 'source') {
                    & $note "the command '$program'"
                }
                elseif ($program -ceq 'printf' -and @($words | Select-Object -Skip ($programIndex + 1) | Where-Object { $_.Text -cmatch '\A-[A-Za-z]*v' }).Count -gt 0) {
                    & $note 'printf -v (it assigns a variable)'
                }
                $first = $false
            }
            if ($stop) { break }
        }
    }

    [PSCustomObject]@{
        PSTypeName        = 'AWS.ToolExtras.LambdaPSRuntime.Bootstrap'
        Bytes             = $Bytes
        ByteOrderMark     = $byteOrderMark
        StartsWithShebang = $startsWithShebang
        Text              = $text
        Shebang           = $shebang
        ShebangRunsPwsh   = $null -ne $shebang -and (($shebang.Substring(2).Trim() -split '\s+')[0] -cmatch '/pwsh\z')
        CodeLines         = [string[]]$codeLines
        Unrecognised      = [string[]]$unrecognised
        Pwsh              = $pwsh
        Environment       = if ($pwsh) { $pwsh.Environment } else { $null }
        Writes            = [string[]]$writes
    }
}
