# Implementation plan

**Goal:** Use Magpie models inside Rhino 9's existing AI panel.

**Architecture:** A dependency-free Magpie agent descriptor writes dedicated JSON.
A self-contained .NET Windows launcher turns it into a child-only Claude gateway
environment, detects Rhino in the registry, and opens the native AI panel.

**Spec:** design.md

1. Test the agent descriptor, gateway configuration and rejection cases.
2. Implement a launcher core with config validation and environment isolation tests.
3. Add Windows launch and diagnostic modes plus install/uninstall scripts.
4. Verify locally, including an actual model request and Rhino panel where possible.
5. Add Windows/Linux CI tests and Windows release packaging, push to a new public
   repository, and wait for the first build.
6. Submit the source package and bilingual entry to magpie-community/plugins.

Review focus: disconnected configuration; credential leakage; URI user info and
nonlocal hosts; existing environment selecting another backend; model alias
overrides; paths with spaces; preserving other applications' settings.
