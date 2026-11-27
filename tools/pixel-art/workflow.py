"""Create an editable ComfyUI workflow, optionally run it on the local server."""
import argparse
import json
import time
from pathlib import Path
from urllib.error import HTTPError
from urllib.request import Request, urlopen


DEFAULT_PROMPT = (
    "a small clockwork beetle knight, front view, two antennae, teal enamel shell, "
    "one orange glowing eye, two short legs, standing at rest"
)
BOSS_SLOT = "<boss_concept>"
SPRITE_TEMPLATE = (f"pixel art, {BOSS_SLOT}, one complete game boss, centered full body, simple chunky shading, "
                   "strong readable silhouette, broad connected color patches, short contact shadows, "
                   "isolated on a flat pure white background")
NEGATIVE = ("photograph, illustration, gradients, blurry, text, letters, watermark, "
            "ground, floor, scenery, cast shadow, cropped, sprite sheet, multiple characters")


def build(args):
    nodes, links, prompt = [], [], {}

    def node(kind, title, pos, size, sockets, outputs, widgets=None, values=None):
        ident = len(nodes) + 1
        node_inputs = []
        api_inputs = dict(widgets or {})
        for slot, (name, dtype, connection) in enumerate(sockets):
            link_id = None
            if connection is not None:
                source, pin = connection
                link_id = len(links) + 1
                links.append([link_id, source, pin, ident, slot, dtype])
                nodes[source - 1]["outputs"][pin]["links"].append(link_id)
                api_inputs[name] = [str(source), pin]
            node_inputs.append({"name": name, "type": dtype, "link": link_id})
        prompt[str(ident)] = {"class_type": kind, "inputs": api_inputs}
        nodes.append({
            "id": ident, "type": kind, "pos": pos, "size": size, "title": title,
            "flags": {}, "order": ident - 1, "mode": 0, "inputs": node_inputs,
            "outputs": [{"name": name, "type": dtype, "links": [], "slot_index": i}
                        for i, (name, dtype) in enumerate(outputs)],
            "properties": {"Node name for S&R": kind},
            "widgets_values": list((widgets or {}).values()) if values is None else values,
        })
        return ident

    ck = node("CheckpointLoaderSimple", "기반 모델 · SDXL", [0, 0], [320, 106], [],
              [("MODEL", "MODEL"), ("CLIP", "CLIP"), ("VAE", "VAE")],
              {"ckpt_name": "sd_xl_base_1.0.safetensors"})
    adapter = node("LoraLoader", "스타일 LoRA · 강도 0은 끔", [350, 0], [340, 128],
                   [("model", "MODEL", (ck, 0)), ("clip", "CLIP", (ck, 1))],
                   [("MODEL", "MODEL"), ("CLIP", "CLIP")],
                   {"lora_name": args.lora, "strength_model": args.strength, "strength_clip": args.strength})
    template = SPRITE_TEMPLATE
    if Path(args.lora).name == "sephiria-boss-v1.safetensors":
        template = "chrono_boss, " + template
    concept = node("StringReplace", "보스 콘셉트 + 기본 화풍", [730, -305], [440, 250],
                   [], [("STRING", "STRING")], {"string": template, "find": BOSS_SLOT, "replace": args.prompt})
    positive = node("CLIPTextEncode", "콘셉트와 화풍 인코딩", [730, 0], [440, 115],
                    [("clip", "CLIP", (adapter, 1)), ("text", "STRING", (concept, 0))],
                    [("CONDITIONING", "CONDITIONING")], values=[""])
    nodes[positive - 1]["inputs"][1]["widget"] = {"name": "text"}
    negative = node("CLIPTextEncode", "제외할 요소", [730, 210], [440, 115],
                    [("clip", "CLIP", (adapter, 1))], [("CONDITIONING", "CONDITIONING")],
                    {"text": NEGATIVE})
    if args.reference:
        rough = node("LoadImage", "첨부 · 러프 또는 원화", [0, 220], [310, 315], [],
                     [("IMAGE", "IMAGE"), ("MASK", "MASK")], {"image": args.reference},
                     [args.reference, "image"])
        prep = node("ChronoReferenceCanvas", "참고 이미지 · 투명 여백을 흰색으로", [350, 265], [335, 90],
                    [("image", "IMAGE", (rough, 0)), ("transparent", "MASK", (rough, 1))],
                    [("IMAGE", "IMAGE")])
        latent = node("VAEEncode", "참고 이미지 인코딩", [730, 400], [250, 75],
                      [("pixels", "IMAGE", (prep, 0)), ("vae", "VAE", (ck, 2))], [("LATENT", "LATENT")])
    else:
        latent = node("EmptyLatentImage", "생성 해상도", [730, 385], [270, 110],
                      [], [("LATENT", "LATENT")], {"width": 768, "height": 768, "batch_size": 1})
    model = adapter
    if args.appearance:
        look_images = []
        for idx, filename in enumerate(args.appearance):
            y = 600 + 360 * idx
            ref = node("LoadImage", f"외형 참고 {idx + 1}", [0, y], [300, 300], [],
                       [("IMAGE", "IMAGE"), ("MASK", "MASK")], {"image": filename}, [filename, "image"])
            look_images.append(node("ChronoReferenceCanvas", "참고 크기 통일", [350, y], [275, 75],
                               [("image", "IMAGE", (ref, 0)), ("transparent", "MASK", (ref, 1))], [("IMAGE", "IMAGE")]))
        appearance = look_images[0]
        if len(look_images) == 2:
            appearance = node("ImageBatch", "외형 참고 두 장", [660, 800], [220, 65],
                              [("image1", "IMAGE", (appearance, 0)), ("image2", "IMAGE", (look_images[1], 0))],
                              [("IMAGE", "IMAGE")])
        loader = node("IPAdapterUnifiedLoader", "외형 참고 모델 · SDXL Plus", [675, 570], [315, 85],
                      [("model", "MODEL", (adapter, 0)), ("ipadapter", "IPADAPTER", None)],
                      [("MODEL", "MODEL"), ("ipadapter", "IPADAPTER")], {"preset": "PLUS (high strength)"})
        model = node("IPAdapterAdvanced", "참고 외형 반영 · weight", [1030, 575], [335, 270],
                     [("model", "MODEL", (loader, 0)), ("ipadapter", "IPADAPTER", (loader, 1)),
                      ("image", "IMAGE", (appearance, 0)), ("image_negative", "IMAGE", None),
                      ("attn_mask", "MASK", None), ("clip_vision", "CLIP_VISION", None)],
                     [("MODEL", "MODEL")], {"weight": 0.5, "weight_type": "linear", "combine_embeds": "average",
                     "start_at": 0.0, "end_at": 0.8, "embeds_scaling": "V only"})
    denoise = args.denoise if args.denoise is not None else (0.55 if args.reference else 1.0)
    sampler = node("KSampler", "생성 · 시드와 변화 강도", [1220, 0], [300, 295],
                   [("model", "MODEL", (model, 0)), ("positive", "CONDITIONING", (positive, 0)),
                    ("negative", "CONDITIONING", (negative, 0)), ("latent_image", "LATENT", (latent, 0))],
                   [("LATENT", "LATENT")], {"seed": args.seed, "steps": args.steps, "cfg": 6.0,
                   "sampler_name": "euler", "scheduler": "normal", "denoise": denoise},
                   [args.seed, "fixed", args.steps, 6.0, "euler", "normal", denoise])
    decode = node("VAEDecode", "생성 원본", [1570, 0], [205, 75],
                  [("samples", "LATENT", (sampler, 0)), ("vae", "VAE", (ck, 2))], [("IMAGE", "IMAGE")])
    guides = []
    for index, (filename, title) in enumerate([(args.silhouette, "실루엣 · 흰 몸 / 검정 배경"),
                                                (args.palette, "팔레트 · 불투명 색상표")]):
        if filename:
            ident = node("LoadImage", title, [1570, 175 + 345 * index], [280, 305], [],
                         [("IMAGE", "IMAGE"), ("MASK", "MASK")], {"image": filename}, [filename, "image"])
            guides.append((ident, 0))
        else:
            guides.append(None)
    grid = node("ChronoSpriteGrid", "최종 PNG · 가로 / 세로 / 최대 색 수", [1880, 0], [350, 195],
                [("image", "IMAGE", (decode, 0)), ("silhouette", "IMAGE", guides[0]),
                 ("palette", "IMAGE", guides[1])], [("IMAGE", "IMAGE")],
                {"width": args.width, "height": args.height, "max_colors": args.colors,
                 "background_tolerance": args.tolerance})
    saved = node("SaveImage", "투명 픽셀 PNG", [2280, 0], [300, 310],
                 [("images", "IMAGE", (grid, 0))], [], {"filename_prefix": "Chrono/" + args.name})
    node("SaveImage", "생성 원본 · 격자 변환 전", [1880, 260], [315, 320],
         [("images", "IMAGE", (decode, 0))], [], {"filename_prefix": "Chrono/raw/" + args.name})
    inputs = [[concept, "replace", {"description": "보스 콘셉트 · 영어로 생김새·방향·자세·장비를 적으세요. 기본 화풍은 자동으로 붙습니다."}]]
    inputs.extend([n["id"], "image", {"description": n["title"]}] for n in nodes if n["type"] == "LoadImage")
    inputs.extend([[grid, "width"], [grid, "height"], [grid, "max_colors"], [sampler, "seed"]])
    if not args.silhouette:
        inputs.append([grid, "background_tolerance"])
    if args.appearance:
        inputs.append([model, "weight"])
    if args.reference:
        inputs.append([sampler, "denoise"])
    inputs.extend([[adapter, "lora_name"], [adapter, "strength_model"], [adapter, "strength_clip"]])
    extra = {"ds": {"scale": 0.70, "offset": [40, 120]}, "linearMode": True,
             "linearData": {"inputs": inputs, "outputs": [str(saved)]}}
    ui = {"last_node_id": len(nodes), "last_link_id": len(links), "nodes": nodes, "links": links,
          "groups": [], "config": {}, "extra": extra, "version": 0.4}
    return prompt, ui


