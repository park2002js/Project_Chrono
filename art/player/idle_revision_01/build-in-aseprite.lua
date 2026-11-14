-- Run inside Aseprite: --batch --noinapp --script-param out=<this folder> --script <this file>
local out=assert(app.params.out)
local function path(p) return app.fs.joinPath(out,p) end
app.fs.makeAllDirectories(out)
local previous=app.fs.joinPath(out,'../idle_concepts_20x20_20261115')
local source=app.open(app.fs.joinPath(previous,'aseprite/04-silver-sweep.aseprite'))
local oldHead=Image(20,20,ColorMode.RGB);oldHead:drawSprite(source,1);source:close()
local head=Image(20,20,ColorMode.RGB)
-- Add a crown row, preserving the original asymmetric fringe and face pixel-for-pixel.
for y=0,12 do
 local sourceY=(y<=2) and y+1 or y
 for x=0,19 do head:drawPixel(x,y,oldHead:getPixel(x,sourceY)) end
end
local rgb={O={25,25,35},K={44,45,58},D={77,80,101},W={248,244,234},
 L={207,212,222},S={151,161,181},P={244,211,185},Q={211,159,143}}
local colors={['.']=0}
for k,v in pairs(rgb) do colors[k]=app.pixelColor.rgba(v[1],v[2],v[3],255) end
local function pixels(rows)
 local img=Image(20,20,ColorMode.RGB)
 for y,row in ipairs(rows) do
  assert(#row==14,'Expected 14 columns: '..row)
  assert(row==row:reverse(),'Outfit must be horizontally symmetric')
  for x=1,14 do
   local k=row:sub(x,x);assert(colors[k]~=nil and k~='P' and k~='Q')
   img:drawPixel(x+2,y+12,colors[k])
  end
 end
 return img
end
local outfit=pixels{
 '...OWLKKLWO...',
 '...OWWKKWWO...',
 '....OKKKKO....',
 '....OKDDKO....',
 '.....OKKO.....',
 '....OKOOKO....',
 '....OO..OO....',
}
local cloak=pixels{
 '...OW....WO...',
 '..OWW....WWO..',
 '.OWWO....OWWO.',
 'OWWLO....OLWWO',
 'OWLO......OLWO',
 'OOO........OOO',
 '..............',
}
-- Split the actual cloak into independently editable left/right panels.
local left=Image(20,20,ColorMode.RGB);local right=Image(20,20,ColorMode.RGB)
for y=0,19 do for x=0,19 do
 (x<10 and left or right):drawPixel(x,y,cloak:getPixel(x,y))
end end
local sprite=Sprite(20,20,ColorMode.RGB)
local layers={sprite.layers[1],sprite:newLayer(),sprite:newLayer(),sprite:newLayer()}
local names={'01 Cloak left','02 Cloak right','03 Inner outfit and feet','04 Head - silver sweep'}
local maps={left,right,outfit,head}
for i,layer in ipairs(layers) do
 layer.name=names[i];sprite:newCel(layer,1,maps[i],Point(0,0))
end
local palette=Palette(9);palette:setColor(0,Color{r=0,g=0,b=0,a=0})
for i,k in ipairs({'O','K','D','W','L','S','P','Q'}) do
 local c=rgb[k];palette:setColor(i,Color{r=c[1],g=c[2],b=c[3],a=255})
end
sprite:setPalette(palette)
local function render(s)
 local img=Image(20,20,ColorMode.RGB);img:drawSprite(s,1);return img
end
local result=render(sprite)
sprite:saveAs(path('player-front-idle-r01.aseprite'))
result:saveAs(path('player-front-idle-r01.png'))
sprite:close()
local reopened=app.open(path('player-front-idle-r01.aseprite'))
assert(reopened.width==20 and reopened.height==20 and #reopened.layers==4 and #reopened.frames==1)
local check=render(reopened);local png=Image{fromFile=path('player-front-idle-r01.png')}
local minX,minY,maxX,maxY=20,20,-1,-1
for y=0,19 do for x=0,19 do
 local p=result:getPixel(x,y)
 assert(p==check:getPixel(x,y) and p==png:getPixel(x,y),'Round-trip mismatch')
 local alpha=app.pixelColor.rgbaA(p);assert(alpha==0 or alpha==255)
 if y>=13 then assert(p==result:getPixel(19-x,y),'Body symmetry mismatch') end
 if alpha==255 then
  minX=math.min(minX,x);minY=math.min(minY,y);maxX=math.max(maxX,x);maxY=math.max(maxY,y)
 end
end end
assert(minY==0 and maxY==19,'Actual artwork must be exactly 20px tall')
assert(maxX-minX+1<=20)
-- Two transparent channels separate the lower cloak from the body.
assert(result:getPixel(7,17)==0 and result:getPixel(12,17)==0)
assert(result:getPixel(6,18)==0 and result:getPixel(13,18)==0)
reopened:close()
local f=assert(io.open(path('validation.json'),'w'))
f:write(json.encode{application='Aseprite '..tostring(app.version),canvas={20,20},
 occupiedWidth=maxX-minX+1,occupiedHeight=20,headHeight=13,headRatio=13/20,
 outfitHorizontallySymmetric=true,cloakLayers=2,layers=4,frames=1,
 nativeReopen=true,pngMatchesNative=true,alpha='0 or 255'})
f:close()
print('PASS: 20x20 canvas; actual height20px; head13px; symmetric outfit; 4 layers; PNG round-trip exact')
-- Aseprite contact sheet; nearest-neighbor blocks, no filtering.
local function col(r,g,b) return app.pixelColor.rgba(r,g,b,255) end
local function rect(img,x,y,w,h,c)
  for yy=y,y+h-1 do for xx=x,x+w-1 do img:drawPixel(xx,yy,c) end end
end
local font={
 A={'01110','10001','10001','11111','10001','10001','10001'},
 B={'11110','10001','10001','11110','10001','10001','11110'},
 C={'01111','10000','10000','10000','10000','10000','01111'},
 D={'11110','10001','10001','10001','10001','10001','11110'},
 E={'11111','10000','10000','11110','10000','10000','11111'},
 F={'11111','10000','10000','11110','10000','10000','10000'},
 G={'01111','10000','10000','10111','10001','10001','01111'},
 H={'10001','10001','10001','11111','10001','10001','10001'},
 I={'111','010','010','010','010','010','111'},
 J={'00111','00010','00010','00010','10010','10010','01100'},
 K={'10001','10010','10100','11000','10100','10010','10001'},
 L={'10000','10000','10000','10000','10000','10000','11111'},
 M={'10001','11011','10101','10101','10001','10001','10001'},
 N={'10001','11001','10101','10011','10001','10001','10001'},
 O={'01110','10001','10001','10001','10001','10001','01110'},
 P={'11110','10001','10001','11110','10000','10000','10000'},
 Q={'01110','10001','10001','10001','10101','10010','01101'},
 R={'11110','10001','10001','11110','10100','10010','10001'},
 S={'01111','10000','10000','01110','00001','00001','11110'},
 T={'11111','00100','00100','00100','00100','00100','00100'},
 U={'10001','10001','10001','10001','10001','10001','01110'},
 V={'10001','10001','10001','10001','10001','01010','00100'},
 W={'10001','10001','10001','10101','10101','10101','01010'},
 X={'10001','10001','01010','00100','01010','10001','10001'},
 Y={'10001','10001','01010','00100','00100','00100','00100'},
 Z={'11111','00001','00010','00100','01000','10000','11111'},
 ['0']={'01110','10001','10011','10101','11001','10001','01110'},
 ['1']={'010','110','010','010','010','010','111'},
 ['2']={'01110','10001','00001','00010','00100','01000','11111'},
 ['3']={'11110','00001','00001','01110','00001','00001','11110'},
 ['4']={'10010','10010','10010','11111','00010','00010','00010'},
 ['5']={'11111','10000','10000','11110','00001','00001','11110'},
 ['6']={'01110','10000','10000','11110','10001','10001','01110'},
 ['7']={'11111','00001','00010','00100','01000','01000','01000'},
 ['8']={'01110','10001','10001','01110','10001','10001','01110'},
 ['9']={'01110','10001','10001','01111','00001','00001','01110'},
 ['/']={'00001','00001','00010','00100','01000','10000','10000'},
 ['+']={'000','010','010','111','010','010','000'},
 ['-']={'000','000','000','111','000','000','000'},
 ['.']={'0','0','0','0','0','1','1'},
 [' ']={'000','000','000','000','000','000','000'},
}
local function text(img,str,x,y,scale,c)
  for ch in str:gmatch('.') do
    local glyph=assert(font[ch],'Missing glyph '..ch)
    for gy,line in ipairs(glyph) do for gx=1,#line do
      if line:sub(gx,gx)=='1' then rect(img,x+(gx-1)*scale,y+(gy-1)*scale,scale,scale,c) end
    end end
    x=x+(#glyph[1]+1)*scale
  end
end
local function enlarged(img,target,x,y,scale)
  for sy=0,19 do for sx=0,19 do
    local c=img:getPixel(sx,sy)
    if app.pixelColor.rgbaA(c)>0 then rect(target,x+sx*scale,y+sy*scale,scale,scale,c) end
  end end
end
local preview=Image(320,320,ColorMode.RGB)
enlarged(result,preview,0,0,16)
preview:saveAs(path('player-front-idle-r01-16x.png'))
local board=Image(832,512,ColorMode.RGB)
rect(board,0,0,832,512,col(22,26,33))
text(board,'PLAYER / REVISION 01',24,22,3,col(241,238,228))
text(board,'04 HAIR + 06 OUTFIT / ACTUAL HEIGHT 20PX',24,60,2,col(157,166,181))
for i=1,2 do
 local x=24+(i-1)*408;local bg=i==1 and col(192,197,205) or col(48,53,65)
 rect(board,x,100,376,348,bg)
 enlarged(result,board,x+28,112,16)
 text(board,i==1 and 'LIGHT' or 'DARK',x+12,458,2,col(157,166,181))
end
text(board,'13PX HEAD / 7PX BODY / CLOAK PANELS ON SEPARATE LAYERS',24,490,1,col(157,166,181))
board:saveAs(path('player-front-idle-r01-preview.png'))
local single=Image(360,360,ColorMode.RGB)
rect(single,0,0,360,360,col(192,197,205))
enlarged(result,single,20,20,16)
single:saveAs(path('player-front-idle-r01-preview-single.png'))

