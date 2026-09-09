"""Remove calibration-only tile transforms from ReferenceCalibrationV1.

The board-direct approval scene must show the same bitmap everywhere.  This
keeps the grid and sorting intact while clearing the old V1 variant rotations,
mirrors, tints, and hero scale overrides from the serialized scene.
"""

import re
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SCENE = ROOT / "unity/TunnelCrew/Assets/_Project/Scenes/ReferenceCalibrationV1.unity"
ART = ROOT / "unity/TunnelCrew/Assets/Art/Visual/ReferenceCalibrationV1"


def replace_line(block: str, key: str, value: str) -> str:
    return re.sub(rf"^\s*{re.escape(key)}:.*$", f"  {key}: {value}", block, flags=re.MULTILINE)


def main() -> None:
    floor_guids = []
    for key in ("a", "b", "c"):
        meta = (ART / f"tr01_reference_floor_{key}_albedo.png.meta").read_text(encoding="utf-8")
        floor_guids.append(re.search(r"^guid: ([0-9a-f]+)$", meta, flags=re.MULTILINE).group(1))

    original = SCENE.read_text(encoding="utf-8")
    blocks = re.split(r"(?=--- !u!\d+ &\d+\n)", original)
    names: dict[str, str] = {}
    for block in blocks:
        header = re.match(r"--- !u!1 &(\d+)", block)
        if not header:
            continue
        name = re.search(r"^  m_Name: (.+)$", block, flags=re.MULTILINE)
        if name:
            names[header.group(1)] = name.group(1)

    changed = 0
    output: list[str] = []
    for block in blocks:
        transform = re.match(r"--- !u!4 &\d+", block)
        renderer = re.match(r"--- !u!212 &\d+", block)
        game_object = re.search(r"^  m_GameObject: \{fileID: (\d+)\}$", block, flags=re.MULTILINE)
        name = names.get(game_object.group(1), "") if game_object else ""
        direct = name.startswith("Floor ") or name in {"Reference Wall", "Reference Crystal", "Reference Driller"}
        if direct and transform:
            block = replace_line(block, "m_LocalRotation", "{x: 0, y: 0, z: 0, w: 1}")
            block = replace_line(block, "m_LocalScale", "{x: 1, y: 1, z: 1}")
            changed += 1
        if direct and renderer:
            block = replace_line(block, "m_Color", "{r: 1, g: 1, b: 1, a: 1}")
            floor = re.match(r"Floor (-?\d+),(-?\d+)", name)
            if floor:
                x, y = int(floor.group(1)), int(floor.group(2))
                floor_index = abs(x * 17 + y * 31 + x * y * 7) % len(floor_guids)
                block = re.sub(
                    r"^  m_Sprite: \{fileID: 21300000, guid: [0-9a-f]+, type: 3\}$",
                    f"  m_Sprite: {{fileID: 21300000, guid: {floor_guids[floor_index]}, type: 3}}",
                    block,
                    flags=re.MULTILINE,
                )
            changed += 1
        output.append(block)

    SCENE.write_text("".join(output), encoding="utf-8", newline="\n")
    print(f"Updated {SCENE}")
    print(f"Changed serialized transform/renderer records: {changed}")


if __name__ == "__main__":
    main()
