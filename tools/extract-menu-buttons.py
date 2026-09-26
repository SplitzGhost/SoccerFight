"""Übernimmt Bildpixel aus den freigegebenen Probebildern; keine KI-Neugenerierung.

Aufruf: python tools/extract-menu-buttons.py --sources <Ordner mit den Probebildern>
Die RGB-Werte der ausgeschnittenen Elemente bleiben unverändert. Nur die Silhouette
wird freigestellt; bei variablen Beschriftungen werden leere Originalflächen verwendet.
"""
from PIL import Image, ImageDraw
from pathlib import Path
import argparse, json, uuid, re

args=argparse.ArgumentParser();args.add_argument('--sources',required=True);opt=args.parse_args()
src=Path(opt.sources);out=Path('SoccerFight/Assets/Resources/UiButtons/Exact');out.mkdir(parents=True,exist_ok=True)
sources={
 'main':'exec-a6e223bc-4e46-43f2-950e-a71d4fe3d0b8.png',
 'pause':'exec-93321712-69c7-4853-a81f-f3a3c67d82b1.png',
 'duo':'exec-4b302c3b-2012-428d-9f23-407abbc07aa1.png',
 'room':'exec-bf3c63fb-ae9c-4f1a-8865-ee4529dc350e.png',
 'connect':'exec-41b4b148-9d59-4f39-9396-19c9ae892966.png',
 'lobby':'exec-33a02139-9e37-408c-be9c-cc0a18fdf4d2.png',
 'options':'03-optionen.png', 'pausedoptions':'exec-04f857d8-82c2-402a-b685-442460bcb341.png', 'dev':'06-entwicklermenue.png',
 'states':'05-events-belohnungen-zustaende.png', 'shop':'02-spieler-shop.png',
 'overview':'01-hauptmenue.png',
 'players':'exec-8c8062b1-1571-4b53-b786-f6cde8c7af44.png',
 'devview':'exec-e2e0b1c5-d343-4761-b2d6-a6f7d8a9c56f.png',
 'rewards':'exec-ac920881-1b84-4135-a559-c9f72682b3d5.png'}
images={k:Image.open(src/v).convert('RGBA') for k,v in sources.items()}
assets=[]
def cut(name,source,box,corner=0,border=0,erase=None):
    im=images[source].crop(box);w,h=im.size
    if corner:
        mask=Image.new('L',(w,h));ImageDraw.Draw(mask).polygon([(corner,0),(w-corner-1,0),(w-1,corner),(w-1,h-corner-1),(w-corner-1,h-1),(corner,h-1),(0,h-corner-1),(0,corner)],fill=255);im.putalpha(mask)
    if erase:
        # Freie Stelle derselben Platte statt einer neu erzeugten oder umgefärbten Fläche.
        for target,sample in erase:
            patch=im.crop(sample).resize((target[2]-target[0],target[3]-target[1]),Image.Resampling.NEAREST)
            im.paste(patch,target[:2])
    assets.append((name,im,border,source,box))
    return im

cut('main-play','main',(1180,705,1651,848),22)
for mode,blank in [('main-mode',False),('main-mode-blank',True)]:
    im=cut(mode,'main',(1187,529,1642,699),20,erase=[((157,39,420,82),(157,26,420,34))] if blank else None)
    m=im.getchannel('A');d=ImageDraw.Draw(m);d.rectangle((0,0,88,18),fill=0);d.rectangle((156,0,im.width,18),fill=0);im.putalpha(m)
cut('main-tag','main',(690,197,983,278),15)
cut('main-tag-blank','main',(690,197,983,278),15,erase=[((103,12,229,65),(110,5,220,10))])
nav=[('home',367,496),('chars',506,637),('shop',644,771),('events',775,904),('ranking',910,1042),('settings',1049,1178)]
for name,x0,x1 in nav:cut('nav-'+name,'main',(x0,4,x1,94),14)
for name,box in [('friends',(1493,17,1547,69)),('info',(1549,17,1602,69)),('quit',(1604,17,1659,69)),('plus',(1295,25,1326,57))]:cut(name,'main',box,6)

