-- Export the edited Aseprite file and regenerate usage previews without changing the source.
local output = assert(app.params["output"],"Missing output directory")
local input = app.params["input"] or (output.."/window-frame.aseprite")
local sprite=assert(app.open(input),"Cannot open window-frame.aseprite")
local W,H,B=256,64,12
assert(sprite.width==W and sprite.height==H,"Keep the 256x64 source canvas")
assert(#sprite.frames==1,"The frame is a static asset")
assert(#sprite.slices==1,"Expected the saved nine-slice")
local center=sprite.slices[1].center
assert(center and center.x==12 and center.y==12 and center.width==232 and center.height==40,
  "Nine-slice must keep 12px on each side")
local image=Image(sprite)
image:saveAs(output.."/window-frame.png")
local function scale(source,factor)
  local result=Image(source.width*factor,source.height*factor,ColorMode.RGB)
  for y=0,result.height-1 do for x=0,result.width-1 do
    result:drawPixel(x,y,source:getPixel(x//factor,y//factor))
  end end
  return result
end
local function sourcePosition(p,total,sourceTotal)
  if p<B then return p end
  if p>=total-B then return sourceTotal-(total-p) end
  return B+math.floor((p-B)*(sourceTotal-2*B)/(total-2*B))
end
local function nineSlice(width,height)
  assert(width>=B*2+1 and height>=B*2+1,"Window is smaller than its fixed corners")
  local result=Image(width,height,ColorMode.RGB)
  for y=0,height-1 do for x=0,width-1 do
    result:drawPixel(x,y,image:getPixel(sourcePosition(x,width,W),sourcePosition(y,height,H)))
  end end
  return result
end
local function fill(target,x,y,w,h,color)
  for yy=y,y+h-1 do for xx=x,x+w-1 do target:drawPixel(xx,yy,color) end end
end
-- Small pixel alphabet belongs only to the layout previews, never to the frame texture.
local glyphs={
  s={"00000","00000","01111","10000","01110","00001","11110"},
  a={"00000","00000","01110","00001","01111","10001","01111"},
  v={"00000","00000","10001","10001","10001","01010","00100"},
  e={"00000","00000","01110","10001","11111","10000","01110"},
  l={"01100","00100","00100","00100","00100","00100","01110"},
  o={"00000","00000","01110","10001","10001","10001","01110"},
  t={"00100","00100","11111","00100","00100","00100","00011"},
  f={"00110","01000","11100","01000","01000","01000","01000"},
  i={"00100","00000","01100","00100","00100","00100","01110"},
  n={"00000","00000","11110","10001","10001","10001","10001"},
  g={"00000","01111","10001","10001","01111","00001","01110"},
  ["0"]={"01110","10001","10011","10101","11001","10001","01110"},
  ["1"]={"00100","01100","00100","00100","00100","00100","01110"},
  ["2"]={"01110","10001","00001","00010","00100","01000","11111"},
  ["3"]={"11110","00001","00001","01110","00001","00001","11110"}
}
local function text(target,value,x,y,color,size)
  for k=1,#value do
    local glyph=glyphs[value:sub(k,k)]
    if glyph then for row=1,7 do for col=1,5 do
      if glyph[row]:sub(col,col)=="1" then
        fill(target,x+(k-1)*6*size+(col-1)*size,y+(row-1)*size,size,size,color)
      end
    end end end
  end
end
local bg=app.pixelColor.rgba(21,21,24,255)
local heading=app.pixelColor.rgba(161,171,195,255)
local body=app.pixelColor.rgba(113,127,153,255)
local function canvas()
  local result=Image(640,360,ColorMode.RGB)
  result:clear(bg)
  return result
end
image:saveAs(output.."/window-frame.png")
scale(image,4):saveAs(output.."/preview-slot-4x.png")
local layout=canvas()
text(layout,"saveslot",(640-(8*6-1)*2)//2,36,heading,2)
for i=1,3 do
  local y=86+(i-1)*78
  layout:drawImage(image,Point(192,y))
  text(layout,string.format("save file 0%d",i),208,y+27,body,1)
end
for _,factor in ipairs({2,3,4}) do
  scale(layout,factor):saveAs(output..string.format("/preview-layout-%dx%d.png",640*factor,360*factor))
end
local horizontal=canvas()
text(horizontal,"saveslot",(640-(8*6-1)*2)//2,36,heading,2)
for i=1,3 do
  local x=80+(i-1)*168
  horizontal:drawImage(nineSlice(144,206),Point(x,104))
  text(horizontal,string.format("save file 0%d",i),x+37,203,body,1)
end
for _,factor in ipairs({2,3,4}) do
  scale(horizontal,factor):saveAs(output..string.format("/preview-horizontal-%dx%d.png",640*factor,360*factor))
end
local settings=canvas()
local window=nineSlice(320,224)
settings:drawImage(window,Point(160,68))
text(settings,"settings",(640-(8*6-1)*2)//2,87,heading,2)
scale(settings,3):saveAs(output.."/preview-settings-1920x1080.png")
-- Compare an intentionally wider row and a small row as well as the large window.
local sizes={{w=160,h=48},{w=256,h=64},{w=416,h=64},{w=320,h=224}}
local reports={}
for i,entry in ipairs(sizes) do
  local resized=nineSlice(entry.w,entry.h)
  for y=0,B-1 do for x=0,B-1 do
    assert(resized:getPixel(x,y)==image:getPixel(x,y),"Top left corner changed")
    assert(resized:getPixel(entry.w-B+x,y)==image:getPixel(W-B+x,y),"Top right corner changed")
    assert(resized:getPixel(x,entry.h-B+y)==image:getPixel(x,H-B+y),"Bottom left corner changed")
    assert(resized:getPixel(entry.w-B+x,entry.h-B+y)==image:getPixel(W-B+x,H-B+y),"Bottom right corner changed")
  end end
  reports[i]={width=entry.w,height=entry.h,fourCornersUnchanged=true}
end
local paletteSet,alphaSet={},{}
for y=0,H-1 do for x=0,W-1 do
  local pixel=image:getPixel(x,y)
  paletteSet[tostring(pixel)]=true
  alphaSet[tostring(app.pixelColor.rgbaA(pixel))]=true
end end
local colorCount=0
for _ in pairs(paletteSet) do colorCount=colorCount+1 end
assert(alphaSet["0"] and alphaSet["255"],"Expected transparent silhouette and opaque panel")
for a in pairs(alphaSet) do assert(a=="0" or a=="255","Antialiased pixel found") end
for y=B,H-B-1 do for x=B,W-B-1 do
  assert(image:getPixel(x,y)==image:getPixel(B,B),"Center is not uniform")
end end
local result={
  aseprite=tostring(app.version),width=W,height=H,layers=#sprite.layers,frames=#sprite.frames,
  slice={left=B,right=B,top=B,bottom=B,center={x=12,y=12,width=232,height=40}},
  colorsIncludingTransparent=colorCount,alphaValues={0,255},flatCenter=true,
  resizeChecks=reports,previews={{width=1280,height=720,integerScale=2},
    {width=1920,height=1080,integerScale=3},{width=2560,height=1440,integerScale=4}},
  nativeReopened=true,sourceRewritten=false,unityImport="Not executed",sceneConnection="Not changed"
}
local file=assert(io.open(output.."/validation.json","w"))
file:write(json.encode(result)); file:close()
print("Exported frame and previews; saved slice, palette, alpha and four-size corner checks passed")
