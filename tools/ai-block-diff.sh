#!/usr/bin/env bash
# AI 크루·관전 블록만 뽑아 두 버전 HTML을 비교한다.
#
#   bash tools/ai-block-diff.sh A.html B.html
#
# 머지 전에 "상대 버전에도 AI 변경이 들어갔는지" 확인하는 용도다.
# 차이 0줄이면 블록 통째 이식이 안전하고, 차이가 있으면
# ai/patch-ai-behavior.py 를 상대 버전 베이스로 재실행해야 한다.
# (docs/ai-behavior-v7.8.1-merge-spec.md §1)

set -u

extract() {  # $1=파일 $2=시작 마커 $3=출력경로
  local f="$1" marker="$2" out="$3"
  local s e
  s=$(grep -n "$marker" "$f" | tail -1 | cut -d: -f1)
  if [ -z "${s:-}" ]; then
    : > "$out"                       # 마커가 없으면 빈 파일 — "블록 없음"
    return 1
  fi
  e=$(awk -v s="$s" 'NR>s+2 && /^<\/script>/{print NR; exit}' "$f")
  sed -n "$((s + 2)),$((e - 1))p" "$f" > "$out"
  echo "$((e - s))"
}

if [ $# -lt 2 ]; then
  echo "사용법: bash tools/ai-block-diff.sh A.html B.html"
  exit 2
fi

A="$1"; B="$2"
for f in "$A" "$B"; do
  [ -f "$f" ] || { echo "파일이 없다: $f"; exit 2; }
done

TMP="${TMPDIR:-/tmp}/ai-block-diff.$$"
mkdir -p "$TMP"
trap 'rm -rf "$TMP"' EXIT

status=0
for pair in "AI_CREW_INJECTED_V1:ai" "OBSERVER_MODE_INJECTED_V1:obs"; do
  marker="${pair%%:*}"; tag="${pair##*:}"
  la=$(extract "$A" "$marker" "$TMP/$tag.a") || la="없음"
  lb=$(extract "$B" "$marker" "$TMP/$tag.b") || lb="없음"
  n=$(diff "$TMP/$tag.a" "$TMP/$tag.b" | grep -c '^[<>]' || true)
  printf '%-28s %s: %s줄  %s: %s줄  →  차이 %s줄\n' \
    "$marker" "$(basename "$A")" "$la" "$(basename "$B")" "$lb" "$n"
  if [ "$n" != "0" ]; then
    status=1
    diff "$TMP/$tag.a" "$TMP/$tag.b" | head -40
    echo "  ... (전체 차이는 diff 로 직접 확인)"
  fi
done

if [ "$status" = "0" ]; then
  echo "동일 — 블록 통째 이식 가능"
else
  echo "차이 있음 — patch-ai-behavior.py 를 상대 버전 베이스로 재실행할 것"
fi
exit "$status"
