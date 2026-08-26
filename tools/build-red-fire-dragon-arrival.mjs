import fs from "node:fs";
import path from "node:path";
import zlib from "node:zlib";

const projectRoot = path.resolve(import.meta.dirname, "..");
const arrivalRoot = path.join(projectRoot, "assets", "red-fire-dragon", "arrival");
const sourceDir = path.join(arrivalRoot, "frames", "source");
const frameDir = path.join(arrivalRoot, "frames");
const gifPath = path.join(arrivalRoot, "red-fire-dragon-arrival.gif");
const canvasSize = 512;
const safeBox = 448;

function paeth(a, b, c) {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  return pb <= pc ? b : c;
}

function readPng(filePath) {
  const file = fs.readFileSync(filePath);
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  if (!file.subarray(0, 8).equals(signature)) throw new Error(`Not a PNG: ${filePath}`);

  let width;
  let height;
  let bitDepth;
  let colorType;
  let interlace;
  const idat = [];
  for (let offset = 8; offset < file.length;) {
    const length = file.readUInt32BE(offset);
    const type = file.toString("ascii", offset + 4, offset + 8);
    const data = file.subarray(offset + 8, offset + 8 + length);
    offset += length + 12;
    if (type === "IHDR") {
      width = data.readUInt32BE(0);
      height = data.readUInt32BE(4);
      bitDepth = data[8];
      colorType = data[9];
      interlace = data[12];
    } else if (type === "IDAT") idat.push(data);
    else if (type === "IEND") break;
  }

  if (bitDepth !== 8 || ![2, 6].includes(colorType) || interlace !== 0) {
    throw new Error(`Unsupported PNG: bitDepth=${bitDepth}, colorType=${colorType}, interlace=${interlace}`);
  }

  const sourceChannels = colorType === 6 ? 4 : 3;
  const stride = width * sourceChannels;
  const inflated = zlib.inflateSync(Buffer.concat(idat));
  const decoded = Buffer.alloc(width * height * sourceChannels);
  let sourceOffset = 0;
  for (let y = 0; y < height; y += 1) {
    const filter = inflated[sourceOffset];
    sourceOffset += 1;
    const rowOffset = y * stride;
    const previousRowOffset = rowOffset - stride;
    for (let x = 0; x < stride; x += 1) {
      const raw = inflated[sourceOffset + x];
      const left = x >= sourceChannels ? decoded[rowOffset + x - sourceChannels] : 0;
      const up = y > 0 ? decoded[previousRowOffset + x] : 0;
      const upLeft = y > 0 && x >= sourceChannels
        ? decoded[previousRowOffset + x - sourceChannels]
        : 0;
      let value;
      if (filter === 0) value = raw;
      else if (filter === 1) value = raw + left;
      else if (filter === 2) value = raw + up;
      else if (filter === 3) value = raw + Math.floor((left + up) / 2);
      else if (filter === 4) value = raw + paeth(left, up, upLeft);
      else throw new Error(`Unsupported PNG filter ${filter}`);
      decoded[rowOffset + x] = value & 0xff;
    }
    sourceOffset += stride;
  }

  const rgba = Buffer.alloc(width * height * 4);
  for (let pixel = 0; pixel < width * height; pixel += 1) {
    rgba[pixel * 4] = decoded[pixel * sourceChannels];
    rgba[pixel * 4 + 1] = decoded[pixel * sourceChannels + 1];
    rgba[pixel * 4 + 2] = decoded[pixel * sourceChannels + 2];
    rgba[pixel * 4 + 3] = sourceChannels === 4 ? decoded[pixel * 4 + 3] : 255;
  }
  return { width, height, rgba };
}

