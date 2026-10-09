"""Inspect the embedded authored template; extracted game content must stay local."""
from pathlib import Path
import zipfile

root = Path(__file__).resolve().parents[1]
blocked = {'.apk', '.bfs', '.dll', '.exe', '.wav', '.ogg', '.mp3', '.fbx', '.png', '.jpg', '.mesh', '.unity', '.prefab'}
with zipfile.ZipFile(root / 'payload.zip') as archive:
    names = archive.namelist()
    for name in names:
        parts = Path(name).parts
        assert 'Original' not in parts and 'Generated' not in parts, name
        assert Path(name).suffix.lower() not in blocked, name
        assert Path(name).name not in ('ChartEffects.json', 'catalog-source.json'), name
        assert '..' not in parts and not Path(name).is_absolute(), name
print(f'PASS: {len(names)} authored template files; no extracted game content or game binaries.')