pause_boxes=[(648,263,1026,349),(648,353,1026,441),(648,447,1026,534),(648,538,1026,626),(648,629,1026,715),(648,716,1026,803)]
pause_names=['WEITER','EINSTELLUNGEN','NEU STARTEN','HAUPTMENÜ','DEVELOPER-MODUS','BEENDEN']
for name,box in zip(pause_names,pause_boxes):cut('action-'+name,'pause',box,0)
panel=images['pause'].crop((607,98,1068,843))
for box in pause_boxes:
    panel.paste(panel.crop((45,154,415,163)).resize((box[2]-box[0],box[3]-box[1]),Image.Resampling.NEAREST),(box[0]-607,box[1]-98))
mask=Image.new('L',panel.size);w,h=panel.size;c=45
ImageDraw.Draw(mask).polygon([(c,0),(w-c-1,0),(w-1,c),(w-1,h-c-1),(w-c-1,h-1),(c,h-1),(0,h-c-1),(0,c)],fill=255);panel.putalpha(mask)
assets.append(('pause-panel',panel,0,'pause',(607,98,1068,843)))

cut('action-ZURÜCK','pausedoptions',(660,748,1014,839),18)
cut('action-STANDARD WIEDERHERSTELLEN','pausedoptions',(1039,748,1344,831),20)
cut('key-face','options',(1298,124,1437,174),7,10,erase=[((39,11,100,40),(17,11,29,40))])
cut('slider-thumb','options',(671,550,730,611),0)
cut('slider-track','options',(434,556,733,606),0,12)
cut('slider-fill','options',(445,568,672,592),0,5)
cut('toggle-off','options',(468,136,784,194),7)
cut('toggle-on','options',(469,236,784,296),7)
rows=[('VOLLBILD',(59,121,855,218)),('VSYNC',(59,223,855,319)),('FPS ANZEIGEN',(59,326,855,419)),('FARBSAUM-EFFEKT',(59,428,855,527))]
for name,box in rows:
    im=cut('toggle-row-'+name,'options',box,16)
    ImageDraw.Draw(im).rectangle((406,8,728,78),fill=(0,0,0,0))
for name,box in [('BILDSCHIRMWACKELN',(59,538,855,624)),('LEUCHTEN (BLOOM)',(59,628,855,716)),('LAUTSTÄRKE',(59,719,855,806))]:
    im=cut('slider-row-'+name,'options',box,16)
    ImageDraw.Draw(im).rectangle((371,8,770,78),fill=(0,0,0,0))
key_names=['NACH LINKS','NACH RECHTS','SPRINGEN','DURCHFALLEN','SCHUSS','KLASSEN-FÄHIGKEIT','FÄHIGKEIT 1','FÄHIGKEIT 2','FÄHIGKEIT 3','FÄHIGKEIT 4']
key_bounds=[(904,121,1475,177),(904,182,1475,240),(904,245,1475,302),(904,306,1475,363),(904,368,1475,425),(904,429,1475,488),(904,492,1475,553),(904,560,1475,619),(904,623,1475,686),(904,690,1475,756)]
for name,box in zip(key_names,key_bounds):
    im=cut('key-row-'+name,'options',box,9)
    ImageDraw.Draw(im).rectangle((350,2,550,im.height-3),fill=(0,0,0,0))

# Die unveränderten Randpixel bilden die skalierbaren Flächen für wechselnde Texte.
cut('plate-petrol','pause',pause_boxes[1],0,24,erase=[((41,5,145,80),(130,7,142,75)),((145,12,349,73),(130,7,142,75))])
cut('plate-gold','pause',pause_boxes[0],0,24,erase=[((44,4,145,79),(131,10,141,70)),((145,10,348,73),(131,10,141,70))])
cut('plate-coral','pause',pause_boxes[5],0,24,erase=[((42,4,145,80),(131,11,142,70)),((145,10,348,74),(131,11,142,70))])
cut('panel-border','pause',(607,98,1068,843),45,45,erase=[((35,25,425,719),(45,154,415,163))])

