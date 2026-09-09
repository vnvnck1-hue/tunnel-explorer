#!/usr/bin/env python3
"""Guard against Unity MCP calls landing in the wrong editor.

The Unity MCP server is app-global: its "active instance" is shared by every
Claude conversation and is re-derived when the connection drops, so a pin set
with set_active_instance can silently move to whichever editor connected last.
With two editors open (SlimeForge and TunnelCrew) that means a call meant for
this project can execute in the other one.

Per-call routing (`unity_instance`) is the only form that cannot be hijacked,
so this hook denies any mcp__unityMCP__* call that does not carry it, and any
call whose instance resolves to a different Unity project than this repo.

The expected instance is resolved from ~/.unity-mcp/unity-mcp-status-*.json by
project path rather than a hard-coded hash, so it keeps working on any machine
and survives a hash change.
"""

import glob
import json
import os
import sys

# set_active_instance takes its own `instance` argument and is how routing is
# repaired, so it must stay callable.
EXEMPT_TOOLS = {"mcp__unityMCP__set_active_instance"}


def deny(reason):
    json.dump(
        {
            "hookSpecificOutput": {
                "hookEventName": "PreToolUse",
                "permissionDecision": "deny",
                "permissionDecisionReason": reason,
            }
        },
        sys.stdout,
    )
    sys.exit(0)


def allow():
    sys.exit(0)


def normalize(path):
    # realpath resolves junctions/symlinks, so a project opened through the
    # C:\Users\<user>\TunnelCrew junction and one opened through the real folder
    # it points at compare equal (the status file records the junction path).
    return os.path.normcase(os.path.realpath(path)).replace("\\", "/").rstrip("/")


def project_root():
    root = os.environ.get("CLAUDE_PROJECT_DIR")
    if root:
        return root
    # The hook runs from the session's working directory.
    return os.getcwd()


def read_instances():
    """hash -> project_path for every Unity editor the MCP bridge knows about."""
    pattern = os.path.join(
        os.path.expanduser("~"), ".unity-mcp", "unity-mcp-status-*.json"
    )
    found = {}
    for path in glob.glob(pattern):
        name = os.path.basename(path)
        instance_hash = name[len("unity-mcp-status-") : -len(".json")]
        try:
            with open(path, encoding="utf-8") as handle:
                data = json.load(handle)
        except (OSError, ValueError):
            continue
        project_path = data.get("project_path")
        if project_path:
            found[instance_hash] = (project_path, data.get("project_name", "?"))
    return found


def main():
    try:
        payload = json.load(sys.stdin)
    except ValueError:
        allow()

    tool_name = payload.get("tool_name", "")
    if not tool_name.startswith("mcp__unityMCP__"):
        allow()
    if tool_name in EXEMPT_TOOLS:
        allow()

    tool_input = payload.get("tool_input") or {}
    supplied = (tool_input.get("unity_instance") or "").strip()

    instances = read_instances()
    # Assets lives under the repo root; status files record the Assets path.
    expected_assets = normalize(os.path.join(project_root(), "Assets"))
    expected = [
        (h, name)
        for h, (path, name) in instances.items()
        if normalize(path) == expected_assets
    ]

    if not expected:
        # Cannot prove which editor is right; do not block legitimate work.
        allow()

    expected_hash, expected_name = expected[0]
    expected_id = "{}@{}".format(expected_name, expected_hash)

    if not supplied:
        deny(
            "Unity MCP routing is app-global and drifts between the two open "
            "editors, so this call could execute in the wrong project. Re-issue "
            "it with unity_instance=\"{}\".".format(expected_id)
        )

    supplied_hash = supplied.split("@")[-1]
    if supplied_hash != expected_hash:
        actual = instances.get(supplied_hash)
        target = actual[1] if actual else "unknown editor"
        deny(
            "unity_instance=\"{}\" points at {}, not this project. Re-issue it "
            "with unity_instance=\"{}\".".format(supplied, target, expected_id)
        )

    allow()


if __name__ == "__main__":
    main()
