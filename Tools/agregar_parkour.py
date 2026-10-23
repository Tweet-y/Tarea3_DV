"""Agrega cuatro rutas al YAML de Unity sin reemplazar objetos existentes."""
import math
import re
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SCENE = ROOT / 'Assets/Scenes/MapaV1.unity'
original = SCENE.read_text(encoding='utf-8-sig')
if 'm_Name: ZonasParkour' in original:
    raise SystemExit('Las zonas ya existen; no se duplicaron.')

existing_ids = set(re.findall(r'^--- !u!\d+ &(-?\d+)', original, re.M))
next_id = 800000000000
blocks = []


def alloc():
    global next_id
    next_id += 1
    assert str(next_id) not in existing_ids
    return next_id


def vec(v):
    return '{' + ', '.join(f'{a}: {n:.6f}' for a, n in zip('xyz', v)) + '}'


def common(go):
    return f'''  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  m_GameObject: {{fileID: {go}}}
'''


def node(name, parent=0, pos=(0, 0, 0), scale=(1, 1, 1), cube=False, gold=False):
    go, tr = alloc(), alloc()
    ids = [tr]
    if cube:
        mf, mr, col = alloc(), alloc(), alloc()
        ids += [mf, mr, col]
    blocks.append(f'''--- !u!1 &{go}
GameObject:
  m_ObjectHideFlags: 0
  m_CorrespondingSourceObject: {{fileID: 0}}
  m_PrefabInstance: {{fileID: 0}}
  m_PrefabAsset: {{fileID: 0}}
  serializedVersion: 6
  m_Component:
''' + ''.join(f'  - component: {{fileID: {i}}}\n' for i in ids) + f'''  m_Layer: 0
  m_Name: {name}
  m_TagString: Untagged
  m_Icon: {{fileID: 0}}
  m_NavMeshLayer: 0
  m_StaticEditorFlags: 0
  m_IsActive: 1
''')
    ti = len(blocks)
    blocks.append(f'''--- !u!4 &{tr}
Transform:
{common(go)}  serializedVersion: 2
  m_LocalRotation: {{x: 0, y: 0, z: 0, w: 1}}
  m_LocalPosition: {vec(pos)}
  m_LocalScale: {vec(scale)}
  m_ConstrainProportionsScale: 0
  m_Children: []
  m_Father: {{fileID: {parent}}}
  m_LocalEulerAnglesHint: {{x: 0, y: 0, z: 0}}
''')
    if cube:
        blocks.append(f'''--- !u!33 &{mf}
MeshFilter:
{common(go)}  m_Mesh: {{fileID: 10202, guid: 0000000000000000e000000000000000, type: 0}}
''')
        # Usar el renderer de un objeto ya configurado en este proyecto.
        renderer = re.search(r'--- !u!23 &1842738548\nMeshRenderer:\n(.*?)(?=--- !u!)', original, re.S)[1]
        renderer = renderer.replace('fileID: 1842738547', f'fileID: {go}')
        material = '6265851632f1b3547a4f3202b980d29f' if gold else 'c40d9eff6fbcda74db999de2eac77216'
        renderer = renderer.replace('f3b56db621cd17b42bd349b11e2f32a8', material)
        blocks.append(f'--- !u!23 &{mr}\nMeshRenderer:\n' + renderer)
        blocks.append(f'''--- !u!65 &{col}
BoxCollider:
{common(go)}  m_Material: {{fileID: 0}}
  m_IncludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_ExcludeLayers:
    serializedVersion: 2
    m_Bits: 0
  m_LayerOverridePriority: 0
  m_IsTrigger: 0
  m_ProvidesContacts: 0
  m_Enabled: 1
  serializedVersion: 3
  m_Size: {{x: 1, y: 1, z: 1}}
  m_Center: {{x: 0, y: 0, z: 0}}
''')
    return tr, ti


def children(index, ids):
    blocks[index] = blocks[index].replace('  m_Children: []', '  m_Children:\n' + '\n'.join(f'  - {{fileID: {i}}}' for i in ids))


prefab = (ROOT / 'Assets/Prefabs/SM_Prop_LargeSign_Bottle_01_Especial.prefab').read_text(encoding='utf-8-sig')


