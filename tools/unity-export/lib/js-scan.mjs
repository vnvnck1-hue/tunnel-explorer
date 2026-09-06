/**
 * 원본 HTML 안의 JavaScript 선언을 실행하지 않고 소스 범위만 잘라내는 스캐너.
 * dump-tuning.mjs / extract-embedded-audio.mjs 가 공유한다.
 */

/**
 * `from` 위치부터 균형 잡힌 표현식의 끝(세미콜론 직전 인덱스)을 찾는다.
 * 문자열·템플릿·주석 안의 괄호는 세지 않는다.
 */
export function findExpressionEnd(src, from) {
  let depth = 0;
  let i = from;
  while (i < src.length) {
    const c = src[i];
    const next = src[i + 1];

    if (c === '/' && next === '/') { i = src.indexOf('\n', i); if (i < 0) return -1; continue; }
    if (c === '/' && next === '*') { i = src.indexOf('*/', i + 2); if (i < 0) return -1; i += 2; continue; }

    if (c === '"' || c === "'" || c === '`') {
      const quote = c;
      i++;
      while (i < src.length) {
        if (src[i] === '\\') { i += 2; continue; }
        if (src[i] === quote) { i++; break; }
        i++;
      }
      continue;
    }

    if (c === '{' || c === '[' || c === '(') depth++;
    else if (c === '}' || c === ']' || c === ')') depth--;
    else if (c === ';' && depth === 0) return i;
    else if (c === '\n' && depth === 0) {
      const rest = src.slice(i + 1, i + 40).trimStart();
      if (/^(const|let|var|function|\/\*|\/\/)/.test(rest)) return i;
    }
    i++;
  }
  return -1;
}

/** `const NAME = <expr>` 의 우변 소스를 찾아 반환. 없으면 null. */
export function extractDeclaration(src, name) {
  const re = new RegExp(String.raw`(?:^|[\n;])\s*(?:const|let|var)\s+${name}\s*=`, 'm');
  const m = re.exec(src);
  if (!m) return null;
  const valueStart = m.index + m[0].length;
  const end = findExpressionEnd(src, valueStart);
  if (end < 0) return null;
  return {
    line: src.slice(0, m.index).split('\n').length,
    expr: src.slice(valueStart, end).trim(),
  };
}

/**
 * 객체 리터럴 안의 `key:{...}` 프로퍼티 값 소스를 잘라낸다.
 * 예: MENU_SFX 안의 `bank:{...}`
 */
export function extractProperty(src, key, fromIndex = 0) {
  const re = new RegExp(String.raw`(?:^|[\n,{])\s*${key}\s*:`, 'm');
  const slice = src.slice(fromIndex);
  const m = re.exec(slice);
  if (!m) return null;
  let valueStart = fromIndex + m.index + m[0].length;
  while (valueStart < src.length && /\s/.test(src[valueStart])) valueStart++;

  // 값이 `{` `[` `(` 로 시작하면, 짝이 닫히는 지점이 곧 값의 끝이다.
  // (닫힌 뒤에도 계속 스캔하면 뒤따르는 코드까지 삼킨다)
  const opener = src[valueStart];
  if (opener === '{' || opener === '[' || opener === '(') {
    const end = findExpressionEnd(src, valueStart + 1);
    // findExpressionEnd 는 depth 0 의 `;` 를 찾으므로, 짝 닫힘을 직접 센다.
    let d = 0, j = valueStart;
    while (j < src.length) {
      const c = src[j], n2 = src[j + 1];
      if (c === '/' && n2 === '/') { j = src.indexOf('\n', j); if (j < 0) break; continue; }
      if (c === '/' && n2 === '*') { j = src.indexOf('*/', j + 2); if (j < 0) break; j += 2; continue; }
      if (c === '"' || c === "'" || c === '`') {
        const q = c; j++;
        while (j < src.length) {
          if (src[j] === '\\') { j += 2; continue; }
          if (src[j] === q) { j++; break; }
          j++;
        }
        continue;
      }
      if (c === '{' || c === '[' || c === '(') d++;
      else if (c === '}' || c === ']' || c === ')') { d--; if (d === 0) { j++; break; } }
      j++;
    }
    void end;
    return { line: src.slice(0, valueStart).split('\n').length, expr: src.slice(valueStart, j).trim() };
  }

  // 그 밖(원시값·식별자)은 depth 0 의 `,` 또는 닫는 괄호에서 끝난다.
  let depth = 0;
  let i = valueStart;
  while (i < src.length) {
    const c = src[i];
    const next = src[i + 1];
    if (c === '/' && next === '/') { i = src.indexOf('\n', i); if (i < 0) break; continue; }
    if (c === '/' && next === '*') { i = src.indexOf('*/', i + 2); if (i < 0) break; i += 2; continue; }
    if (c === '"' || c === "'" || c === '`') {
      const q = c; i++;
      while (i < src.length) {
        if (src[i] === '\\') { i += 2; continue; }
        if (src[i] === q) { i++; break; }
        i++;
      }
      continue;
    }
    if (c === '{' || c === '[' || c === '(') depth++;
    else if (c === '}' || c === ']' || c === ')') { if (depth === 0) break; depth--; }
    else if (c === ',' && depth === 0) break;
    i++;
  }
  return { line: src.slice(0, valueStart).split('\n').length, expr: src.slice(valueStart, i).trim() };
}

/** data URI 를 {mime, ext, buffer} 로 디코드. data URI 가 아니면 null. */
export function decodeDataUri(s) {
  if (typeof s !== 'string') return null;
  const m = /^data:([^;,]+)(;base64)?,/.exec(s);
  if (!m) return null;
  const mime = m[1];
  const body = s.slice(m[0].length);
  const buffer = m[2]
    ? Buffer.from(body, 'base64')
    : Buffer.from(decodeURIComponent(body), 'binary');
  const ext = {
    'audio/mpeg': 'mp3', 'audio/mp3': 'mp3', 'audio/wav': 'wav', 'audio/x-wav': 'wav',
    'audio/ogg': 'ogg', 'audio/webm': 'webm',
    'image/png': 'png', 'image/webp': 'webp', 'image/gif': 'gif', 'image/jpeg': 'jpg',
    'font/woff2': 'woff2', 'font/woff': 'woff',
  }[mime] ?? 'bin';
  return { mime, ext, buffer };
}