# Auch kleine und selten benutzte Aktionen behalten ihre gemalten Originalbeschriftungen.
dev_actions=[('VOLLE HEILUNG',(41,395,269,489)),('ALLE FÄHIGKEITEN FREISCHALTEN',(281,395,531,489)),
 ('+5 ZUFÄLLIGE UPGRADES',(543,395,795,489)),('BUILD LEEREN',(805,395,1016,489)),
 ('+500 MÜNZEN',(1027,395,1265,489)),('PROFIL LÖSCHEN',(1276,395,1496,489)),
 ('ZU STAGE SPRINGEN',(1159,265,1496,376)),('WELLE ÜBERSPRINGEN',(126,510,515,599)),
 ('BOSS RUFEN',(541,509,964,599)),('ALLE GEGNER BESIEGEN',(989,510,1407,599)),
 ('NORMAL',(183,617,532,708)),('ELITE',(578,617,961,708)),('MINIBOSS',(1002,617,1356,708)),
 ('UPGRADE-KARTEN',(49,727,510,838)),('BOSS-KARTEN (SELTEN+)',(533,727,1005,838)),
 ('FÄHIGKEIT WÄHLEN',(1027,727,1491,838)),('−',(708,275,801,369)),('+',(1037,275,1129,369))]
dev_actions=[('VOLLE HEILUNG',(208,552,415,624)),('ALLE FÄHIGKEITEN FREISCHALTEN',(422,552,630,624)),
 ('+5 ZUFÄLLIGE UPGRADES',(208,631,415,710)),('BUILD LEEREN',(422,631,630,710)),
 ('+500 MÜNZEN',(208,718,415,798)),('PROFIL LÖSCHEN',(422,718,630,798)),
 ('ZU STAGE SPRINGEN',(673,362,1080,451)),('WELLE ÜBERSPRINGEN',(673,461,875,526)),
 ('BOSS RUFEN',(880,461,1080,526)),('ALLE GEGNER BESIEGEN',(673,534,1080,597)),
 ('NORMAL',(668,611,801,665)),('ELITE',(807,611,939,665)),('MINIBOSS',(947,611,1081,665)),
 ('UPGRADE-KARTEN',(668,680,875,753)),('BOSS-KARTEN (SELTEN+)',(880,680,1082,753)),
 ('FÄHIGKEIT WÄHLEN',(668,760,1082,827)),('−',(677,282,756,349)),('+',(1001,282,1078,349))]
for name,box in dev_actions:cut('action-'+name,'devview',box,9)
cut('dev-panel','devview',(166,110,1510,865),28,erase=[((40,161,466,713),(50,155,61,159)),((501,162,918,716),(512,155,523,160)),((955,161,1307,714),(1268,157,1276,161))])
cut('dev-stage','devview',(768,282,986,349),9,erase=[((149,16,188,51),(189,17,198,50))])
for name,box in [('UNVERWUNDBAR',(208,277,415,345)),('KEINE ABKLINGZEITEN',(422,277,630,345)),('EIN-TREFFER-KILLS',(208,355,415,424)),('INFO-ANZEIGE',(422,355,630,424))]:
    im=cut('toggle-row-'+name,'devview',box,9);ImageDraw.Draw(im).rectangle((15,12,74,55),fill=(0,0,0,0))
