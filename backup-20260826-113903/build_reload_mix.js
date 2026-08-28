const fs = require('fs');

const INPUTS = [
  { path: 'C:/Users/Loadcomplete/Downloads/clipload2 (1).wav', gain: 1.18 },
  { path: 'C:/Users/Loadcomplete/Downloads/clipload1 (1).wav', gain: 0.64 },
  { path: 'C:/Users/Loadcomplete/Downloads/singlebullet1 (1).wav', gain: 1.48 },
];
const OUTPUT = 'tunnel-crew-reload-mix.wav';
const SAMPLE_RATE = 44100;
const TARGET_SECONDS = 1.35;
const START_SECONDS = [0.05, 0.44, 1.00];
const CROSSFADE = Math.round(SAMPLE_RATE * 0.025);
const THRESHOLD = 260;

function readWav(filePath) {
  const file = fs.readFileSync(filePath);
  if (file.toString('ascii', 0, 4) !== 'RIFF' || file.toString('ascii', 8, 12) !== 'WAVE') {
    throw new Error(`Not a WAV file: ${filePath}`);
  }
  const channels = file.readUInt16LE(22);
  const sampleRate = file.readUInt32LE(24);
  const bits = file.readUInt16LE(34);
  if (channels !== 1 || sampleRate !== SAMPLE_RATE || bits !== 16) {
    throw new Error(`Expected 44.1kHz/16-bit mono WAV: ${filePath}`);
  }
  let offset = 12;
  let dataOffset = -1;
  let dataBytes = 0;
  while (offset + 8 <= file.length) {
    const chunk = file.toString('ascii', offset, offset + 4);
    const size = file.readUInt32LE(offset + 4);
    if (chunk === 'data') {
      dataOffset = offset + 8;
      dataBytes = size;
      break;
    }
    offset += 8 + size + (size & 1);
  }
  if (dataOffset < 0) throw new Error(`Missing data chunk: ${filePath}`);
  const samples = new Float32Array(dataBytes / 2);
  for (let i = 0; i < samples.length; i++) samples[i] = file.readInt16LE(dataOffset + i * 2) / 32768;

  let first = 0;
  while (first < samples.length && Math.abs(samples[first]) * 32768 < THRESHOLD) first++;
  let last = samples.length - 1;
  while (last >= first && Math.abs(samples[last]) * 32768 < THRESHOLD) last--;
  return samples.slice(Math.max(0, first - 220), Math.min(samples.length, last + 221));
}

function putWithCrossfade(destination, source, start, gain) {
  for (let i = 0; i < source.length; i++) {
    const at = start + i;
    if (at < 0 || at >= destination.length) continue;
    let envelope = 1;
    if (i < CROSSFADE && start > 0) envelope *= i / CROSSFADE;
    const tail = source.length - 1 - i;
    if (tail < CROSSFADE) envelope *= tail / CROSSFADE;
    destination[at] += source[i] * gain * envelope;
  }
}

function makeImpact(source) {
  let peakAt = 0;
  for (let i = 1; i < source.length; i++) {
    if (Math.abs(source[i]) > Math.abs(source[peakAt])) peakAt = i;
  }
  const before = Math.round(SAMPLE_RATE * 0.010);
  const length = Math.round(SAMPLE_RATE * 0.105);
  const impact = new Float32Array(length);
  const start = Math.max(0, peakAt - before);
  for (let i = 0; i < impact.length; i++) {
    const sourceAt = Math.min(source.length - 1, start + i);
    const attack = Math.min(1, i / Math.max(1, Math.round(SAMPLE_RATE * 0.002)));
    const release = impact.length > 1 ? Math.max(0, 1 - i / (impact.length - 1)) : 0;
    impact[i] = source[sourceAt] * attack * release;
  }
  return impact;
}

const clips = INPUTS.map(item => ({ ...item, samples: readWav(item.path) }));
const starts = START_SECONDS.map(seconds => Math.round(seconds * SAMPLE_RATE));
const outputLength = Math.round(TARGET_SECONDS * SAMPLE_RATE);
const mixed = new Float32Array(outputLength);
clips.forEach((clip, i) => putWithCrossfade(mixed, clip.samples, starts[i], clip.gain));
const impact = makeImpact(clips[1].samples);
const impactStart = outputLength - impact.length;
for (let i = 0; i < impact.length; i++) mixed[impactStart + i] += impact[i] * 0.82;

let peak = 0;
for (const sample of mixed) peak = Math.max(peak, Math.abs(sample));
const master = peak > 0 ? Math.min(1, 0.92 / peak) : 1;
const dataBytes = mixed.length * 2;
const wav = Buffer.alloc(44 + dataBytes);
wav.write('RIFF', 0, 'ascii');
wav.writeUInt32LE(36 + dataBytes, 4);
wav.write('WAVE', 8, 'ascii');
wav.write('fmt ', 12, 'ascii');
wav.writeUInt32LE(16, 16);
wav.writeUInt16LE(1, 20);
wav.writeUInt16LE(1, 22);
wav.writeUInt32LE(SAMPLE_RATE, 24);
wav.writeUInt32LE(SAMPLE_RATE * 2, 28);
wav.writeUInt16LE(2, 32);
wav.writeUInt16LE(16, 34);
wav.write('data', 36, 'ascii');
wav.writeUInt32LE(dataBytes, 40);
for (let i = 0; i < mixed.length; i++) {
  const value = Math.max(-1, Math.min(1, mixed[i] * master));
  wav.writeInt16LE(Math.round(value * 32767), 44 + i * 2);
}
fs.writeFileSync(OUTPUT, wav);
console.log(JSON.stringify({ output: OUTPUT, duration: +(mixed.length / SAMPLE_RATE).toFixed(3), starts: START_SECONDS, peak: +peak.toFixed(4), master: +master.toFixed(4) }));
