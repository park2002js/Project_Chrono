"""원본 세피리아 bank에서 확정한6개 표본만 PCM WAV로 추출"""
import argparse
import array
import hashlib
import json
import math
import os
from pathlib import Path
import re
import struct
import subprocess
import time
import uuid
import wave

SOURCE_HASHES = {
    "Master.bank": "24f8cb016d55f9d434ef174b4239598a5fed48edc38a1a4f4f411c767d32ba9c",
    "Master.strings.bank": "b14a6518226947fa883836ab0d532c514d720d48e36502f212740425d4be3b9e",
}
SELECTIONS = [
    ("AttackSwingA", "attackSwish07", "event:/Scene/attackSwish07", "원작 기본 대검1·2타의 실제 공통 스윙"),
    ("AttackSwingB", "attackSwish06", "event:/Scene/attackSwish06", "사용자 요청의 다른2타 소리를 위해 같은 swish군의 별도 표본 선택"),
    ("AttackHit", "hitSlash01", "event:/Scene/CrashSlice", "원작 대검 성공 적중 이벤트에 실제 포함된 베기 적중 표본1개 선택"),
    ("FootstepTile01", "footstepRoad01", "event:/Scene/footstepRoad", "현재 타일 바닥용으로 선택한 원작 road 발소리 첫 표본"),
    ("FootstepTile02", "footstepRoad02", "event:/Scene/footstepRoad", "같은 road 이벤트의 두 번째 변형"),
    ("FootstepTile03", "footstepRoad03", "event:/Scene/footstepRoad", "같은 road 이벤트의 세 번째 변형"),
]


def sha(data):
    return hashlib.sha256(data).hexdigest()


def create_meta(path, folder=False):
    # 이미 연결된 GUID는 보존하고 신규 파일의 importer만 최초 생성
    meta = Path(str(path) + ".meta")
    if meta.exists():
        return re.search(r"^guid: ([a-f0-9]+)$", meta.read_text(encoding="utf-8"), re.M)[1]
    guid = uuid.uuid4().hex
    if folder:
        text = f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n"
    else:
        text = f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 2
    sampleRateOverride: 48000
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 1
  userData: 
  assetBundleName: 
  assetBundleVariant: 
