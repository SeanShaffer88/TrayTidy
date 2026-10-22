# Security

The latest published release is the supported version. Update before reporting a bug.

For a potential security issue, use GitHub's **Report a vulnerability** feature on this repository's Security tab. Avoid publishing sensitive paths, personal files, or exploit details in a public issue before the problem has been assessed.

TrayTidy runs as the current user and can move files that user can access. It does not elevate, upload files, accept network commands, or provide a security boundary against another process running as the same user. Linked destinations and files are rejected as a best-effort protection; filesystem changes can race validation. Do not run it on folders writable by untrusted users or use it as a security isolation tool.

Release binaries are unsigned. SHA-256 checksums detect download corruption and can be compared with release asset digests; they do not replace code signing or independently authenticate the publisher.