function removeConnectedCheckerboard(image) {
  const { width, height, rgba } = image;
  const background = new Uint8Array(width * height);
  const queue = [];
  const isBackgroundColor = (pixel) => {
    const offset = pixel * 4;
    const r = rgba[offset];
    const g = rgba[offset + 1];
    const b = rgba[offset + 2];
    return Math.min(r, g, b) >= 205 && Math.max(r, g, b) - Math.min(r, g, b) <= 28;
  };
  const enqueue = (pixel) => {
    if (!background[pixel] && isBackgroundColor(pixel)) {
      background[pixel] = 1;
      queue.push(pixel);
    }
  };
  for (let x = 0; x < width; x += 1) {
    enqueue(x);
    enqueue((height - 1) * width + x);
  }
  for (let y = 0; y < height; y += 1) {
    enqueue(y * width);
    enqueue(y * width + width - 1);
  }
  for (let head = 0; head < queue.length; head += 1) {
    const pixel = queue[head];
    const x = pixel % width;
    const y = Math.floor(pixel / width);
    if (x > 0) enqueue(pixel - 1);
    if (x + 1 < width) enqueue(pixel + 1);
    if (y > 0) enqueue(pixel - width);
    if (y + 1 < height) enqueue(pixel + width);
  }

  for (let pass = 0; pass < 3; pass += 1) {
    const add = [];
    for (let pixel = 0; pixel < background.length; pixel += 1) {
      if (background[pixel]) continue;
      const x = pixel % width;
      const y = Math.floor(pixel / width);
      const offset = pixel * 4;
      const r = rgba[offset];
      const g = rgba[offset + 1];
      const b = rgba[offset + 2];
      if (Math.min(r, g, b) < 190 || Math.max(r, g, b) - Math.min(r, g, b) > 38) continue;
      const touches = (x > 0 && background[pixel - 1])
        || (x + 1 < width && background[pixel + 1])
        || (y > 0 && background[pixel - width])
        || (y + 1 < height && background[pixel + width]);
      if (touches) add.push(pixel);
    }
    for (const pixel of add) background[pixel] = 1;
  }

  let removed = 0;
  for (let pixel = 0; pixel < background.length; pixel += 1) {
    if (!background[pixel]) continue;
    const offset = pixel * 4;
    rgba.fill(0, offset, offset + 4);
    removed += 1;
  }
  return removed;
}

function fillTinyAlphaHoles(image, maxArea = 16) {
  const { width, height, rgba } = image;
  const visited = new Uint8Array(width * height);
  const repaired = [];
  for (let start = 0; start < visited.length; start += 1) {
    if (visited[start] || rgba[start * 4 + 3] > 8) continue;
    const queue = [start];
    const component = [];
    visited[start] = 1;
    let touchesEdge = false;
    for (let head = 0; head < queue.length; head += 1) {
      const pixel = queue[head];
      component.push(pixel);
      const x = pixel % width;
      const y = Math.floor(pixel / width);
      if (x === 0 || y === 0 || x === width - 1 || y === height - 1) touchesEdge = true;
      const neighbors = [];
      if (x > 0) neighbors.push(pixel - 1);
      if (x + 1 < width) neighbors.push(pixel + 1);
      if (y > 0) neighbors.push(pixel - width);
      if (y + 1 < height) neighbors.push(pixel + width);
      for (const next of neighbors) {
        if (!visited[next] && rgba[next * 4 + 3] <= 8) {
          visited[next] = 1;
          queue.push(next);
        }
      }
    }
    if (touchesEdge || component.length > maxArea) continue;
    const set = new Set(component);
    const replacements = [];
    for (const pixel of component) {
      const x = pixel % width;
      const y = Math.floor(pixel / width);
      let found = null;
      for (let radius = 1; radius <= 8 && found === null; radius += 1) {
        for (let dy = -radius; dy <= radius && found === null; dy += 1) {
          for (let dx = -radius; dx <= radius; dx += 1) {
            if (Math.abs(dx) !== radius && Math.abs(dy) !== radius) continue;
            const nx = x + dx;
            const ny = y + dy;
            if (nx < 0 || ny < 0 || nx >= width || ny >= height) continue;
            const neighbor = ny * width + nx;
            if (set.has(neighbor) || rgba[neighbor * 4 + 3] < 128) continue;
            const offset = neighbor * 4;
            found = [rgba[offset], rgba[offset + 1], rgba[offset + 2]];
            break;
          }
        }
      }
      if (found === null) break;
      replacements.push([pixel, found]);
    }
    if (replacements.length !== component.length) continue;
    for (const [pixel, color] of replacements) {
      const offset = pixel * 4;
      rgba[offset] = color[0];
      rgba[offset + 1] = color[1];
      rgba[offset + 2] = color[2];
      rgba[offset + 3] = 255;
    }
    repaired.push(component.length);
  }
  return repaired;
}