def bottle(name, pos, parent):
    text = prefab[prefab.index('--- !u!'):]
    mapping = {int(i): alloc() for i in re.findall(r'^--- !u!\d+ &(\d+)', text, re.M)}
    text = re.sub(r'(&|fileID: )(\d+)', lambda m: m[1] + str(mapping.get(int(m[2]), int(m[2]))), text)
    text = text.replace('m_Name: SM_Prop_LargeSign_Bottle_01_Especial', 'm_Name: ' + name)
    # Solo el Transform de la raiz; conservar escala, collider, luz y script.
    start = text.index(f'--- !u!4 &{mapping[400001]}\n')
    end = text.index('--- !u!', start + 5)
    transform = text[start:end].replace('m_LocalPosition: {x: 0, y: 0, z: 0}', 'm_LocalPosition: ' + vec(pos))
    transform = transform.replace('m_Father: {fileID: 0}', f'm_Father: {{fileID: {parent}}}')
    text = text[:start] + transform + text[end:]
    text = text.replace('amplitudFlotacion: 0.35', 'amplitudFlotacion: 0.15')
    blocks.append('\n'.join(line.rstrip() for line in text.splitlines()) + '\n')
    return mapping[400001]


# Azoteas con orientacion identidad: su collider superior esta en Y + 0.5.
targets = [('Patio', '17', -30., 16.6, -55., 0),
           ('Centro', '04', -20., 18.1, -20., 0),
           ('CalleOeste', '63', -50., 19.6, -40., 90),
           ('BarrioSur', '43', -55., 19.6, -125., 0)]
root, root_index = node('ZonasParkour')
route_roots = []
report = []
for label, roof_id, x, y, z, yaw in targets:
    assert f'value: SM_Bld_Apartment_Roof_{roof_id}\n' in original
    route, route_index = node('Parkour_' + label, root)
    route_roots.append(route)
    parts = []
    roof_top = y + .4998675
    finish = roof_top + .10
    ground = 7.6
    count = math.ceil((finish - ground) / .80)
    rise = (finish - ground) / count
    assert .65 < rise <= .80 < 1.2
    # Giro de cuatro plataformas: 3.2 m entre pisos superpuestos, con
    # espacio para la capsula de CJ y saltos cortos entre esquinas.
    corners = [(-.8, -.8), (-.8, .8), (.8, .8), (.8, -.8)]
    offset = (-(count - 1)) % 4  # Ultimo salto junto a la fachada.
    def place(px, py, pz):
        if yaw == 90:
            return (x + pz, py, z - px)
        return (x + px, py, z + pz)
    for step in range(count):
        dx, dz = corners[(step + offset) % 4]
        top = ground + (step + 1) * rise
        platform, _ = node(f'Plataforma_{step + 1:02}', route,
                           place(-2.5 + dx, top - .15, 2.6 + dz), (1.4, .3, 1.4), True, step % 4 == 0)
        parts.append(platform)
    landing_size = (1.6, .3, 2.4) if yaw == 90 else (2.4, .3, 1.6)
    landing, _ = node('Pasarela_Azotea', route, place(-2.9, finish - .15, .1), landing_size, True, True)
    parts.append(landing)
    for number, dx in enumerate((-1.2, 0., 1.2), 1):
        parts.append(bottle(f'Botella_Azotea_{label}_{number}', place(-2.5 + dx, roof_top + .85, -2.3), route))
    children(route_index, parts)
    report.append(f'{label}: {count} saltos; subida {rise:.3f} m; azotea {place(-2.5, roof_top, -2.3)}; 3 botellas.')
children(root_index, route_roots)

marker = '--- !u!1660057539 &9223372036854775807'
assert original.count(marker) == 1
output = original.replace(marker, ''.join(blocks) + marker)
output += f'  - {{fileID: {root}}}\n'
# Cada documento YAML anterior sigue siendo identico; solo SceneRoots
# recibe la nueva raiz. Esto protege el resto del mapa contra reemplazos.
def documents(text):
    return {m[1]: m[0] for m in re.finditer(r'^--- !u!\d+ &(-?\d+).*?(?=^--- !u!|\Z)', text, re.M | re.S)}
before, after = documents(original), documents(output)
for identifier, doc in before.items():
    if identifier != '9223372036854775807':
        assert after[identifier] == doc, identifier
ids = re.findall(r'^--- !u!\d+ &(-?\d+)', output, re.M)
assert len(ids) == len(set(ids)), 'IDs duplicados'
assert output.count('m_Name: Botella_Azotea_') == 12
backup = ROOT / 'Backups/MapaV1-antes-parkour.unity'
backup.parent.mkdir(exist_ok=True)
if backup.exists():
    assert backup.read_text(encoding='utf-8-sig') == original, 'La copia anterior pertenece a otro estado del mapa'
else:
    backup.write_bytes(SCENE.read_bytes())
SCENE.write_text(output, encoding='utf-8', newline='\n')
(ROOT / 'Backups/parkour-verificacion.txt').write_text('\n'.join(report) + '\nObjetos anteriores conservados: ' + str(len(before) - 1) + '\nBotellas nuevas: 12. Total esperado: 30.\n', encoding='utf-8')
print('\n'.join(report))
print(f'OK: {len(before)-1} documentos existentes conservados, 4 rutas y 12 botellas nuevas.')
