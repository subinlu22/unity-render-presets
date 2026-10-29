"""Poly Haven(CC0) HDRI 받기 - 렌더링 템플릿 프리셋용.

받은 파일은 git에 안 올림(.gitignore). 다른 PC에서는 한 번 실행:
    python Tools/fetch_polyhaven.py
흐름: Poly Haven API에 파일 주소를 물어봄 -> Assets/RenderTemplate/ThirdParty/PolyHaven/ 에 저장 (있으면 건너뜀)
"""
import json, os, urllib.request

ROOT = os.path.join(os.path.dirname(__file__), "..", "Assets", "RenderTemplate", "ThirdParty", "PolyHaven")
UA = {"User-Agent": "cafesim-render/1.0"}  # User-Agent 없으면 403
HDRI = [("studio_small_09", "1k"),       # 스튜디오 프리셋
        ("brown_photostudio_02", "1k")]  # 카페 무드 프리셋 (따뜻한 갈색 톤)


def main():
    os.makedirs(ROOT, exist_ok=True)
    for name, res in HDRI:
        path = os.path.join(ROOT, f"{name}_{res}.hdr")
        if os.path.exists(path):
            continue
        info = json.load(urllib.request.urlopen(urllib.request.Request("https://api.polyhaven.com/files/" + name, headers=UA)))
        data = urllib.request.urlopen(urllib.request.Request(info["hdri"][res]["hdr"]["url"], headers=UA)).read()
        open(path, "wb").write(data)
        print(name, round(len(data) / 1e6, 2), "MB")


if __name__ == "__main__":
    main()