function resizeIntoSafeCanvas(image) {
  const scale = safeBox / Math.max(image.width, image.height);
  const scaledWidth = Math.max(1, Math.round(image.width * scale));
  const scaledHeight = Math.max(1, Math.round(image.height * scale));
  const xOffset = Math.floor((canvasSize - scaledWidth) / 2);
  const yOffset = Math.floor((canvasSize - scaledHeight) / 2);
  const output = Buffer.alloc(canvasSize * canvasSize * 4);

  for (let y = 0; y < scaledHeight; y += 1) {
    const sourceY = (y + 0.5) / scale - 0.5;
    const y0 = Math.max(0, Math.floor(sourceY));
    const y1 = Math.min(image.height - 1, y0 + 1);
    const fy = Math.max(0, sourceY - y0);
    for (let x = 0; x < scaledWidth; x += 1) {
      const sourceX = (x + 0.5) / scale - 0.5;
      const x0 = Math.max(0, Math.floor(sourceX));
      const x1 = Math.min(image.width - 1, x0 + 1);
      const fx = Math.max(0, sourceX - x0);
      const samples = [
        [x0, y0, (1 - fx) * (1 - fy)],
        [x1, y0, fx * (1 - fy)],
        [x0, y1, (1 - fx) * fy],
        [x1, y1, fx * fy],
      ];
      let alpha = 0;
      let red = 0;
      let green = 0;
      let blue = 0;
      for (const [sx, sy, weight] of samples) {
        const offset = (sy * image.width + sx) * 4;
        const normalizedAlpha = image.rgba[offset + 3] / 255;
        const weightedAlpha = weight * normalizedAlpha;
        alpha += weightedAlpha;
        red += image.rgba[offset] * weightedAlpha;
        green += image.rgba[offset + 1] * weightedAlpha;
        blue += image.rgba[offset + 2] * weightedAlpha;
      }
      const destination = ((y + yOffset) * canvasSize + x + xOffset) * 4;
      if (alpha > 0) {
        output[destination] = Math.round(red / alpha);
        output[destination + 1] = Math.round(green / alpha);
        output[destination + 2] = Math.round(blue / alpha);
        output[destination + 3] = Math.round(alpha * 255);
      }
    }
  }
  return { width: canvasSize, height: canvasSize, rgba: output };
}

function scaleCanvasContent(image, factor) {
  const output = Buffer.alloc(image.width * image.height * 4);
  const centerX = (image.width - 1) / 2;
  const centerY = (image.height - 1) / 2;
  for (let y = 0; y < image.height; y += 1) {
    const sourceY = centerY + (y - centerY) / factor;
    if (sourceY < 0 || sourceY > image.height - 1) continue;
    const y0 = Math.floor(sourceY);
    const y1 = Math.min(image.height - 1, y0 + 1);
    const fy = sourceY - y0;
    for (let x = 0; x < image.width; x += 1) {
      const sourceX = centerX + (x - centerX) / factor;
      if (sourceX < 0 || sourceX > image.width - 1) continue;
      const x0 = Math.floor(sourceX);
      const x1 = Math.min(image.width - 1, x0 + 1);
      const fx = sourceX - x0;
      const samples = [
        [x0, y0, (1 - fx) * (1 - fy)],
        [x1, y0, fx * (1 - fy)],
        [x0, y1, (1 - fx) * fy],
        [x1, y1, fx * fy],
      ];
      let alpha = 0, red = 0, green = 0, blue = 0;
      for (const [sx, sy, weight] of samples) {
        const sourceOffset = (sy * image.width + sx) * 4;
        const normalizedAlpha = image.rgba[sourceOffset + 3] / 255;
        const weightedAlpha = weight * normalizedAlpha;
        alpha += weightedAlpha;
        red += image.rgba[sourceOffset] * weightedAlpha;
        green += image.rgba[sourceOffset + 1] * weightedAlpha;
        blue += image.rgba[sourceOffset + 2] * weightedAlpha;
      }
      if (alpha === 0) continue;
      const destination = (y * image.width + x) * 4;
      output[destination] = Math.round(red / alpha);
      output[destination + 1] = Math.round(green / alpha);
      output[destination + 2] = Math.round(blue / alpha);
      output[destination + 3] = Math.round(alpha * 255);
    }
  }
  return { width: image.width, height: image.height, rgba: output };
}

function crc32(buffer) {
  let crc = 0xffffffff;
  for (const byte of buffer) {
    crc ^= byte;
    for (let bit = 0; bit < 8; bit += 1) crc = (crc >>> 1) ^ ((crc & 1) ? 0xedb88320 : 0);
  }
  return (crc ^ 0xffffffff) >>> 0;
}

