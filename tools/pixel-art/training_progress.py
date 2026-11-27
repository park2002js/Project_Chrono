"""Display sd-scripts --run_control telemetry without owning the trainer process."""
import argparse
import datetime
import json
import os
import re
import time
import tkinter as tk
from pathlib import Path
from tkinter import ttk


def tail(path):
    try:
        with path.open("rb") as stream:
            stream.seek(max(0, stream.seek(0, 2) - 8192))
            text = stream.read().decode("utf-8-sig", errors="replace")
        lines = re.sub(r"\x1b\[[0-9;?]*[a-zA-Z]", "", text).splitlines()
        return next((line.strip()[-180:] for line in reversed(lines) if line.strip()), "")
    except OSError:
        return ""


def duration(seconds):
    if seconds is None:
        return "계산 중"
    hours, rest = divmod(max(0, int(seconds)), 3600)
    minutes, seconds = divmod(rest, 60)
    return f"{hours}:{minutes:02}:{seconds:02}"


def monitor(output, name, steps):
    run = output / (name + "_run")
    log = output / (name + ".log")
    exit_file = output / (name + ".exit")
    weights = output / (name + ".safetensors")
    clock_start = time.time()
    root = tk.Tk()
    root.title(f"LoRA 학습 진행 · {name}")
    root.minsize(600, 300)
    root.attributes("-topmost", True)
    root.after(4500, lambda: root.attributes("-topmost", False))
    pane = ttk.Frame(root, padding=22)
    pane.pack(fill="both", expand=True)

    def label(size=10, bold=False):
        field = ttk.Label(pane, wraplength=565, justify="left",
                          font=("맑은 고딕", size, "bold" if bold else "normal"))
        field.pack(anchor="w", pady=3)
        return field

    headline = label(16, True)
    counts = label(12)
    bar = ttk.Progressbar(pane, mode="determinate", maximum=steps)
    bar.pack(fill="x", pady=8)
    details, timing, heartbeat, latest = label(), label(), label(), label()
    ttk.Button(pane, text="출력 폴더 열기", command=lambda: os.startfile(output)).pack(anchor="e", pady=6)
    states = {"starting": "훈련 워커 준비 중", "running": "학습 중", "paused": "일시 정지",
              "finished": "가중치 저장 확인 중", "failed": "학습 실패", "interrupted": "학습 중단",
              "aborted": "학습 중단", "stopped": "학습 중지"}
    last_status = {}

    def refresh():
        nonlocal last_status
        now = time.time()
        try:
            last_status = json.loads((run / "status.json").read_text(encoding="utf-8-sig"))
        except (OSError, ValueError):
            pass
        try:
            exit_code = int(exit_file.read_text(encoding="utf-8-sig").strip())
        except (OSError, ValueError):
            exit_code = None
        line = tail(log)
        progress = last_status.get("progress", {})
        step = progress.get("global_step", 0)
        total = progress.get("max_train_steps") or steps
        started = last_status.get("started_at", clock_start)
        eta = last_status.get("speed", {}).get("eta_sec")
        if step >= total or (exit_code == 0 and weights.is_file()):
            eta = 0
        elif eta is None and step >= 10 and last_status.get("state") == "running":
            measured = last_status.get("updated_at", started) - started
            if measured > 0:
                eta = (total - step) * measured / step
        loss = last_status.get("loss", {}).get("average")
        stage = states.get(last_status.get("state"), "학습 모델과 데이터 준비 중" if log.exists() else "학습 데이터 준비 중")
        if exit_code is not None:
            if exit_code == 0 and weights.is_file():
                stage = "학습 완료 · LoRA 저장됨"
            else:
                stage = f"실행 종료 · 로그 확인 (종료 코드 {exit_code})"
        headline.config(text=stage)
        counts.config(text=f"{step:,} / {total:,} 스텝   ·   {100 * step / total:.1f}%")
        bar.config(maximum=total, value=step)
        epoch = progress.get("epoch", 0)
        details.config(text=f"에포크 {epoch}   ·   평균 손실 {loss:.5f}" if isinstance(loss, (int, float)) else "첫 스텝의 손실을 기다리는 중")
        end = last_status.get("updated_at", now) if exit_code is not None else now
        timing.config(text=f"경과 {duration(end - started)}   ·   예상 남은 시간 {duration(eta)}")
        observed = last_status.get("updated_at")
        if not observed and log.exists():
            try:
                observed = log.stat().st_mtime
            except OSError:
                pass
        checked = datetime.datetime.now().strftime("%H:%M:%S")
        age = f"   ·   훈련 기록 {max(0, int(now - observed))}초 전" if observed else ""
        heartbeat.config(text=f"화면 확인 {checked}{age}")
        latest.config(text=line)
        if exit_code is None:
            root.after(1500, refresh)

    refresh()
    root.mainloop()


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("output", type=Path)
    parser.add_argument("name")
    parser.add_argument("steps", type=int)
    args = parser.parse_args()
    monitor(args.output, args.name, args.steps)