"""
    try:
        with meta.open("x", encoding="utf-8", newline="\n") as stream:
            stream.write(text)
    except FileExistsError:
        return create_meta(path, folder)
    return guid


def inspect_wave(path):
    # 실제 PCM 구조·표본 수·무음 여부·레벨을 확인하고 청감 평가는 별도로 구분
    with wave.open(str(path), "rb") as source:
        rate, channels, width, frames = source.getframerate(), source.getnchannels(), source.getsampwidth(), source.getnframes()
        assert source.getcomptype() == "NONE" and width == 2
        pcm = source.readframes(frames)
    values = array.array("h", pcm)
    peak = max(abs(value) for value in values)
    rms = math.sqrt(sum(value * value for value in values) / len(values))
    assert peak > 0 and len(pcm) == frames * channels * width
    return {"sampleRate": rate, "channels": channels, "bits": width * 8, "frames": frames,
            "durationSeconds": frames / rate, "pcmSha256": sha(pcm), "wavSha256": sha(path.read_bytes()),
            "peak": peak / 32768, "rmsDbFs": 20 * math.log10(rms / 32768),
            "fullScaleSampleCount": sum(abs(value) >= 32767 for value in values)}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--workspace", type=Path, default=Path(__file__).resolve().parents[4])
    parser.add_argument("--decoder", type=Path, required=True)
    parser.add_argument("--playback-check", action="store_true")
    args = parser.parse_args()
    root = args.workspace.resolve()
    archive = root / "art/archive/audio/player-feedback-r01"
    work = root / "art/audio/sephiria-sfx"
    unity = root / "project/Assets/Audio/PlayerFeedback"
    source = root / "docs/reference/sephiria/assets/ExportedProject/Assets/StreamingAssets"
    archive.mkdir(parents=True, exist_ok=True)
    work.mkdir(parents=True, exist_ok=True)
    unity.mkdir(parents=True, exist_ok=True)
    create_meta(unity, folder=True)

    # 사용자 제공 원본의 고정 해시와 유일한 FSB5 구간 검증
    originals = {name: (source / name).read_bytes() for name in SOURCE_HASHES}
    for name, expected in SOURCE_HASHES.items():
        assert sha(originals[name]) == expected, f"Changed original: {name}"
    bank = originals["Master.bank"]
    offset = bank.index(b"FSB5")
    assert bank.find(b"FSB5", offset + 4) == -1
    version, count, headers, names_size, data_size, codec = struct.unpack_from("<6I", bank, offset + 4)
    assert (version, count, codec) == (1, 720, 15)
    name_base = offset + 60 + headers
    name_offsets = struct.unpack_from("<" + str(count) + "I", bank, name_base)
    names = [bank[name_base + index:bank.index(0, name_base + index)].decode("utf-8") for index in name_offsets]
    fsb_bytes = bank[offset:offset + 60 + headers + names_size + data_size]
    assert len(fsb_bytes) == 60 + headers + names_size + data_size
    fsb = work / "Master.fsb"
    if not fsb.exists() or sha(fsb.read_bytes()) != sha(fsb_bytes):
        fsb.write_bytes(fsb_bytes)
    event_proof = json.loads((archive / "source-events.json").read_text(encoding="utf-8"))
    events = {entry["path"]: entry for entry in event_proof["events"]}

    # 원본 이벤트에 실제 연결된6개 표본만 선택하고 게인·피치·길이 변경 없이 디코딩
    outputs = []
    for name, sample, event_path, reason in SELECTIONS:
        assert sample in events[event_path]["samples"]
        subsong = names.index(sample) + 1
        command = [str(args.decoder.resolve()), "-i", "-s", str(subsong), "-o", str(archive / (name + ".wav")), str(fsb)]
        result = subprocess.run(command, capture_output=True, text=True, check=True)
        (work / (name + "-decode.log")).write_text(result.stdout + result.stderr, encoding="utf-8")
        metadata = json.loads(subprocess.run([str(args.decoder.resolve()), "-I", "-s", str(subsong), str(fsb)], capture_output=True, text=True, check=True).stdout)
        wav_path = archive / (name + ".wav")
        metrics = inspect_wave(wav_path)
        assert metrics["frames"] == metadata["numberOfSamples"] and metrics["sampleRate"] == metadata["sampleRate"]
        assert metadata["streamInfo"]["name"] == sample
        target = unity / wav_path.name
        target.write_bytes(wav_path.read_bytes())
        guid = create_meta(target)
        assert target.read_bytes() == wav_path.read_bytes()
        outputs.append({"file": name + ".wav", "unityPath": str(target.relative_to(root)).replace("\\", "/"), "unityGuid": guid,
                        "sourceSample": sample, "sourceSubsongOneBased": subsong, "sourceEventPath": event_path,
                        "sourceEventGuid": events[event_path]["guid"], "selectionReason": reason, **metrics})

    # Windows 재생 API의 실제 완료만 기록하며 스피커 청감 확인으로 확대하지 않음
    playback = {"requested": args.playback_check, "apiCompleted": False, "subjectiveListeningVerified": False}
    if args.playback_check:
        assert os.name == "nt", "Playback check requires Windows winsound"
        import winsound
        started = time.perf_counter()
        for output in outputs:
            winsound.PlaySound(str(archive / output["file"]), winsound.SND_FILENAME | winsound.SND_NODEFAULT)
        playback.update(apiCompleted=True, seconds=time.perf_counter() - started)

    # 추출 후 원본 불변과 양쪽 산출물의 바이트 일치를 최종 확인
    for name, expected in SOURCE_HASHES.items():
        assert sha((source / name).read_bytes()) == expected
    manifest = {"createdDate": "2026-11-16", "sourceDirectory": str(source.relative_to(root)).replace("\\", "/"),
                "sourceHashesBeforeAndAfter": SOURCE_HASHES, "sourceFilesWritten": False,
                "fsb": {"offset": offset, "sampleCount": count, "sha256": sha(fsb_bytes), "codec": "FSB5 Custom Vorbis"},
                "decoder": {"release": "r2168", "url": "https://github.com/vgmstream/vgmstream/releases/tag/r2168", "exeSha256": sha(args.decoder.read_bytes()),
                            "releaseZipSha256": "692d45070852a35c1fa42726aa3c9de528e44eec925d37d99b1074256b99a229"},
                "processing": "Full original samples decoded to PCM16 WAV; no trim, resample, normalization, pitch or time change",
                "unityImport": "PCM/decompress-on-load,48kHz,stereo,preload; existing meta GUIDs preserved",
                "eventMapping": event_proof["parser"], "outputs": outputs, "playbackCheck": playback,
                "limitations": ["Extracted dry samples omit FMOD event gain, random pitch,3D and Material routing", "Road event samples are selected for our tile floor; original tile-to-event callsite not confirmed", "Distinct combo2 sound is our selection; original basic combo1/2 share attackSwish07", "Unity import and gameplay playback are verified separately"]}
    (archive / "manifest.json").write_text(json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"outputs": outputs, "playbackCheck": playback, "originalsUnchanged": True}, ensure_ascii=True))


if __name__ == "__main__":
    main()