function pngChunk(type, data) {
  const typeBuffer = Buffer.from(type, "ascii");
  const output = Buffer.alloc(data.length + 12);
  output.writeUInt32BE(data.length, 0);
  typeBuffer.copy(output, 4);
  data.copy(output, 8);
  output.writeUInt32BE(crc32(Buffer.concat([typeBuffer, data])), data.length + 8);
  return output;
}

function writePng(filePath, image) {
  const header = Buffer.alloc(13);
  header.writeUInt32BE(image.width, 0);
  header.writeUInt32BE(image.height, 4);
  header[8] = 8;
  header[9] = 6;
  const scanlines = Buffer.alloc(image.height * (image.width * 4 + 1));
  for (let y = 0; y < image.height; y += 1) {
    const row = y * (image.width * 4 + 1);
    scanlines[row] = 0;
    image.rgba.copy(scanlines, row + 1, y * image.width * 4, (y + 1) * image.width * 4);
  }
  const signature = Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]);
  fs.writeFileSync(filePath, Buffer.concat([
    signature,
    pngChunk("IHDR", header),
    pngChunk("IDAT", zlib.deflateSync(scanlines, { level: 9 })),
    pngChunk("IEND", Buffer.alloc(0)),
  ]));
}

function makeColorBox(entries) {
  let minR = Infinity, minG = Infinity, minB = Infinity;
  let maxR = -Infinity, maxG = -Infinity, maxB = -Infinity;
  let population = 0;
  for (const entry of entries) {
    minR = Math.min(minR, entry.r); minG = Math.min(minG, entry.g); minB = Math.min(minB, entry.b);
    maxR = Math.max(maxR, entry.r); maxG = Math.max(maxG, entry.g); maxB = Math.max(maxB, entry.b);
    population += entry.count;
  }
  return { entries, population, rangeR: maxR - minR, rangeG: maxG - minG, rangeB: maxB - minB };
}

function quantizeFrame(image) {
  const counts = new Uint32Array(32768);
  const sums = [new Uint32Array(32768), new Uint32Array(32768), new Uint32Array(32768)];
  for (let pixel = 0; pixel < image.width * image.height; pixel += 1) {
    const offset = pixel * 4;
    if (image.rgba[offset + 3] < 96) continue;
    const key = ((image.rgba[offset] >> 3) << 10) | ((image.rgba[offset + 1] >> 3) << 5) | (image.rgba[offset + 2] >> 3);
    counts[key] += 1;
    sums[0][key] += image.rgba[offset]; sums[1][key] += image.rgba[offset + 1]; sums[2][key] += image.rgba[offset + 2];
  }
  const entries = [];
  for (let key = 0; key < counts.length; key += 1) {
    if (!counts[key]) continue;
    entries.push({ key, count: counts[key], r: sums[0][key] / counts[key], g: sums[1][key] / counts[key], b: sums[2][key] / counts[key] });
  }
  const boxes = entries.length ? [makeColorBox(entries)] : [];
  while (boxes.length < 255) {
    let selected = -1, score = -1;
    for (let i = 0; i < boxes.length; i += 1) {
      if (boxes[i].entries.length < 2) continue;
      const current = boxes[i].population * (1 + Math.max(boxes[i].rangeR, boxes[i].rangeG, boxes[i].rangeB));
      if (current > score) { selected = i; score = current; }
    }
    if (selected < 0) break;
    const box = boxes[selected];
    const axis = box.rangeR >= box.rangeG && box.rangeR >= box.rangeB ? "r" : box.rangeG >= box.rangeB ? "g" : "b";
    box.entries.sort((a, b) => a[axis] - b[axis]);
    let sum = 0, split = 1;
    for (; split < box.entries.length; split += 1) { sum += box.entries[split - 1].count; if (sum >= box.population / 2) break; }
    split = Math.min(Math.max(split, 1), box.entries.length - 1);
    boxes.splice(selected, 1, makeColorBox(box.entries.slice(0, split)), makeColorBox(box.entries.slice(split)));
  }
  const palette = Buffer.alloc(768);
  const map = new Uint8Array(32768);
  for (let i = 0; i < boxes.length; i += 1) {
    const paletteIndex = i + 1;
    let r = 0, g = 0, b = 0;
    for (const entry of boxes[i].entries) {
      r += entry.r * entry.count; g += entry.g * entry.count; b += entry.b * entry.count;
      map[entry.key] = paletteIndex;
    }
    palette[paletteIndex * 3] = Math.round(r / boxes[i].population);
    palette[paletteIndex * 3 + 1] = Math.round(g / boxes[i].population);
    palette[paletteIndex * 3 + 2] = Math.round(b / boxes[i].population);
  }
  const indices = Buffer.alloc(image.width * image.height);
  for (let pixel = 0; pixel < indices.length; pixel += 1) {
    const offset = pixel * 4;
    if (image.rgba[offset + 3] < 96) continue;
    const key = ((image.rgba[offset] >> 3) << 10) | ((image.rgba[offset + 1] >> 3) << 5) | (image.rgba[offset + 2] >> 3);
    indices[pixel] = map[key];
  }
  return { palette, indices };
}

