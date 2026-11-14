-- Native Aseprite pixel authoring and round-trip verification.
-- Aseprite --batch --noinapp --script-param out=<this folder> --script <this file>
local out=assert(app.params.out)
local function path(p) return app.fs.joinPath(out,p) end
for _,d in ipairs({'aseprite','png','previews'}) do app.fs.makeAllDirectories(path(d)) end
local concepts=dofile(path('concepts.lua'))
assert(#concepts==6)
local rgb={
 O={25,25,35}, K={44,45,58}, D={77,80,101},
 W={248,244,234}, L={207,212,222}, S={151,161,181},
 P={244,211,185}, Q={211,159,143},
}
local colors={['.']=0}
for k,v in pairs(rgb) do colors[k]=app.pixelColor.rgba(v[1],v[2],v[3],255) end
local function setup(s)
 s.layers[1].name='01 Outfit - no arms or weapon'
 local head=s:newLayer(); head.name='02 Head - 12px'
 local p=Palette(9); p:setColor(0,Color{r=0,g=0,b=0,a=0})
 for i,k in ipairs({'O','K','D','W','L','S','P','Q'}) do
  local v=rgb[k]; p:setColor(i,Color{r=v[1],g=v[2],b=v[3],a=255})
 end
 s:setPalette(p)
 return {s.layers[1],head}
end
local function draw(rows,y0,body)
 local img=Image(20,20,ColorMode.RGB)
 for y,row in ipairs(rows) do
  assert(#row==14,'Drawing width '..#row..' in row '..row)
  for x=1,14 do
   local k=row:sub(x,x)
   assert(colors[k]~=nil,'Unknown pixel '..k)
   if body then assert(k~='P' and k~='Q','Skin outside head') end
   local px,py=x+2,y0+y-1
   assert(px>=0 and px<20 and py>=0 and py<20)
   img:drawPixel(px,py,colors[k])
  end
 end
 return img
end
local function render(s,f)
 local img=Image(20,20,ColorMode.RGB);img:drawSprite(s,f or 1);return img
end
local master=Sprite(20,20,ColorMode.RGB)
local masterLayers=setup(master)
local sheet=Image(120,20,ColorMode.RGB)
local images={}
local report={application='Aseprite '..tostring(app.version),canvas={20,20},
 headHeight=12,occupiedHeight=18,headRatio=12/18,alpha='0 or 255',
 pose='front static idle',weapon=false,arms=false,hands=false,concepts={}}
for n,c in ipairs(concepts) do
 assert(#c.head==12 and #c.body==6)
 local maps={draw(c.body,13,true),draw(c.head,1,false)}
 local s=Sprite(20,20,ColorMode.RGB);local layers=setup(s)
 if n>1 then master:newEmptyFrame(n) end
 for i,img in ipairs(maps) do
  s:newCel(layers[i],1,img,Point(0,0))
  master:newCel(masterLayers[i],n,img,Point(0,0))
 end
 local tag=master:newTag(n,n);tag.name=c.id..' '..c.title
 master.frames[n].duration=1
 local name=c.id..'-'..c.slug
 local native=path('aseprite/'..name..'.aseprite')
 s:saveAs(native)
 local img=render(s);img:saveAs(path('png/'..name..'.png'));s:close()
 local reopened=app.open(native)
 assert(reopened.width==20 and reopened.height==20)
 assert(#reopened.frames==1 and #reopened.layers==2)
 local again=render(reopened);local png=Image{fromFile=path('png/'..name..'.png')}
 assert(png.width==20 and png.height==20)
 local minX,minY,maxX,maxY=20,20,-1,-1
 local used={};local pixels=0
 for y=0,19 do for x=0,19 do
  local p=img:getPixel(x,y)
  assert(p==again:getPixel(x,y) and p==png:getPixel(x,y),'Round-trip changed pixel')
  local a=app.pixelColor.rgbaA(p);assert(a==0 or a==255,'Partial alpha')
  if a==255 then
   minX=math.min(minX,x);minY=math.min(minY,y);maxX=math.max(maxX,x);maxY=math.max(maxY,y)
   used[tostring(p)]=true;pixels=pixels+1
  end
 end end
 local colorCount=0;for _ in pairs(used) do colorCount=colorCount+1 end
 assert(colorCount<=8 and minY==1 and maxY==18 and maxX-minX+1<20)
 table.insert(report.concepts,{id=c.id,file=name,occupiedWidth=maxX-minX+1,
  occupiedHeight=maxY-minY+1,pixels=pixels,colors=colorCount,nativeReopen=true,pngMatchesNative=true})
 reopened:close();images[n]=img;sheet:drawImage(img,Point((n-1)*20,0))
 print(name..': PASS 20x20, head 12/18px, alpha 0/255, native/PNG identical')
end
master:saveAs(path('player-idle-concepts.aseprite'));master:close()
sheet:saveAs(path('player-idle-sheet-120x20.png'))
local reopened=app.open(path('player-idle-concepts.aseprite'))
assert(reopened.width==20 and reopened.height==20 and #reopened.frames==6)
for n,img in ipairs(images) do
 local again=render(reopened,n)
 for y=0,19 do for x=0,19 do assert(img:getPixel(x,y)==again:getPixel(x,y)) end end
end
reopened:close()
local log=assert(io.open(path('validation.json'),'w'));log:write(json.encode(report));log:close()
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
local board=Image(1024,952,ColorMode.RGB)
rect(board,0,0,1024,952,col(22,26,33))
text(board,'PLAYER / IDLE',32,30,4,col(241,238,228))
text(board,'20 X 20 PX / FRONT VIEW / 6 CONCEPTS',34,76,2,col(157,166,181))
rect(board,32,108,960,1,col(67,75,90))
for n,c in ipairs(concepts) do
  local x=32+((n-1)%3)*328; local y=132+((n-1)//3)*376
  rect(board,x,y,304,352,col(226,225,216))
  text(board,c.id,x+16,y+14,2,col(59,64,78))
  text(board,'20X20',x+228,y+14,1,col(88,94,108))
  for yy=0,19 do for xx=0,19 do
    local bg=((xx//2+yy//2)%2==0) and col(184,190,200) or col(177,183,193)
    rect(board,x+32+xx*12,y+42+yy*12,12,12,bg)
  end end
  enlarged(images[n],board,x+32,y+42,12)
  text(board,c.title,x+16,y+303,2,col(28,32,43))
  text(board,c.note,x+16,y+331,1,col(85,91,104))
  local preview=Image(240,240,ColorMode.RGB)
  enlarged(images[n],preview,0,0,12)
  preview:saveAs(path('previews/'..c.id..'-12x.png'))
end
text(board,'ASEPRITE / 12X PIXEL PREVIEW / ORIGINALS 20X20',32,892,2,col(157,166,181))
text(board,'NO ARMS OR WEAPONS / HEAD 12PX OF 18PX / SIX STATIC VARIANTS',32,926,1,col(157,166,181))
board:saveAs(path('player-idle-comparison.png'))
local sizeCheck=Image(640,288,ColorMode.RGB)
rect(sizeCheck,0,0,640,288,col(22,26,33))
text(sizeCheck,'NATIVE 1X / DARK',16,12,1,col(220,224,232))
text(sizeCheck,'4X / DARK',16,84,1,col(220,224,232))
rect(sizeCheck,0,180,640,108,col(226,225,216))
text(sizeCheck,'4X / LIGHT',16,190,1,col(38,42,52))
for n,img in ipairs(images) do
 local x=16+(n-1)*104
 enlarged(img,sizeCheck,x+30,34,1)
 text(sizeCheck,tostring(n),x+36,61,1,col(151,161,181))
 enlarged(img,sizeCheck,x,100,4)
 enlarged(img,sizeCheck,x,208,4)
end
sizeCheck:saveAs(path('player-idle-size-check.png'))
print('PASS: six native files, six PNGs, six-frame master reopened; Aseprite comparison exported.')