cut('dev-toggle-on','devview',(223,290,282,333),6)
cut('dev-toggle-off','devview',(437,290,496,333),6)
cut('slider-row-SPIELTEMPO','devview',(208,439,630,539),9,erase=[((81,43,330,75),(82,39,330,41)),((344,41,400,79),(337,42,343,76))])
cut('dev-slider-thumb','devview',(396,479,436,518),5)
cut('dev-slider-track','devview',(284,485,536,510),0,8,erase=[((110,0,151,25),(65,0,106,25))])
cut('action-RAUM ERSTELLEN','duo',(194,299,819,683),22)
cut('action-BEITRETEN','duo',(979,553,1371,646),16)
cut('action-RAUM SCHLIESSEN','room',(632,725,1045,817),16)
cut('action-ABBRECHEN','connect',(675,659,997,753),16)
cut('action-DUO STARTEN','lobby',(625,720,1047,897),18)
cut('action-VERLASSEN','lobby',(53,802,366,897),15)
cut('code-cell','duo',(919,438,997,533),9,10,erase=[((16,21,60,66),(12,22,16,66))])
cut('duo-join-panel','duo',(866,299,1480,684),24,erase=[((50,138,567,234),(60,128,75,132)),((110,253,513,347),(40,254,50,260))])
cut('duo-host-panel','room',(324,311,1339,700),24,erase=[((41,24,973,361),(76,230,83,240))])
cut('duo-connect-panel','connect',(502,243,1170,639),24,erase=[((50,107,612,331),(50,170,62,180))])
cut('duo-spinner','connect',(775,420,899,542),18)
cut('currency-coins','main',(1203,18,1335,64),5,12,erase=[((58,11,78,36),(84,12,89,35))])
cut('currency-gems','main',(1350,18,1478,64),5,12,erase=[((53,11,76,36),(82,12,87,35))])
frame=cut('frame-only','pause',(607,98,1068,843),45,45)
ImageDraw.Draw(frame).rectangle((35,36,425,710),fill=(0,0,0,0))
for name,box in [('WÄHLEN',(899,823,1068,904)),('AUSWÄHLEN',(899,823,1068,904))]:cut('action-'+name,'states',box,12)
for i,box in enumerate([(318,593,613,675),(690,593,983,675),(1061,593,1356,675)]):cut('choose-'+str(i),'rewards',box,12)
frame=cut('reward-frame','rewards',(297,245,634,696),0,30,erase=[((20,58,315,430),(26,285,35,310)),((69,10,269,44),(65,15,71,33))])
mask=Image.new('L',frame.size);ImageDraw.Draw(mask).polygon([(71,0),(269,0),(287,16),(317,16),(336,40),(336,417),(317,450),(21,450),(0,423),(0,40),(24,16),(52,16)],fill=255);frame.putalpha(mask)
cut('upgrade-row','devview',(1128,273,1447,312),5,8,erase=[((15,5,308,35),(64,7,74,33))])
for name,box in [('Damage',(351,304,575,457)),('Speed',(727,318,933,456)),('Heart',(1127,320,1289,451))]:cut('up-icon-'+name,'rewards',box,18)
for label,box in [('FUSSBALL',(253,130,535,201)),('BASKETBALL',(555,130,829,201)),('BOXEN',(849,130,1128,201)),('TENNIS',(1148,130,1426,201))]:cut('action-'+label,'players',box,12)
for label,box in [('STUFE 1  ·  WÄHLEN',(665,749,1013,824)),('UPGRADE 2  ·  12 ◇',(227,749,579,824))]:cut('action-'+label,'players',box,12)
for label,box in [('GEWÄHLT',(359,397,649,558)),('ZUM SHOP',(977,396,1272,555)),('STUFE 10  ·  MAXIMUM',(363,580,654,713)),("LOS GEHT'S",(519,741,1123,881))]:cut('action-'+label,'shop',box,12)

# Auch die Symbole stammen aus den ursprünglichen Bildern, nicht aus dem späteren Nachbau.
menu_icons=[('overview',(137,155,384,327)),('overview',(729,228,846,344)),('overview',(914,242,1028,338)),
 ('overview',(1116,236,1220,343)),('overview',(1304,233,1406,340)),('overview',(1492,232,1602,341)),
 ('overview',(91,767,162,827)),('overview',(241,759,304,827)),('overview',(386,758,449,827)),
 ('overview',(533,766,599,819)),('overview',(1515,529,1575,599)),('shop',(159,398,279,491)),
 ('shop',(477,586,539,653)),('overview',(677,762,744,826)),('shop',(182,166,341,304)),('shop',(581,166,729,304))]
