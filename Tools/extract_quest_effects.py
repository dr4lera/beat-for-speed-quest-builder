import json, pathlib, re, yaml, argparse
parser=argparse.ArgumentParser(); parser.add_argument('--root',required=True); args=parser.parse_args()
root=pathlib.Path(args.root).resolve()
assets=root/'FreshExport/ExportedProject/Assets'
def docs(path):
    text=path.read_text(encoding='utf-8-sig')
    return [(int(m[0]),int(m[1]),m[2]) for m in re.findall(r'^--- !u!(\d+) &(\d+)\n(.*?)(?=^--- !u!|\Z)',text,re.M|re.S)]
def obj(path):
    return yaml.safe_load(docs(path)[0][2])['MonoBehaviour']
guidpaths={}
for p in assets.rglob('*.meta'):
    match=re.search(r'^guid: (\w+)',p.read_text(encoding='utf-8-sig'),re.M)
    if match: guidpaths[match[1]]=p.with_suffix('')
themes=[]
for p in (assets/'MonoBehaviour').glob('LevelTheme_*.asset'):
    data=obj(p)
    if 'themeName' not in data: continue
    template=data.get('configTemplate',{}).get('guid'); effects={}
    if template in guidpaths:
        effects={e['id']:e for e in obj(guidpaths[template]).get('effectConfigs',[])}
    effects.update({e['id']:e for e in data.get('effectConfigs',[])})
    for effect in effects.values():
        for key in ['gameObjectActive','meshRendererEnabled']:
            if key in effect: effect[key]=bool(effect[key])
    themes.append(dict(name=data['themeName'],primary=data['uiPrimaryColor'],effects=list(effects.values())))
mapping=[]
for entry in obj(assets/'MonoBehaviour/MusicPathPrefabConfig.asset')['datamodelPrefabs']:
    for ref in entry['prefabs']:
        p=guidpaths.get(ref.get('guid'))
        if not p: continue
        for kind,id,text in docs(p):
            if kind==114 and 'stringParameter:' in text:
                event=yaml.safe_load(text)['MonoBehaviour']
                mapping.append(dict(model=entry['datamodel'].split('/')[-1],theme=event.get('stringParameter',''),eventType=event.get('eventType',0)))
bindings=[]
for name in ['CityScene','ForestScene']:
    chunks={id:(kind,text) for kind,id,text in docs(assets/f'GameObject/{name}.prefab')}
    transforms={}; bygame={}
    def ref(text,key):
        match=re.search(r'\b'+key+r': \{fileID: (\d+)',text)
        return int(match[1]) if match else 0
    for id,(kind,text) in chunks.items():
        if kind==4:
            parent=ref(text,'m_Father'); game=ref(text,'m_GameObject')
            child=re.search(r'm_Children:(.*?)m_Father:',text,re.S)
            children=[int(v) for v in re.findall(r'fileID: (\d+)',child[1])] if child else []
            transforms[id]=(parent,children); bygame[game]=id
    def path(game):
        current=bygame[game]; indices=[]
        while transforms[current][0]:
            parent=transforms[current][0]; indices.append(transforms[parent][1].index(current)); current=parent
        return list(reversed(indices))
    def game(component):
        if component not in chunks: return 0
        kind,text=chunks[component]
        return component if kind==1 else ref(text,'m_GameObject')
    for id,(kind,text) in chunks.items():
        if kind!=114 or 'effectId:' not in text: continue
        d=yaml.safe_load(text)['MonoBehaviour']; owner=d['m_GameObject']['fileID']
        target=d.get('targetRenderer',{}).get('fileID',0) or d.get('targetLight',{}).get('fileID',0) or d.get('targetParticle',{}).get('fileID',0) or d.get('targetGameObject',{}).get('fileID',0)
        targetgame=game(target) or owner
        bindings.append(dict(track='city' if name=='CityScene' else 'forest',id=d['effectId'],path=path(targetgame),
             forceEmission=bool(d.get('overrideEmissiveColorIntensity',0)),emissionLimit=d.get('forcedEmissiveColorIntensity',1),modes=d.get('localModeOverrides',[])))
output=root/'QuestProject/Assets/Port/ChartEffects.json'
output.write_text(json.dumps(dict(themes=themes,mappings=mapping,bindings=bindings),ensure_ascii=False),encoding='utf-8')
print(json.dumps(dict(themes=len(themes),mappings=len(mapping),bindings=len(bindings),output=str(output))))
