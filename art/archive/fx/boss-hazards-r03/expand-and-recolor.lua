-- R02 중심을 유지하고 외곽을 1px 확장한 뒤 같은 형태의 황금색 변형 작성
local input = app.params["input"]
local output = app.params["output"]
assert(input and output, "input and output are required")
local source = app.open(input)
assert(source and source.width==12 and source.height==12, "A 12x12 R02 source is required")
local original = Image(source.spec)
original:drawSprite(source,1)
local normal = Sprite(14,14,ColorMode.RGB)
normal.layers[1].name = "AddedRim1px"
local edge = Color{r=122,g=23,b=45,a=255}
local body = Color{r=244,g=66,b=80,a=255}
local ring = Image(14,14,ColorMode.RGB)
ring:clear()
for y=0,13 do
  for x=0,13 do
    local sx,sy=x-1,y-1
    local inside=sx>=0 and sx<12 and sy>=0 and sy<12 and app.pixelColor.rgbaA(original:getPixel(sx,sy))>0
    if not inside then
      local near=false
      for dy=-1,1 do
        for dx=-1,1 do
          local px,py=sx+dx,sy+dy
          if dx*dx+dy*dy<=1 and px>=0 and px<12 and py>=0 and py<12 and app.pixelColor.rgbaA(original:getPixel(px,py))>0 then near=true end
        end
      end
      if near then ring:drawPixel(x,y,edge) end
    end
  end
end
normal:newCel(normal.layers[1],1,ring,Point(0,0))
for _,layer in ipairs(source.layers) do
  local cel=layer:cel(1)
  if cel then
    local target=normal:newLayer()
    target.name=layer.name=="ExpandedRim2px" and "BodyFromR02Rim" or layer.name
    local image=Image(cel.image)
    for y=0,image.height-1 do
      for x=0,image.width-1 do
        local p=image:getPixel(x,y)
        if app.pixelColor.rgbaA(p)>0 and app.pixelColor.rgbaR(p)==122 and app.pixelColor.rgbaG(p)==23 and app.pixelColor.rgbaB(p)==45 then image:drawPixel(x,y,body) end
      end
    end
    normal:newCel(target,1,image,Point(cel.position.x+1,cel.position.y+1))
  end
end
normal:saveAs(output.."/normal-bullet.aseprite")
normal:saveCopyAs(output.."/normal-bullet.png")

-- 모든 레이어와 알파를 그대로 두고 세 가지 색만 황금 팔레트로 치환
local gold=app.open(output.."/normal-bullet.aseprite")
for _,cel in ipairs(gold.cels) do
  local image=cel.image
  for y=0,image.height-1 do
    for x=0,image.width-1 do
      local p=image:getPixel(x,y)
      if app.pixelColor.rgbaA(p)>0 then
        local color
        if app.pixelColor.rgbaR(p)==255 then color=Color{r=255,g=250,b=224,a=255}
        elseif app.pixelColor.rgbaR(p)==244 then color=Color{r=255,g=196,b=61,a=255}
        else color=Color{r=168,g=108,b=21,a=255} end
        image:drawPixel(x,y,color)
      end
    end
  end
end
gold:saveAs(output.."/gold-bullet.aseprite")
gold:saveCopyAs(output.."/gold-bullet.png")
