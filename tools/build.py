#!/usr/bin/env python3
"""Single source of truth for built-in presets.
Writes themes/presets/*.json and injects the same presets into prototype/index.html."""
import json, re
from pathlib import Path

ROOT = Path(__file__).resolve().parent.parent

def preset(id, name, author, tags, accent, surface, surface_solid, tile, layout, alignment,
           height, icon, radius, blur, hover, icon_style, tools, open_anim, min_anim, dur, wallpaper,
           text="#F5F3FF", border="rgba(255,255,255,0.10)"):
    return {
        "$schema": "../../schema/theme.schema.json",
        "formatVersion": 1,
        "id": id,
        "name": name,
        "author": author,
        "version": "1.0.0",
        "tags": tags,
        "palette": {"accent": accent, "surface": surface, "surfaceSolid": surface_solid,
                    "text": text, "tile": tile, "border": border},
        "taskbar": {"layout": layout, "alignment": alignment, "height": height, "iconSize": icon,
                    "radius": radius, "blur": blur, "hoverEffect": hover, "iconStyle": icon_style,
                    "toolsGroup": tools},
        "clock": {"showSeconds": False, "showDate": True},
        "windows": {"open": open_anim, "minimize": min_anim, "durationMs": dur},
        "wallpaper": {"type": "procedural", "source": wallpaper},
        "performance": {"pauseOnFullscreen": True, "pauseOnBattery": True, "ecoMode": False},
    }

PRESETS = [
    preset("lavender-glass", "Лавандовое стекло", "Aura", ["стекло", "тёмная"],
           "#C9B8FF", "rgba(20,20,28,0.72)", "#16161D", "rgba(255,255,255,0.07)",
           "islands", "center", 60, 28, 18, 24, "dock", "tile", True, "scale", "genie", 300, "aurora"),
    preset("midnight", "Полночь", "Aura", ["минимализм", "тёмная"],
           "#E8E8EC", "rgba(8,8,10,0.82)", "#0B0B0D", "rgba(255,255,255,0.05)",
           "island", "center", 56, 26, 14, 18, "lift", "plain", True, "fade", "shrink", 220, "mono"),
    preset("sakura", "Сакура", "Aura", ["тёплая", "стекло"],
           "#FFB3C7", "rgba(40,18,28,0.66)", "#2A121C", "rgba(255,255,255,0.08)",
           "islands", "center", 62, 28, 22, 28, "dock", "tile", True, "slide", "genie", 340, "sunset"),
    preset("neon", "Неон", "Aura", ["киберпанк", "яркая"],
           "#5CF2FF", "rgba(6,14,24,0.74)", "#07111C", "rgba(92,242,255,0.07)",
           "islands", "left", 58, 26, 10, 20, "dock", "tile", True, "scale", "shrink", 240, "deep"),
    preset("classic-11", "Классика 11", "Aura", ["привычная"],
           "#7CC4FF", "rgba(30,30,34,0.85)", "#1E1E22", "rgba(255,255,255,0.06)",
           "full", "center", 52, 24, 0, 30, "lift", "plain", False, "scale", "shrink", 200, "bloom"),
    preset("forest", "Лес", "Aura", ["природа", "спокойная"],
           "#9BE3B0", "rgba(10,24,18,0.70)", "#0C1A14", "rgba(255,255,255,0.07)",
           "island", "center", 64, 30, 20, 22, "dock", "tile", True, "fade", "genie", 320, "forest"),
]

def main():
    out = ROOT / "themes" / "presets"
    out.mkdir(parents=True, exist_ok=True)
    for p in PRESETS:
        (out / f"{p['id']}.json").write_text(json.dumps(p, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
    tpl = (ROOT / "prototype" / "template.html").read_text(encoding="utf-8")
    data = json.dumps(PRESETS, ensure_ascii=False)
    assert "/*__PRESETS__*/[]" in tpl
    html = tpl.replace("/*__PRESETS__*/[]", data)
    (ROOT / "prototype" / "index.html").write_text(html, encoding="utf-8")
    print(f"wrote {len(PRESETS)} presets + prototype/index.html")

if __name__ == "__main__":
    main()
