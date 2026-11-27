# 설정 창 시안 렌더러: 저장 슬롯 프레임을 336×216으로 늘린 겉틀 안의 사운드·화면 묶음을 1920×1080으로 출력
# 실행: py -3.11 render.py <출력 폴더> (프로젝트 루트에서)
import sys
from PIL import Image, ImageDraw, ImageFont
out=sys.argv[1]
K=3
FRAME=Image.open('project/Assets/Art/UI/Windows/window-frame.png').convert('RGBA')
FONT='project/Assets/Font/PF스타더스트 3.0.ttf'
BG=(21,21,24); TXT=(161,171,195); WHITE=(255,255,255); CYAN=(122,245,255); DIM=(91,102,128)
EDGE=(28,47,76); EDGE2=(37,62,96); INK=(7,10,18); CELL=(12,19,33); FILL=(62,150,182); PURPLE=(68,43,88)
def nine(img,x,y,w,h,b=12):
    s=FRAME; W,H=s.size
    xs=[0,b,W-b,W]; ys=[0,b,H-b,H]; dx=[x,x+b,x+w-b,x+w]; dy=[y,y+b,y+h-b,y+h]
    for i in range(3):
        for j in range(3):
            part=s.crop((xs[i],ys[j],xs[i+1],ys[j+1])).resize((dx[i+1]-dx[i],dy[j+1]-dy[j]),Image.NEAREST)
            img.alpha_composite(part,(dx[i],dy[j]))
def rect(d,x,y,w,h,c): d.rectangle([x,y,x+w-1,y+h-1],fill=c)
def cell(d,x,y,w,h,hl=False):
    rect(d,x+1,y,w-2,h,EDGE2 if hl else EDGE); rect(d,x,y+1,w,h-2,EDGE2 if hl else EDGE)
    rect(d,x+1,y+1,w-2,h-2,CELL)
def arrow(d,x,y,left,hover=False):
    rect(d,x+1,y,10,12,INK); rect(d,x,y+1,12,10,INK); rect(d,x+1,y+1,10,10,EDGE)
    c=CYAN if hover else TXT
    for k in range(4):
        xx = x+7-k if left else x+4+k
        rect(d,xx,y+6-k-1+1,1,2*k+1-1+1,c) if False else rect(d,xx,y+6-k,1,2*k,c) if k else None
    # simple chevron
def chevron(d,x,y,left,c):
    pts=[(0,0),(1,1),(2,2),(1,3),(0,4)] if not left else [(2,0),(1,1),(0,2),(1,3),(2,4)]
    for px,py in pts: rect(d,x+px,y+py,1,1,c); rect(d,x+px+1,y+py,1,1,c)
def button(d,x,y,left,hover=False):
    rect(d,x+1,y,10,12,INK); rect(d,x,y+1,12,10,INK); rect(d,x+1,y+1,10,10,EDGE)
    chevron(d,x+4 if not left else x+4,y+4-0+0-0,left,CYAN if hover else TXT)
def pill(d,x,y,w,h,active):
    c=CYAN if active else DIM
    rect(d,x+2,y,w-4,h,INK); rect(d,x,y+2,w,h-4,INK); rect(d,x+1,y+1,w-2,h-2,INK)
    rect(d,x+2,y+1,w-4,h-2,c); rect(d,x+1,y+2,w-2,h-4,c); rect(d,x+2,y+2,w-4,h-4,CELL)
def slider(d,x,y,w,v):
    rect(d,x,y,w,6,INK); rect(d,x+1,y+1,w-2,4,EDGE)
    fw=int((w-2)*v); rect(d,x+1,y+1,fw,4,FILL); rect(d,x+1,y+1,fw,1,(96,190,215))
    hx=x+1+fw-2; rect(d,hx-1,y-4,6,14,INK); rect(d,hx,y-3,4,12,WHITE); rect(d,hx,y+7,4,2,(190,200,220))
def make(path,pending,hover_confirm=False):
    base=Image.new('RGBA',(640,360),BG+(255,))
    d=ImageDraw.Draw(base)
    nine(base,152,76,336,216)
    d=ImageDraw.Draw(base)
    # section underline
    for y in (106,182): rect(d,176,y,288,1,EDGE)
    for y,name,v in ((112,'bgm',0.8),(136,'sfx',1.0)):
        cell(d,172,y,296,20); slider(d,252,y+7,160,v)
    cell(d,172,188,296,20,pending); cell(d,172,212,296,20)
    for y in (188,212):
        button(d,252,y+4,True); button(d,400,y+4,False,hover=(y==188))
    pill(d,236,244,76,20,pending or hover_confirm); pill(d,328,244,76,20,False)
    img=base.resize((1920,1080),Image.NEAREST); dd=ImageDraw.Draw(img)
    f=ImageFont.truetype(FONT,30); fh=ImageFont.truetype(FONT,60); fs=ImageFont.truetype(FONT,30)
    def t(x,y,w,h,s,c,font=f,align='c'):
        bb=dd.textbbox((0,0),s,font=font); tw=bb[2]-bb[0]; th=bb[3]-bb[1]
        X=x*K+((w*K-tw)//2 if align=='c' else (0 if align=='l' else w*K-tw))-bb[0]; Y=y*K+(h*K-th)//2-bb[1]
        dd.text((X,Y),s,font=font,fill=c)
    t(80,34,480,36,'settings',WHITE,fh)
    t(178,88,100,16,'사운드',WHITE,align='l'); t(178,164,100,16,'화면',WHITE,align='l')
    t(182,112,64,20,'배경음',TXT,align='l'); t(418,112,44,20,'80%',TXT,align='r')
    t(182,136,64,20,'효과음',TXT,align='l'); t(418,136,44,20,'100%',TXT,align='r')
    t(182,188,64,20,'해상도',TXT,align='l'); t(266,188,132,20,'1600 x 900',CYAN if pending else WHITE)
    t(182,212,64,20,'화면 모드',TXT,align='l'); t(266,212,132,20,'창 모드' if pending else '전체 화면',CYAN if pending else WHITE)
    t(236,244,76,20,'확정',CYAN if pending else DIM); t(328,244,76,20,'닫기',WHITE)
    if pending: t(172,266,296,10,'확정을 누르면 해상도와 화면 모드가 바뀝니다',DIM,ImageFont.truetype(FONT,24))
    img.save(path)
make(out+'/settings-pending.png',True)
make(out+'/settings-applied.png',False)
