from pathlib import Path
import tarfile, json, shutil

root = Path(__file__).resolve().parents[1]
project = root / 'RequiemLantern'
editor = Path(r'C:\Program Files\Unity\Hub\Editor\6000.6.0f1\Editor')
template = editor / 'Data/Resources/PackageManager/ProjectTemplates/com.unity.template.2d-cross-platform-2d-7.0.0.tgz'
with tarfile.open(template) as archive:
    prefix = 'package/ProjectData~/'
    for member in archive.getmembers():
        if not member.isfile() or not member.name.startswith(prefix): continue
        relative = member.name[len(prefix):]
        if not (relative.startswith('ProjectSettings/') or relative.startswith('Assets/Settings/')): continue
        if 'Welcome' in relative or 'Tutorial' in relative or 'InputSystem' in relative: continue
        destination = project / relative
        if not destination.exists():
            destination.parent.mkdir(parents=True, exist_ok=True)
            destination.write_bytes(archive.extractfile(member).read())
for directory in ['Packages','Assets/Scripts/Presentation','Assets/Editor','Assets/Resources/Art','Assets/Resources/Fonts','Assets/Scenes']:
    (project/directory).mkdir(parents=True, exist_ok=True)
(project/'Packages/manifest.json').write_text(json.dumps({'dependencies':{
    'com.unity.render-pipelines.universal':'17.6.0',
    'com.unity.ugui':'2.6.0',
    'com.unity.modules.audio':'1.0.0',
    'com.unity.modules.screencapture':'1.0.0',
    'com.unity.modules.imageconversion':'1.0.0',
    'com.unity.modules.jsonserialize':'1.0.0',
    'com.unity.modules.ui':'1.0.0',
    'com.unity.modules.uielements':'1.0.0',
    'com.unity.modules.physics2d':'1.0.0',
}},indent=2)+'\n',encoding='utf-8')
(project/'ProjectSettings/ProjectVersion.txt').write_text('m_EditorVersion: 6000.6.0f1\n',encoding='utf-8')
settings=project/'ProjectSettings/ProjectSettings.asset'
value=settings.read_text(encoding='utf-8').replace('activeInputHandler: 1','activeInputHandler: 0')
settings.write_text(value,encoding='utf-8')
names={'리안':'Lian','세라':'Sera','미나토':'Minato','노아':'Noa','하이델':'Heidel','주인공':'Kai','오르페':'Orphe'}
for source in (root/'이미지/캐릭터').rglob('*.png'):
    shutil.copy2(source,project/'Assets/Resources/Art'/f'{names[source.stem]}_Reference.png')
print('Project scaffold prepared:',project)
