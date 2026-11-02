"""Extrae las texturas de nemesis_re3.glb y el mapeo material -> textura.

El FBX exportado por Blender no trae las imágenes, así que ConfigurarEnemigos.cs
arma los materiales URP desde este mapeo.

Uso:
    python3 -I Tools/extraer_texturas_nemesis.py
"""
import json
import os
import struct

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(ROOT, "Assets/Prefabs/Personaje/nemesis/nemesis_re3.glb")
DST = os.path.join(ROOT, "Assets/Prefabs/Personaje/nemesis/Texturas")


def leer_glb(ruta):
    with open(ruta, "rb") as f:
        f.read(12)
        largo, _ = struct.unpack("<II", f.read(8))
        gltf = json.loads(f.read(largo))
        largo_bin, _ = struct.unpack("<II", f.read(8))
        return gltf, f.read(largo_bin)


def main():
    gltf, binario = leer_glb(SRC)
    os.makedirs(DST, exist_ok=True)
    imagenes = []
    for i, imagen in enumerate(gltf["images"]):
        vista = gltf["bufferViews"][imagen["bufferView"]]
        inicio = vista.get("byteOffset", 0)
        nombre = "nemesis_%d.png" % i
        with open(os.path.join(DST, nombre), "wb") as f:
            f.write(binario[inicio:inicio + vista["byteLength"]])
        imagenes.append(nombre)

    def imagen_de(referencia):
        if referencia is None:
            return ""
        return imagenes[gltf["textures"][referencia["index"]]["source"]]

    materiales = []
    for material in gltf["materials"]:
        pbr = material.get("pbrMetallicRoughness", {})
        materiales.append({
            "nombre": material["name"],
            "color": imagen_de(pbr.get("baseColorTexture")),
            "normal": imagen_de(material.get("normalTexture")),
            "emision": imagen_de(material.get("emissiveTexture")),
            "factor": pbr.get("baseColorFactor", [1, 1, 1, 1]),
        })
    with open(os.path.join(DST, "materiales.json"), "w", encoding="utf-8") as f:
        json.dump({"materiales": materiales}, f, indent=2)

    sin_color = [m["nombre"] for m in materiales if not m["color"] and m["factor"] == [1, 1, 1, 1]]
    assert not sin_color, "Materiales que quedarían blancos: %s" % sin_color
    print("imagenes=%d materiales=%d destino=%s" % (len(imagenes), len(materiales), DST))


if __name__ == "__main__":
    main()
