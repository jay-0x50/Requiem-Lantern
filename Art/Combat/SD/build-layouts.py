"""Read alpha and write sprite layout JSON; never modify raster images."""
import json
from pathlib import Path
import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parents[3]
RESOURCES = ROOT / 'RequiemLantern/Assets/Resources/Combat'
# x, y, width, height, footX, footY, contactX, contactY; PNG top-left coordinates.
LAYOUTS = {
    'Lian': (490, [
        (0,0,512,512,274,498,310,450),
        (512,0,512,512,755,498,550,65),
        (1024,0,512,512,1250,493,1520,205),
        (0,512,571,512,270,997,558,875),
        (571,512,453,512,765,995,890,913),
        (1024,512,512,512,1310,992,1390,700)]),
    'Sera': (368, [
        (0,0,512,512,252,458,482,458),
        (512,0,512,512,756,458,575,275),
        (1024,0,512,512,1260,458,1477,232),
        (0,512,512,512,260,932,495,930),
        (512,512,512,512,760,935,996,934),
        (1024,512,512,512,1320,935,1450,565)]),
    'Minato': (499, [
        (0,0,512,512,260,505,145,405),
        (512,0,488,512,785,505,625,190),
        (1000,0,536,512,1290,505,1490,310),
        (0,512,548,512,257,997,505,928),
        (548,512,476,512,805,1004,680,908),
        (1024,512,512,512,1305,997,1300,820)]),
    'Noa': (488, [
        (0,0,512,512,235,503,240,250),
        (512,0,512,512,790,504,680,180),
        (1024,0,512,512,1310,504,1518,196),
        (0,512,512,512,300,998,460,849),
        (512,512,512,512,806,1005,850,735),
        (1024,512,512,512,1370,999,1260,720)]),
    'Kai': (359, [
        (0,0,512,512,266,462,165,385),
        (512,0,512,512,790,462,632,210),
        (1024,0,512,512,1260,461,1440,300),
        (0,512,512,512,277,952,440,820),
        (512,512,512,512,804,950,790,860),
        (1024,512,512,512,1333,949,1270,815)]),
    'Heidel': (364, [
        (0,0,512,512,254,479,373,329),
        (512,0,512,512,763,470,626,287),
        (1024,0,512,512,1250,470,1480,310),
        (0,512,512,512,257,939,495,875),
        (512,512,512,512,769,949,903,798),
        (1024,512,512,512,1350,942,1390,800)])
}

def main():
    report = []
    for name, (body_height, poses) in LAYOUTS.items():
        path = RESOURCES / f'{name}SDActions.png'
        image = Image.open(path)
        assert image.mode == 'RGBA', path
        width, height = image.size
        alpha = np.array(image)[:, :, 3]
        assert float((alpha == 0).mean()) > .4, f'{name}: missing transparency'
        frames = []
        checks = []
        for index, pose in enumerate(poses):
            frame = dict(zip(('x','y','width','height','footX','footY','contactX','contactY'), pose))
            x,y,w,h,fx,fy,cx,cy = pose
            assert 0 <= x < x+w <= width and 0 <= y < y+h <= height
            assert x <= fx <= x+w and y <= fy <= y+h
            assert x <= cx <= x+w and y <= cy <= y+h
            sub = alpha[y:y+h, x:x+w]
            edge = max(int(v.max()) for v in (sub[0],sub[-1],sub[:,0],sub[:,-1]))
            assert edge <= 6, f'{name} frame {index}: clipped silhouette alpha {edge}'
            assert int((sub > 5).sum()) > 10000
            frames.append(frame)
            checks.append({'pose':index,'edgeAlphaMax':edge,'visiblePixels':int((sub>5).sum())})
        layout = {'atlasWidth':width,'atlasHeight':height,'bodyHeight':body_height,'frames':frames}
        (RESOURCES/f'{name}SDActionsLayout.json').write_text(json.dumps(layout,indent=2)+'\n',encoding='utf-8')
        report.append({'id':name,'png':str(path.relative_to(ROOT)),'size':[width,height],
                       'alphaRange':[int(alpha.min()),int(alpha.max())],
                       'transparentFraction':round(float((alpha==0).mean()),6),'frames':checks})
    (Path(__file__).parent/'validation.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(f'PASS: {len(LAYOUTS)} transparent SD atlases, {len(LAYOUTS)*6} unclipped frame regions, registered feet/contact points')

if __name__ == '__main__':
    main()
