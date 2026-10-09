import json, re, argparse, shutil
from pathlib import Path

parser=argparse.ArgumentParser(); parser.add_argument('--root',required=True); args=parser.parse_args()
root=Path(args.root).resolve()
original = root / 'FreshExport/ExportedProject/Assets'
project = root / 'QuestProject'
assets = project / 'Assets/Original'
point_meta=original/'Scripts/Assembly-CSharp/PathPointTool.cs.meta'
if not point_meta.exists(): raise RuntimeError('Path point schema not found in export')
path_guid=re.search(r'^guid: (\w+)',point_meta.read_text(encoding='utf-8-sig'),re.M)[1]
(project/'Assets/Port/PathPointTool.cs.meta').write_text('fileFormatVersion: 2\nguid: '+path_guid+'\nMonoImporter:\n  serializedVersion: 2\n  defaultReferences: []\n  executionOrder: 0\n',encoding='utf-8')
allowed=['AnimationClip','AnimatorController','AnimatorOverrideController','AudioClip','AudioMixerController','Avatar','Cubemap','D3D12DeviceFilterLists','Font','GameObject','Material','Mesh','OcclusionCullingData','Scenes','Sprite','TextAsset','Texture2D','Texture3D']
if assets.exists(): raise RuntimeError('Refusing to overwrite an existing staged Original folder')
assets.mkdir(parents=True)
for folder in allowed:
    if (original/folder).exists(): shutil.copytree(original/folder,assets/folder)
    meta=original/(folder+'.meta')
    if meta.exists(): shutil.copy2(meta,assets/(folder+'.meta'))
for name in ['ForestScene','CityScene','MainCube','Sting','PathFollower','board fragture effect']:
    if not (assets/'GameObject'/(name+'.prefab')).exists(): raise RuntimeError('Required playtest asset missing: '+name)

guid_paths = {}
for meta in original.rglob('*.meta'):
    m = re.search(r'^guid: (\w+)', meta.read_text(encoding='utf-8-sig'), re.M)
    if m:
        guid_paths[m[1]] = meta.with_suffix('').relative_to(original).as_posix()

songs = []
for data in original.glob('MonoBehaviour/MusicDataAsset*.asset'):
    text = data.read_text(encoding='utf-8-sig')
    chart_guid = re.search(r'jsonFile:.*guid: (\w+)', text)[1]
    audio_guid = re.search(r'musicClip:.*guid: (\w+)', text)[1]
    chart = json.loads((original / guid_paths[chart_guid]).read_text(encoding='utf-8-sig'))
    songs.append({'title': chart.get('bfsMetadata', {}).get('songName', data.stem),
                  'chart': 'Assets/Original/' + guid_paths[chart_guid],
                  'audio': 'Assets/Original/' + guid_paths[audio_guid],
                  'track': chart['musicData'].get('trackId', 'forest')})
(project / 'catalog-source.json').write_text(json.dumps({'songs': songs}, indent=2), encoding='utf-8')

# Strip desktop behaviour/effects only in the isolated build copy. Keep serialized
# path-point references so the same track coordinates drive the rebuilt rider.
counts = {'prefabs': 0, 'desktop_components_removed': 0}
for prefab in assets.rglob('*.prefab'):
    text = prefab.read_text(encoding='utf-8-sig')
    blocks = re.split(r'(?=^--- !u!)', text, flags=re.M)
    removed, keep = set(), []
    for block in blocks:
        h = re.match(r'--- !u!(\d+) &(\d+)', block)
        if h and int(h[1]) in (114, 95, 82, 20, 120, 198, 199, 328, 329):
            if int(h[1]) == 114 and path_guid in block:
                keep.append(block)
            else:
                removed.add(h[2])
        else:
            keep.append(block)
    result = ''.join(keep)
    result = re.sub(r'^  - component: \{fileID: (\d+)\}\s*\n',
                    lambda m: '' if m[1] in removed else m[0], result, flags=re.M)
    prefab.write_text(result, encoding='utf-8')
    counts['prefabs'] += 1
    counts['desktop_components_removed'] += len(removed)
print(json.dumps({'songs': songs, 'stripping': counts}, indent=2))
