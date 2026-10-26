"""Convierte nemesis_re3.glb a FBX con huesos Bip01 en metros, sin escalas intermedias.

Sketchfab deja la escala de 100 en un empty y los huesos chicos. Mecanim no tolera
esa escala entre la raíz y las caderas. Aquí se hornea en la longitud de los
huesos y el cuerpo queda a 2,6 m.

Uso:
    blender --background --python Tools/convertir_nemesis.py
"""
import os
import re
import bpy

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), ".."))
SRC = os.path.join(ROOT, "Assets/Prefabs/Personaje/nemesis/nemesis_re3.glb")
DST = os.path.join(ROOT, "Assets/Prefabs/Personaje/nemesis/Nemesis.fbx")
PATRON = re.compile(r"^ValveBiped\.(.+?)(?:_\d+){2}$")
ALTURA_OBJETIVO = 2.6


def renombrar_huesos():
    total = 0
    for obj in bpy.data.objects:
        if obj.type != "ARMATURE":
            continue
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.mode_set(mode="EDIT")
        for bone in obj.data.edit_bones:
            coincidencia = PATRON.match(bone.name)
            if coincidencia is None:
                continue
            bone.name = coincidencia.group(1)
            total += 1
        bpy.ops.object.mode_set(mode="OBJECT")
    return total


def quitar_esfera_de_preview():
    quitados = 0
    for obj in list(bpy.data.objects):
        if obj.type == "MESH" and obj.parent is None:
            bpy.data.objects.remove(obj, do_unlink=True)
            quitados += 1
    return quitados


def seleccionar(objetos):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in objetos:
        obj.select_set(True)
    if objetos:
        bpy.context.view_layer.objects.active = objetos[0]


def altura_deformada():
    bpy.context.view_layer.update()
    deps = bpy.context.evaluated_depsgraph_get()
    minimo = 1e9
    maximo = -1e9
    for obj in bpy.data.objects:
        if obj.type != "MESH":
            continue
        evaluado = obj.evaluated_get(deps)
        malla = evaluado.to_mesh()
        for vertice in malla.vertices:
            mundo = evaluado.matrix_world @ vertice.co
            minimo = min(minimo, mundo.z)
            maximo = max(maximo, mundo.z)
        evaluado.to_mesh_clear()
    return maximo - minimo


def normalizar_rig():
    """Hornea la escala de Sketchfab en los huesos y deja el cuerpo a 2,6 m."""
    bpy.ops.object.mode_set(mode="OBJECT")
    piezas = [obj for obj in bpy.data.objects if obj.type in {"ARMATURE", "MESH"}]
    if not any(obj.type == "ARMATURE" for obj in piezas):
        raise RuntimeError("El glb no trae armature.")
    seleccionar(piezas)
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")

    for obj in list(bpy.data.objects):
        if obj.type == "EMPTY":
            bpy.data.objects.remove(obj, do_unlink=True)

    armature = next(obj for obj in bpy.data.objects if obj.type == "ARMATURE")
    mallas = [obj for obj in bpy.data.objects if obj.type == "MESH"]
    modificadores = {
        malla: [mod.name for mod in malla.modifiers if mod.type == "ARMATURE"]
        for malla in mallas
    }
    seleccionar([armature] + mallas)
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

    altura = altura_deformada()
    if altura < 0.01:
        raise RuntimeError("La malla no tiene altura tras aplicar la escala.")
    factor = ALTURA_OBJETIVO / altura
    for obj in [armature] + mallas:
        obj.scale = (factor, factor, factor)
    seleccionar([armature] + mallas)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)

    for malla in mallas:
        seleccionar([malla, armature])
        bpy.context.view_layer.objects.active = armature
        bpy.ops.object.parent_set(type="ARMATURE", keep_transform=True)
        nuevos = [mod for mod in malla.modifiers if mod.type == "ARMATURE" and mod.name not in modificadores[malla]]
        for mod in nuevos:
            malla.modifiers.remove(mod)

    return armature


def exigir_rig(armature):
    escala = armature.scale
    if any(abs(componente - 1.0) > 0.01 for componente in escala):
        raise RuntimeError("La escala del armature quedó en %s; se esperaba 1." % (tuple(escala),))
    hueso = armature.data.bones.get("Bip01_Pelvis")
    if hueso is None:
        raise RuntimeError("Falta Bip01_Pelvis tras normalizar.")
    pelvis = (armature.matrix_world @ hueso.head_local).z
    if pelvis < 1.0 or pelvis > 1.6:
        raise RuntimeError("La pelvis quedó a %.2f m; se esperaba entre 1,0 y 1,6." % pelvis)
    print("armature escala=%.3f pelvis=%.2f" % (escala.x, pelvis))


def exportar():
    os.makedirs(os.path.dirname(DST), exist_ok=True)
    bpy.ops.export_scene.fbx(
        filepath=DST,
        use_selection=False,
        object_types={"ARMATURE", "MESH"},
        use_mesh_modifiers=True,
        add_leaf_bones=False,
        bake_anim=False,
        embed_textures=True,
        path_mode="COPY",
        # FBX_SCALE_NONE deja el factor de centímetros (100) en el armature.
        # Mecanim encoge la malla si hay escala entre la raíz y las caderas.
        apply_scale_options="FBX_SCALE_ALL",
        apply_unit_scale=True,
        bake_space_transform=False,
        axis_forward="-Z",
        axis_up="Y",
        global_scale=1,
    )


def main():
    if not os.path.isfile(SRC):
        raise RuntimeError("No está el glb: " + SRC)
    bpy.ops.wm.read_factory_settings(use_empty=True)
    # El bind pose que adivina el importador deja la columna en ~126 m y la malla
    # solo se ve bien por el pose. Sin adivinarlo, los huesos ya están en metros.
    bpy.ops.import_scene.gltf(filepath=SRC, guess_original_bind_pose=False)
    if renombrar_huesos() < 20:
        raise RuntimeError("No se renombraron los huesos ValveBiped.")
    print("esferas quitadas=%d" % quitar_esfera_de_preview())
    exigir_rig(normalizar_rig())
    exportar()
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=DST)
    exigir_rig(next(obj for obj in bpy.data.objects if obj.type == "ARMATURE"))
    altura = altura_deformada()
    if altura < 1.5 or altura > 4.0:
        raise RuntimeError("El FBX deformado mide %.2f m; el cuerpo no sobrevivió la exportación." % altura)
    print("huesos ok altura=%.2f bytes=%d destino=%s" % (altura, os.path.getsize(DST), DST))


if __name__ == "__main__":
    main()
