"""Übernimmt die freigegebenen Charakterbilder und leert nur ihre variablen Zahlenfelder.

Die Originale liegen unter Inspiration/CharacterMenuPreviews (nicht versioniert).
Aufruf mit dem mitgelieferten Python: tools/extract-character-menu.py
"""
from pathlib import Path
from PIL import Image, ImageFilter
import uuid

root = Path(__file__).resolve().parent.parent
source = root / 'Inspiration/CharacterMenuPreviews'
target = root / 'SoccerFight/Assets/Resources/Menu/CharacterDetails'
target.mkdir(parents=True, exist_ok=True)

# Die Felder liegen in allen sechs freigegebenen Bildern an denselben Stellen.
# Kleine Abweichungen bleiben durch die großzügigen Innenflächen der Platten verdeckt.
fields = [(1012, 143, 1090, 207), (1020, 610, 1088, 651),
          (1240, 610, 1310, 651), (1452, 610, 1522, 651),
          (1260, 716, 1310, 746), (1406, 24, 1469, 51),
          (1555, 24, 1624, 51)]

def clear(im, box, numbers_only=False):
    # Farben aus den freien Rändern derselben Originalplatte; kein neues Grafikdesign.
    x0, y0, x1, y1 = box
    px = im.load()
    if numbers_only:
        # Nur die dunkle Zahl freistellen: Facetten und Gravuren der Originalraute bleiben erhalten.
        mask = Image.new('L', im.size)
        mp = mask.load()
        for y in range(y0, y1):
            for x in range(x0, x1):
                r, g, b = px[x, y]
                if r < 80 and g < 125 and b < 150:
                    mp[x, y] = 255
        mask = mask.filter(ImageFilter.MaxFilter(5))
        mp = mask.load()
        for y in range(y0, y1):
            for x in range(x0, x1):
                if not mp[x, y]:
                    continue
                samples = []
                for dx, dy in ((-1,0), (1,0), (0,-1), (0,1)):
                    xx, yy = x, y
                    while mp[xx, yy]:
                        xx += dx; yy += dy
                    samples.append(px[xx, yy])
                px[x, y] = tuple(round(sum(p[i] for p in samples) / 4) for i in range(3))
        return
    for y in range(y0, y1):
        a, b = px[x0 - 3, y], px[x1 + 3, y]
        for x in range(x0, x1):
            t = (x - x0) / max(1, x1 - x0 - 1)
            px[x, y] = tuple(round(a[i] * (1-t) + b[i] * t) for i in range(3))

for name in ('rio', 'bruno', 'mira', 'dre', 'titan', 'nova'):
    im = Image.open(source / (name + '.png')).convert('RGB')
    assert im.size == (1672, 941), (name, im.size)
    for index, box in enumerate(fields):
        clear(im, box, 1 <= index <= 3)
    output = target / (name + '.png')
    im.save(output)
    meta = output.with_suffix('.png.meta')
    if not meta.exists():
        template = (root / 'SoccerFight/Assets/Resources/Menu/StadionUfo.png.meta').read_text()
        template = template.replace('bc1a174a192b4e2e9606b464ccd49702', uuid.uuid4().hex)
        template = template.replace('enableMipMap: 1', 'enableMipMap: 0').replace('nPOTScale: 2', 'nPOTScale: 0')
        meta.write_text(template, encoding='utf-8', newline='\n')
    print(name)
