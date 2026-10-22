# Contributing

Open an issue with the problem, expected behavior, and Windows version. For file-handling bugs, use synthetic filenames and example rules; do not attach personal folder contents or credentials.

Build with `powershell -File .\build.ps1` and run `powershell -File .\test.ps1`. Tests create isolated fixtures under `work/` and use an injected settings path. Do not test rules against valuable files. Run `package.ps1` and `verify-package.ps1` for packaging changes; packaging verification requires a clean profile without an existing installed TrayTidy.

Preserve existing destination files, per-folder rule isolation, first-match ordering, and safe default pause behavior. Add tests for changed file handling. UI changes should be checked at default and minimum window sizes and with Windows display scaling. Submit focused pull requests with the problem, resulting behavior, and validation performed.

Contributions are licensed under the repository's MIT license. Do not add generated executables, personal settings, or signing credentials to source control.