for i,(source,box) in enumerate(menu_icons):cut('menu-icon-'+str(i),source,box,8)
sport_icons=[('players',(240,257,288,313)),('players',(674,257,725,313)),('players',(1114,257,1168,313)),
 ('dev',(1092,735,1191,824)),('shop',(913,169,1092,302)),('shop',(1276,169,1467,303)),
 ('overview',(661,481,826,621)),('shop',(230,582,292,663)),('devview',(1140,313,1185,350)),
 ('shop',(136,579,228,660)),('rewards',(1127,320,1289,451)),('overview',(1007,750,1088,831)),
 ('overview',(533,766,599,819)),('shop',(581,166,729,304)),('overview',(526,224,645,343)),('players',(674,257,725,313))]
for i,(source,box) in enumerate(sport_icons):cut('sport-icon-'+str(i),source,box,6)

# Zahlen und Namen bleiben dynamisch, ihre Buchstaben kommen aus den Originalüberschriften.
from PIL import ImageFilter
glyphs={}
def letters(source,box,text,bounds=None):
    im=images[source].crop(box);p=im.load();mask=Image.new('L',im.size);mp=mask.load()
    for yy in range(im.height):
        for xx in range(im.width):
            r,g,b,a=p[xx,yy]
            if min(r,g,b)>120 and max(r,g,b)-min(r,g,b)<85:mp[xx,yy]=255
    if bounds is None:
        active=[sum(mask.getpixel((xx,yy))>0 for yy in range(im.height))>2 for xx in range(im.width)]
        bounds=[];start=None
        for xx,v in enumerate(active+[False]):
            if v and start is None:start=xx
            if not v and start is not None:
                if xx-start>3:bounds.append((start,xx))
                start=None
    if len(text)!=len(bounds):raise ValueError((source,text,len(bounds)))
    for ch,(x0,x1) in zip(text,bounds):
        if ch in glyphs:continue
        m=mask.crop((x0,0,x1,im.height));rect=m.getbbox()
        if not rect:continue
        tile=im.crop((x0,0,x1,im.height));tile.putalpha(m.filter(ImageFilter.MaxFilter(3)))
        tile=tile.crop((0,max(0,rect[1]-1),x1-x0,min(im.height,rect[3]+1)))
        glyphs[ch]=tile
letters('options',(57,29,821,93),'OPTIONEN&STEUERUNG')
letters('overview',(48,51,884,122),'HAUPTMENÜ&NAVIGATION')
letters('states',(51,25,980,83),'EVENTS,BELOHNUNGEN&ZUSTÄNDE')
letters('dev',(42,5,752,114),'ENTWICKLERMENÜ',[(3,48),(55,110),(116,158),(163,231),(237,256),(264,304),(311,362),(368,405),(410,449),(453,504),(510,566),(573,609),(615,661),(666,708)])
letters('rewards',(527,130,1128,181),'WELLE2GESCHAFFT')
font=Image.new('RGBA',(1024,512));x=y=row_h=0;entries=[]
for ch,im in glyphs.items():
    if x+im.width+8>1024:x=0;y+=row_h+8;row_h=0
    font.paste(im,(x,y));entries.append(dict(unicode=ord(ch),x=x,y=512-y-im.height,width=im.width,height=im.height));x+=im.width+8;row_h=max(row_h,im.height)
