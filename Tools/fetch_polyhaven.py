"""Poly Haven·ambientCG(CC0) HDRI·텍스처 받기 - 렌더링 템플릿 프리셋용.

받은 파일은 git에 안 올림(.gitignore). 다른 PC에서는 한 번 실행:
    python Tools/fetch_polyhaven.py
흐름: Poly Haven API에 파일 주소를 물어봄 -> Assets/RenderTemplate/ThirdParty/PolyHaven/ 에 저장 (있으면 건너뜀)
"""
import json, os, urllib.request

ROOT = os.path.join(os.path.dirname(__file__), "..", "Assets", "RenderTemplate", "ThirdParty", "PolyHaven")
UA = {"User-Agent": "cafesim-render/1.0"}  # User-Agent 없으면 403
HDRI = [("cayley_interior", "2k"),       # 다크 라운지 (따뜻한 실내)
        ("photo_studio_loft_hall", "2k")]  # 화이트 라운지: 밝고 부드러운 실내광
TEXTURES = [("painted_plaster_wall", "1k"),  # 회벽 노멀
            ("wood_table_001", "1k"),        # 원목 (Reference Matcher)
            ("white_oak_veneer", "1k")]      # 밝은 오크 하부장 (레퍼런스)
AMBIENTCG = ["Marble012"]                    # ambientCG(CC0) 흰 대리석 상판 (레퍼런스)
MAPS = ["Diffuse", "nor_gl", "Rough"]


def get(url):
    return urllib.request.urlopen(urllib.request.Request(url, headers=UA)).read()


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
    for name, res in TEXTURES:
        files = json.loads(get("https://api.polyhaven.com/files/" + name))
        folder = os.path.join(ROOT, "Textures", name)
        os.makedirs(folder, exist_ok=True)
        for m in MAPS:
            url = files[m][res]["jpg"]["url"]
            path = os.path.join(folder, url.split("/")[-1])
            if not os.path.exists(path):
                open(path, "wb").write(get(url))
                print(" ", name, m)


def ambientcg():
    """ambientCG(CC0) 재질: 1K-JPG zip을 받아 색·노멀(GL)·거칠기만 꺼냄"""
    import io, zipfile
    for name in AMBIENTCG:
        folder = os.path.join(ROOT, "..", "AmbientCG", name)
        if os.path.isdir(folder) and os.listdir(folder):
            continue
        os.makedirs(folder, exist_ok=True)
        data = get(f"https://ambientcg.com/get?file={name}_1K-JPG.zip")
        with zipfile.ZipFile(io.BytesIO(data)) as z:
            for f in z.namelist():
                if any(k in f for k in ("_Color.", "_NormalGL.", "_Roughness.")):
                    open(os.path.join(folder, os.path.basename(f)), "wb").write(z.read(f))
                    print(" ", f)


if __name__ == "__main__":
    main()
    ambientcg()
