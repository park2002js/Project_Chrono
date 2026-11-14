-- Aseprite --batch --noinapp --script-param out=<folder> --script <this file>
local out=assert(app.params.out)
local function path(p) return app.fs.joinPath(out,p) end
for _,d in ipairs({'aseprite','png','previews'}) do app.fs.makeAllDirectories(path(d)) end
local concepts=dofile(path('concepts.lua'))
assert(#concepts==10,'Deliver all ten redrawn candidates')
local rgb={O={25,25,35},K={44,45,58},D={77,80,101},W={248,244,234},
 L={207,212,222},S={151,161,181},P={244,211,185},Q={211,159,143}}
local colors={['.']=0}
for k,v in pairs(rgb) do colors[k]=app.pixelColor.rgba(v[1],v[2],v[3],255) end
local function setup(s)
 local layers={s.layers[1],s:newLayer(),s:newLayer()}
 local names={'01 Inner outfit and feet','02 Continuous outer coat','03 Head'}
 for i,l in ipairs(layers) do l.name=names[i] end
 local p=Palette(9);p:setColor(0,Color{r=0,g=0,b=0,a=0})
 for i,k in ipairs({'O','K','D','W','L','S','P','Q'}) do
  local v=rgb[k];p:setColor(i,Color{r=v[1],g=v[2],b=v[3],a=255})
 end
 s:setPalette(p);return layers
end
local function render(s,f)
 local img=Image(20,20,ColorMode.RGB);img:drawSprite(s,f or 1);return img
end
local master=Sprite(20,20,ColorMode.RGB);local masterLayers=setup(master)
local images={};local sheet=Image(20*#concepts,20,ColorMode.RGB)
local report={application='Aseprite '..tostring(app.version),canvas={20,20},
 actualHeight=20,headHeight=13,visualStatus='redrawn candidates, not user approved',concepts={}}
local signatures={}
for n,c in ipairs(concepts) do
 assert(#c.rows==20)
 local expected=Image(20,20,ColorMode.RGB)
 local maps={Image(20,20,ColorMode.RGB),Image(20,20,ColorMode.RGB),Image(20,20,ColorMode.RGB)}
 for y,row in ipairs(c.rows) do
  assert(#row==14,c.id..' width '..#row..' '..row)
  for x=1,14 do
   local k=row:sub(x,x);assert(colors[k]~=nil)
   if y>13 then assert(k~='P' and k~='Q','Skin on body') end
   local layer=(y<=13) and 3 or ((k=='W' or k=='L' or k=='S' or (k=='O' and y<20)) and 2 or 1)
   maps[layer]:drawPixel(x+2,y-1,colors[k]);expected:drawPixel(x+2,y-1,colors[k])
  end
 end
 local s=Sprite(20,20,ColorMode.RGB);local layers=setup(s)
 if n>1 then master:newEmptyFrame(n) end
 for i,img in ipairs(maps) do
  s:newCel(layers[i],1,img,Point(0,0));master:newCel(masterLayers[i],n,img,Point(0,0))
 end
 master.frames[n].duration=1;local tag=master:newTag(n,n);tag.name=c.id..' '..c.title
 local name=c.id..'-'..c.slug
 s:saveAs(path('aseprite/'..name..'.aseprite'))
 local img=render(s);img:saveAs(path('png/'..name..'.png'));s:close()
 local reopened=app.open(path('aseprite/'..name..'.aseprite'))
 assert(reopened.width==20 and reopened.height==20 and #reopened.frames==1 and #reopened.layers==3)
 local again=render(reopened);local png=Image{fromFile=path('png/'..name..'.png')}
 local minX,minY,maxX,maxY=20,20,-1,-1;local signature={};local occupied={};local total=0
 for y=0,19 do for x=0,19 do
  local p=img:getPixel(x,y)
  assert(p==expected:getPixel(x,y) and p==again:getPixel(x,y) and p==png:getPixel(x,y),'Layer or export mismatch')
  local a=app.pixelColor.rgbaA(p);assert(a==0 or a==255)
  table.insert(signature,tostring(p))
  if a==255 then
   minX=math.min(minX,x);minY=math.min(minY,y);maxX=math.max(maxX,x);maxY=math.max(maxY,y)
   occupied[y*20+x]=true;total=total+1
  end
 end end
 assert(minY==0 and maxY==19)
 local key=table.concat(signature,',');assert(not signatures[key],'Duplicate design');signatures[key]=true
 -- Pixel connectivity is a technical check only, not a substitute for visual approval.
 local first=next(occupied);local seen={[first]=true};local queue={first};local qi=1
 while qi<=#queue do
  local p=queue[qi];qi=qi+1;local x,y=p%20,p//20
  for _,d in ipairs({{-1,0},{1,0},{0,-1},{0,1}}) do
   local nx,ny=x+d[1],y+d[2];local np=ny*20+nx
   if nx>=0 and nx<20 and ny>=0 and ny<20 and occupied[np] and not seen[np] then
    seen[np]=true;table.insert(queue,np)
   end
  end
 end
 assert(#queue==total,'Disconnected pixel island')
 table.insert(report.concepts,{id=c.id,file=name,width=maxX-minX+1,height=20,
  nativeReopen=true,pngMatchesNative=true,pixelConnected=true,pixels=total})
 reopened:close();images[n]=img;sheet:drawImage(img,Point((n-1)*20,0))
 print(name..': TECHNICAL PASS 20x20, actual20px, source/native/PNG identical, connected silhouette')
end
master:saveAs(path('player-leftfront-redraw.aseprite'));master:close()
sheet:saveAs(path('player-leftfront-sheet.png'))
local check=app.open(path('player-leftfront-redraw.aseprite'))
assert(check.width==20 and check.height==20 and #check.frames==#concepts)
for n,img in ipairs(images) do
 local again=render(check,n)
 for y=0,19 do for x=0,19 do assert(img:getPixel(x,y)==again:getPixel(x,y)) end end
end
check:close()
local f=assert(io.open(path('validation.json'),'w'));f:write(json.encode(report));f:close()
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
local board=Image(1392,792,ColorMode.RGB)
rect(board,0,0,1392,792,col(22,26,33))
text(board,'PLAYER / REDRAW 02',24,24,4,col(241,238,228))
text(board,'10 LEFT-FRONT STUDIES / 20X20 CANVAS / ACTUAL HEIGHT 20PX / HEAD 13PX',24,70,2,col(157,166,181))
for n,c in ipairs(concepts) do
 local x=24+((n-1)%5)*272;local y=112+((n-1)//5)*320
 rect(board,x,y,256,300,col(226,225,216))
 text(board,c.id,x+14,y+12,2,col(45,49,60))
 text(board,'LEFT-FRONT',x+142,y+16,1,col(88,94,108))
 rect(board,x+28,y+42,200,200,col(184,190,200))
 enlarged(images[n],board,x+28,y+42,10)
 text(board,c.title,x+14,y+254,2,col(28,32,43))
 text(board,c.note,x+14,y+282,1,col(85,91,104))
 local preview=Image(320,320,ColorMode.RGB)
 enlarged(images[n],preview,0,0,16);preview:saveAs(path('previews/'..c.id..'-16x.png'))
end
text(board,'ASEPRITE / 10X PREVIEW / WHITE HAIR + BLACK-WHITE COAT / NO ARMS OR WEAPONS',24,766,2,col(157,166,181))
board:saveAs(path('player-leftfront-comparison.png'))
local small=Image(1060,300,ColorMode.RGB)
rect(small,0,0,1060,300,col(22,26,33))
text(small,'NATIVE 1X / LEFT-FRONT',20,16,1,col(220,224,232))
text(small,'4X / DARK',20,82,1,col(220,224,232))
rect(small,0,184,1060,116,col(226,225,216))
text(small,'4X / LIGHT',20,196,1,col(38,42,52))
for n,img in ipairs(images) do
 local x=20+(n-1)*104
 enlarged(img,small,x+30,36,1)
 text(small,tostring(n),x+34,62,1,col(151,161,181))
 enlarged(img,small,x,100,4);enlarged(img,small,x,216,4)
end
small:saveAs(path('player-leftfront-size-check.png'))
print('TECHNICAL PASS: '..#concepts..' distinct candidates and master reopened; visual acceptance is separate.')