font.save(out/'letters.png')
(out/'letters.json').write_text(json.dumps(dict(items=entries),ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')

# Freistellung der vorhandenen Buchstaben ohne Änderung ihrer Farben oder Form.
title=cut('main-title','main',(43,132,438,193))
pixels=title.load()
for yy in range(title.height):
    for xx in range(title.width):
        r,g,b,a=pixels[xx,yy]
        if min(r,g,b)<140 or max(r,g,b)-min(r,g,b)>65:pixels[xx,yy]=(r,g,b,0)
title.putalpha(title.getchannel('A').filter(ImageFilter.MaxFilter(5)))
original_title=images['main'].crop((43,132,438,193));original_title.putalpha(title.getchannel('A'));title.paste(original_title,(0,0))
thumb=next(a[1] for a in assets if a[0]=='slider-thumb')
mask=Image.new('L',thumb.size);ImageDraw.Draw(mask).polygon([(29,0),(58,30),(29,60),(0,30)],fill=255);thumb.putalpha(mask)
# Der Reglerknopf ist ein separates bewegliches Element, nicht Bestandteil der Schiene.
track=next(a[1] for a in assets if a[0]=='slider-track')
track.paste(track.crop((210,0,232,50)).resize((67,50),Image.Resampling.NEAREST),(232,0))

# Platzsparende Atlanten ohne Neuberechnung oder Skalierung der Originalpixel.
pages=[];spaces=[];records=[]
for name,im,border,source,box in sorted(assets,key=lambda a:a[1].width*a[1].height,reverse=True):
    w,h=im.width+8,im.height+8
    fits=[(r[2]*r[3]-w*h,p,r) for p,free in enumerate(spaces) for r in free if r[2]>=w and r[3]>=h]
    if not fits:
        pages.append(Image.new('RGBA',(2048,2048)));spaces.append([(0,0,2048,2048)]);fits=[(2048*2048-w*h,len(pages)-1,spaces[-1][0])]
    _,p,r=min(fits);x,y=r[:2];new=[]
    for a,b,c,d in spaces[p]:
        if x+w<=a or x>=a+c or y+h<=b or y>=b+d:new.append((a,b,c,d));continue
        if x>a:new.append((a,b,x-a,d))
        if x+w<a+c:new.append((x+w,b,a+c-x-w,d))
        if y>b:new.append((a,b,c,y-b))
        if y+h<b+d:new.append((a,y+h,c,b+d-y-h))
    spaces[p]=[r for i,r in enumerate(new) if not any(i!=j and r[0]>=q[0] and r[1]>=q[1] and r[0]+r[2]<=q[0]+q[2] and r[1]+r[3]<=q[1]+q[3] and (r!=q or i>j) for j,q in enumerate(new))]
    pages[p].paste(im,(x,y));records.append(dict(name=name,page=p,x=x,y=2048-y-im.height,width=im.width,height=im.height,border=border,source=sources[source],sourceRect=list(box)))
for i,page in enumerate(pages):page.save(out/f'atlas-{i}.png')
# Der Atlas muss sämtliche Ausschnittpixel exakt erhalten; auch Überlappungen fallen hier auf.
expected={name:im for name,im,*_ in assets}
for r in records:
    y=2048-r['y']-r['height'];tile=pages[r['page']].crop((r['x'],y,r['x']+r['width'],y+r['height']))
    assert tile.tobytes()==expected[r['name']].tobytes(),r['name']
for old in out.glob('atlas-*.png'):
    if int(old.stem.split('-')[1])>=len(pages):
        assert old.resolve().parent==out.resolve()
        old.unlink();Path(str(old)+'.meta').unlink(missing_ok=True)
(out/'layout.json').write_text(json.dumps(dict(items=records,pages=len(pages)),ensure_ascii=False,indent=2)+'\n',encoding='utf-8',newline='\n')
def meta(asset,kind):
    p=Path(str(asset)+'.meta')
    if p.exists():return
    guid=uuid.uuid4().hex
    if kind=='png':
        body=(out/'atlas-0.png.meta').read_text()
        body=re.sub(r'guid: [a-f0-9]+','guid: '+guid,body);p.write_text(body,encoding='utf-8',newline='\n');return
    body='folderAsset: yes\nDefaultImporter:\n  externalObjects: {}\n' if kind=='dir' else 'TextScriptImporter:\n  externalObjects: {}\n'
    p.write_text('fileFormatVersion: 2\nguid: '+guid+'\n'+body,encoding='utf-8',newline='\n')
meta(out,'dir');meta(out/'layout.json','json')
meta(out/'letters.png','png');meta(out/'letters.json','json')
for i in range(len(pages)):meta(out/f'atlas-{i}.png','png')
print(f'{len(assets)} direkt übernommene Elemente in {len(pages)} Atlanten')
