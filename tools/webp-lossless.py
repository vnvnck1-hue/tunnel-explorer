"""build-single-html.mjs 의 보조 스크립트 — PNG 를 무손실 WebP 로 변환한다.

stdin 으로 JSON 배열 [[src, dst], ...] 을 받아 각 파일을 변환하고,
stdout 에 JSON [{src, dst, ok, size, error}] 를 돌려준다.
픽셀은 그대로(lossless) 유지되며, 원본 파일은 건드리지 않는다.
"""
import io
import json
import os
import sys

try:
    from PIL import Image
except ImportError:  # 빌더가 경고 후 PNG 그대로 사용
    print(json.dumps({"error": "Pillow 가 없습니다: pip install pillow"}))
    sys.exit(3)

jobs = json.load(sys.stdin)
out = []
for src, dst in jobs:
    try:
        im = Image.open(src)
        im.load()
        if im.format == "GIF" or getattr(im, "n_frames", 1) > 1:
            out.append({"src": src, "dst": dst, "ok": False, "error": "animated/GIF skip"})
            continue
        if im.mode not in ("RGB", "RGBA"):
            im = im.convert("RGBA")
        os.makedirs(os.path.dirname(dst), exist_ok=True)
        buf = io.BytesIO()
        im.save(buf, "WEBP", lossless=True, quality=100, method=6)
        with open(dst, "wb") as f:
            f.write(buf.getvalue())
        out.append({"src": src, "dst": dst, "ok": True, "size": buf.tell()})
    except Exception as e:  # noqa: BLE001
        out.append({"src": src, "dst": dst, "ok": False, "error": str(e)})
print(json.dumps(out))