def query(path, data=None):
    url = "http://127.0.0.1:8188" + path
    body = json.dumps(data).encode() if data is not None else None
    try:
        with urlopen(Request(url, data=body, headers={"Content-Type": "application/json"}), timeout=30) as response:
            return json.load(response)
    except HTTPError as err:
        raise RuntimeError(err.read().decode()) from err


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--save-workflow", type=Path)
    parser.add_argument("--run", action="store_true")
    parser.add_argument("--prompt", default=DEFAULT_PROMPT)
    parser.add_argument("--reference", help="Image filename already in ComfyUI/input")
    parser.add_argument("--appearance", nargs="+", help="One or two ComfyUI input images for appearance conditioning")
    parser.add_argument("--silhouette", help="Native-size white-on-black mask in ComfyUI/input")
    parser.add_argument("--palette", help="Opaque RGB swatches in ComfyUI/input")
    parser.add_argument("--lora", default="pixel-art-xl.safetensors")
    parser.add_argument("--strength", type=float, default=1)
    parser.add_argument("--seed", type=int, default=2311)
    parser.add_argument("--steps", type=int, default=20)
    parser.add_argument("--denoise", type=float)
    parser.add_argument("--width", type=int, default=48)
    parser.add_argument("--height", type=int, default=48)
    parser.add_argument("--colors", type=int, default=18)
    parser.add_argument("--tolerance", type=int, default=104)
    parser.add_argument("--name", default="boss")
    args = parser.parse_args()
    if args.appearance and len(args.appearance) > 2:
        parser.error("Select one or two appearance references.")
    if not args.run and not args.save_workflow:
        parser.error("Specify --run or --save-workflow.")
    prompt, ui = build(args)
    if args.save_workflow:
        args.save_workflow.parent.mkdir(parents=True, exist_ok=True)
        args.save_workflow.write_text(json.dumps(ui, ensure_ascii=False, indent=2), encoding="utf-8")
    if args.run:
        job = query("/prompt", {"prompt": prompt, "extra_data": {"extra_pnginfo": {"workflow": ui}}})["prompt_id"]
        print("Queued", job, flush=True)
        while True:
            record = query("/history/" + job).get(job)
            if record:
                if record["status"]["status_str"] != "success":
                    raise RuntimeError(json.dumps(record["status"], ensure_ascii=True))
                print(json.dumps(record["outputs"], ensure_ascii=True), flush=True)
                return
            time.sleep(2)


if __name__ == "__main__":
    main()