function lzwLiteralStream(indices) {
  const codes = [];
  for (let offset = 0; offset < indices.length; offset += 200) {
    codes.push(256);
    for (let i = offset; i < Math.min(offset + 200, indices.length); i += 1) codes.push(indices[i]);
  }
  codes.push(257);
  const packed = [];
  let accumulator = 0, bits = 0;
  for (const code of codes) {
    accumulator |= code << bits; bits += 9;
    while (bits >= 8) { packed.push(accumulator & 0xff); accumulator >>>= 8; bits -= 8; }
  }
  if (bits) packed.push(accumulator & 0xff);
  return Buffer.from(packed);
}

function subBlocks(data) {
  const blocks = [];
  for (let offset = 0; offset < data.length; offset += 255) {
    const block = data.subarray(offset, Math.min(offset + 255, data.length));
    blocks.push(Buffer.from([block.length]), block);
  }
  blocks.push(Buffer.from([0]));
  return Buffer.concat(blocks);
}

function writeGif(filePath, frames, delays) {
  const screen = Buffer.alloc(7);
  screen.writeUInt16LE(canvasSize, 0); screen.writeUInt16LE(canvasSize, 2); screen[4] = 0xf7;
  const loop = Buffer.from([0x21, 0xff, 0x0b, ...Buffer.from("NETSCAPE2.0"), 0x03, 0x01, 0, 0, 0]);
  const chunks = [Buffer.from("GIF89a"), screen, Buffer.alloc(768), loop];
  for (let i = 0; i < frames.length; i += 1) {
    const control = Buffer.from([0x21, 0xf9, 0x04, 0x09, delays[i] & 0xff, delays[i] >> 8, 0, 0]);
    const descriptor = Buffer.alloc(10);
    descriptor[0] = 0x2c; descriptor.writeUInt16LE(canvasSize, 5); descriptor.writeUInt16LE(canvasSize, 7); descriptor[9] = 0x87;
    const quantized = quantizeFrame(frames[i]);
    chunks.push(control, descriptor, quantized.palette, Buffer.from([8]), subBlocks(lzwLiteralStream(quantized.indices)));
  }
  chunks.push(Buffer.from([0x3b]));
  fs.writeFileSync(filePath, Buffer.concat(chunks));
}

function alphaBounds(image) {
  let left = image.width, top = image.height, right = -1, bottom = -1, pixels = 0;
  for (let y = 0; y < image.height; y += 1) {
    for (let x = 0; x < image.width; x += 1) {
      if (image.rgba[(y * image.width + x) * 4 + 3] < 16) continue;
      left = Math.min(left, x); top = Math.min(top, y); right = Math.max(right, x); bottom = Math.max(bottom, y); pixels += 1;
    }
  }
  return right < 0 ? null : { left, top, right, bottom, width: right - left + 1, height: bottom - top + 1, pixels };
}

fs.mkdirSync(frameDir, { recursive: true });
const frames = [];
for (let index = 1; index <= 8; index += 1) {
  const name = `frame_${String(index).padStart(2, "0")}.png`;
  const source = readPng(path.join(sourceDir, name));
  const checkerboardPixels = index === 4 ? removeConnectedCheckerboard(source) : 0;
  const repairedHoles = fillTinyAlphaHoles(source);
  let frame = resizeIntoSafeCanvas(source);
  if (index === 4) frame = scaleCanvasContent(frame, 1.3);
  writePng(path.join(frameDir, name), frame);
  frames.push(frame);
  console.log(JSON.stringify({ frame: name, source: `${source.width}x${source.height}`, checkerboardPixels, repairedHoles, bounds: alphaBounds(frame) }));
}
writeGif(gifPath, frames, [10, 9, 8, 8, 7, 18, 9, 55]);
console.log(`Wrote ${gifPath}`);
