-- Run with Aseprite --batch --noinapp --script-param out=<folder> --script <file>.
local out=assert(app.params.out)
local function path(p) return app.fs.joinPath(out,p) end
for _,d in ipairs({'aseprite','png','previews'}) do app.fs.makeAllDirectories(path(d)) end
local concepts=dofile(path('concepts.lua'));assert(#concepts==6)
local rgb={O={30,27,39},K={47,44,60},D={81,76,103},W={247,243,230},
 L={207,210,222},S={145,151,174},P={245,207,178},Q={199,139,123}}
local colors={['.']=0}
for k,v in pairs(rgb) do colors[k]=app.pixelColor.rgba(v[1],v[2],v[3],255) end
local function setup(s)
 local layers={s.layers[1],s:newLayer(),s:newLayer()}
 for i,name in ipairs({'01 Outfit - no arms or weapon','02 Hair and head outline','03 Face and right ear'}) do layers[i].name=name end
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
local images={};local sheet=Image(120,20,ColorMode.RGB)
local report={application='Aseprite '..tostring(app.version),canvas={20,20},
 actualHeight=20,headRegionHeight=13,headRatio=0.65,alpha='0 or 255',
 visualStatus='design proposals; user selection pending',
 headNote='13px main head region; tied/long hair tips may extend below it',
 pose='static frontal idle; right ear and rear hair visible',concepts={}}
local signatures={}
for n,c in ipairs(concepts) do
 assert(#c.rows==20,c.id..' must have 20 rows')
 local expected=Image(20,20,ColorMode.RGB)
 local maps={Image(20,20,ColorMode.RGB),Image(20,20,ColorMode.RGB),Image(20,20,ColorMode.RGB)}
 for y,row in ipairs(c.rows) do
  assert(#row==16,c.id..' row '..y..' width '..#row..' '..row)
  for x=1,16 do
   local k=row:sub(x,x);assert(colors[k]~=nil)
   local isSkin=k=='P' or k=='Q'
   assert(not isSkin or y<=13,'Skin below head: '..c.id)
   local face=isSkin or ((y==9 or y==10) and (x==5 or x==9) and k=='O')
   local hair=y<=13 or (n==4 and x>=14) or (n==6 and y<=15 and (x<=5 or x>=12))
   local layer=face and 3 or (hair and 2 or 1)
   maps[layer]:drawPixel(x+1,y-1,colors[k]);expected:drawPixel(x+1,y-1,colors[k])
  end
 end
 local s=Sprite(20,20,ColorMode.RGB);local layers=setup(s)
 if n>1 then master:newEmptyFrame(n) end
 for i,img in ipairs(maps) do s:newCel(layers[i],1,img,Point(0,0));master:newCel(masterLayers[i],n,img,Point(0,0)) end
 master.frames[n].duration=1;local tag=master:newTag(n,n);tag.name=c.id..' '..c.title
 local name=c.id..'-'..c.slug
 s:saveAs(path('aseprite/'..name..'.aseprite'))
 local img=render(s);img:saveAs(path('png/'..name..'.png'));s:close()
 local reopened=app.open(path('aseprite/'..name..'.aseprite'))
 assert(reopened.width==20 and reopened.height==20 and #reopened.frames==1 and #reopened.layers==3)
 local again=render(reopened);local png=Image{fromFile=path('png/'..name..'.png')}
 local minX,minY,maxX,maxY=20,20,-1,-1;local signature={};local occupied={};local total=0;local used={}
 for y=0,19 do for x=0,19 do
  local p=img:getPixel(x,y)
  assert(p==expected:getPixel(x,y) and p==again:getPixel(x,y) and p==png:getPixel(x,y),'Export mismatch')
  local a=app.pixelColor.rgbaA(p);assert(a==0 or a==255)
  table.insert(signature,tostring(p))
  if a==255 then
   minX=math.min(minX,x);minY=math.min(minY,y);maxX=math.max(maxX,x);maxY=math.max(maxY,y)
   occupied[y*20+x]=true;total=total+1;used[p]=true
  end
 end end
 assert(minY==0 and maxY==19 and minX>0 and maxX<19)
 local key=table.concat(signature,',');assert(not signatures[key],'Duplicate design');signatures[key]=true
 local first=next(occupied);local seen={[first]=true};local queue={first};local qi=1
 while qi<=#queue do
  local p=queue[qi];qi=qi+1;local x,y=p%20,p//20
  for _,d in ipairs({{-1,0},{1,0},{0,-1},{0,1}}) do
   local nx,ny=x+d[1],y+d[2];local np=ny*20+nx
   if nx>=0 and nx<20 and ny>=0 and ny<20 and occupied[np] and not seen[np] then seen[np]=true;table.insert(queue,np) end
  end
 end
 assert(#queue==total,'Disconnected pixel island in '..c.id)
 local count=0;for _ in pairs(used) do count=count+1 end
 table.insert(report.concepts,{id=c.id,file=name,width=maxX-minX+1,height=20,colors=count,
  nativeReopen=true,pngMatchesNative=true,pixelConnected=true,pixels=total})
 reopened:close();images[n]=img;sheet:drawImage(img,Point((n-1)*20,0))
 print(name..': PASS; actual '..(maxX-minX+1)..'x20; native and PNG identical')
end
master:saveAs(path('player-idle-concepts.aseprite'));master:close()
sheet:saveAs(path('player-idle-sheet-120x20.png'))
local check=app.open(path('player-idle-concepts.aseprite'))
assert(check.width==20 and check.height==20 and #check.frames==6)
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
local board=Image(1024,952,ColorMode.RGB)
rect(board,0,0,1024,952,col(22,26,33))
text(board,'PLAYER / IDLE V2',32,30,4,col(241,238,228))
text(board,'20 X 20 PX / FRONT + VISIBLE EAR / 6 DESIGNS',34,76,2,col(157,166,181))
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
text(board,'NO ARMS OR WEAPONS / HEAD 13PX OF 20PX / SIX STATIC VARIANTS',32,926,1,col(157,166,181))
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


