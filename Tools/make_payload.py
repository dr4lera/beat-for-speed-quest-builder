from pathlib import Path
import zipfile
root=Path(__file__).resolve().parents[1]
with zipfile.ZipFile(root/'payload.zip','w',zipfile.ZIP_DEFLATED) as archive:
 for file in sorted((root/'Template').rglob('*')):
  if not file.is_file():continue
  relative=file.relative_to(root/'Template').as_posix()
  if 'Original/' in relative or '/Generated/' in relative or file.name in ['ChartEffects.json','catalog-source.json']:
   raise RuntimeError('Game-derived content cannot be embedded: '+relative)
  archive.write(file,relative)
print('Embedded source template built; no extracted game content included.')
